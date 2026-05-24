using System.Globalization;
using System.IO;
using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Models;
using EvUdsAnalyzer.Infrastructure.IsoTp;

namespace EvUdsAnalyzer.Infrastructure.Parsers;

public sealed class TrcParser : ITrcParser
{
    private static readonly HashSet<string> DataFrameTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DT", "FD", "FB", "FE", "BI", "Rx", "Tx", "D", "d"
    };


    public static bool InferIsRxFromUdsStyle(uint canId)
    {
        if (canId <= 0x7FF)
        {
            return (canId & 0xF) >= 0x8;
        }

        byte target = (byte)((canId >> 8) & 0xFF);
        byte source = (byte)((canId & 0xFF));

        return source < target;
    }

    public async Task<IReadOnlyList<CanFrame>> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        var frames = new List<CanFrame>(capacity: 4096);
        string[]? columns = null;
        var lineNumber = 0;

        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith(";$COLUMNS=", StringComparison.OrdinalIgnoreCase))
            {
                columns = trimmed[";$COLUMNS=".Length..]
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                continue;
            }

            if (trimmed.StartsWith(';'))
            {
                continue;
            }

            if (TryParseLine(trimmed, lineNumber, columns, out var frame))
            {
                frames.Add(frame);
            }
        }

        return frames;
    }

    private static bool TryParseLine(string line, int lineNumber, string[]? columns, out CanFrame frame)
    {
        frame = new CanFrame();
        var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 4)
        {
            return false;
        }

        if (columns is not null && TryParseColumnDriven(tokens, lineNumber, columns, out frame))
        {
            return true;
        }

        return TryParseLegacyOrCompact(tokens, lineNumber, out frame);
    }

    private static bool TryParseColumnDriven(string[] tokens, int lineNumber, string[] columns, out CanFrame frame)
    {
        frame = new CanFrame();
        var index = 0;
        var timestamp = 0d;
        var bus = (int?)null;
        var type = "";
        var id = "";
        var direction = "";
        var data = Array.Empty<byte>();

        foreach (var column in columns)
        {
            if (column == "D")
            {
                data = ParseDataBytes(tokens.Skip(index));
                break;
            }

            if (index >= tokens.Length)
            {
                return false;
            }

            var token = tokens[index++].TrimEnd(')');
            switch (column)
            {
                case "N":
                    break;
                case "O":
                    if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out timestamp))
                    {
                        return false;
                    }
                    break;
                case "T":
                    type = token;
                    break;
                case "B":
                    if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBus))
                    {
                        bus = parsedBus;
                    }
                    break;
                case "I":
                    id = token;
                    break;
                case "d":
                    direction = token;
                    break;
                case "R":
                case "l":
                case "L":
                case "V":
                case "S":
                case "A":
                case "r":
                case "s":
                    break;
            }
        }

        return BuildFrame(lineNumber, timestamp, bus, type, id, direction, data, out frame);
    }

    private static bool TryParseLegacyOrCompact(string[] tokens, int lineNumber, out CanFrame frame)
    {
        frame = new CanFrame();
        var start = tokens[0].EndsWith(')') ? 1 : 0;
        if (tokens.Length - start < 4)
        {
            return false;
        }

        if (!double.TryParse(tokens[start], NumberStyles.Float, CultureInfo.InvariantCulture, out var timestamp))
        {
            return false;
        }

        int? bus = null;
        var cursor = start + 1;

        if (cursor < tokens.Length && int.TryParse(tokens[cursor], out var parsedBus) &&
            cursor + 3 < tokens.Length &&
            (LooksLikeCanId(tokens[cursor + 1]) || IsDirection(tokens[cursor + 1])))
        {
            bus = parsedBus;
            cursor++;
        }

        string id;
        string direction;
        string type = "";

        if (cursor < tokens.Length && IsDirection(tokens[cursor]))
        {
            direction = tokens[cursor++];
            id = cursor < tokens.Length ? tokens[cursor++] : "";
        }
        else if (cursor + 1 < tokens.Length && LooksLikeCanId(tokens[cursor]) && IsDirection(tokens[cursor + 1]))
        {
            id = tokens[cursor++];
            direction = tokens[cursor++];
        }
        else if (cursor + 2 < tokens.Length && LooksLikeCanId(tokens[cursor]) && IsDirection(tokens[cursor + 1]))
        {
            id = tokens[cursor++];
            direction = tokens[cursor++];
        }
        else
        {
            return false;
        }

        if (cursor < tokens.Length && !byte.TryParse(tokens[cursor], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            type = tokens[cursor++];
        }

        if (cursor < tokens.Length && int.TryParse(tokens[cursor], out _))
        {
            cursor++;
        }

        var data = ParseDataBytes(tokens.Skip(cursor));
        return BuildFrame(lineNumber, timestamp, bus, type, id, direction, data, out frame);
    }

    private static bool BuildFrame(int lineNumber, double timestamp, int? bus, string type, string id, string direction, byte[] data, out CanFrame frame)
    {
        frame = new CanFrame();
        if (!DataFrameTypes.Contains(type) && !string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        if (!IsDirection(direction) || !TryParseCanId(id, out var canIdValue))
        {
            return false;
        }

        frame = new CanFrame
        {
            LineNumber = lineNumber,
            TimestampMs = timestamp,
            Bus = bus,
            CanIdValue = canIdValue,
            CanId = FormatCanId(canIdValue),
            IsRx = InferIsRxFromUdsStyle(canIdValue),
            RawType = type,
            Data = data,
            IsoTpFrameType = IsoTpUtilities.GetFrameType(data)
        };
        return true;
    }

    private static byte[] ParseDataBytes(IEnumerable<string> tokens)
    {
        var bytes = new List<byte>(8);
        foreach (var token in tokens)
        {
            if (token.Equals("RTR", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (token.Length is < 1 or > 2)
            {
                break;
            }

            if (byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                bytes.Add(value);
            }
            else
            {
                break;
            }
        }

        return bytes.ToArray();
    }

    private static bool IsDirection(string value) =>
        value.Equals("Rx", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Tx", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeCanId(string value) => TryParseCanId(value, out _);

    private static bool TryParseCanId(string value, out uint canId)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out canId);
    }

    private static string FormatCanId(uint canId) => canId <= 0x7FF ? canId.ToString("X3") : canId.ToString("X8");
}
