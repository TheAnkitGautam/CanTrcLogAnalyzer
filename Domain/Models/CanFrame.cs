using EvUdsAnalyzer.Domain.Enums;

namespace EvUdsAnalyzer.Domain.Models;

public sealed class CanFrame
{
    public int LineNumber { get; init; }
    public double TimestampMs { get; init; }
    public int? Bus { get; init; }
    public string CanId { get; init; } = "";
    public uint CanIdValue { get; init; }
    public bool IsRx { get; init; }
    public byte[] Data { get; init; } = [];
    public string RawType { get; init; } = "";
    public IsoTpFrameType IsoTpFrameType { get; init; } = IsoTpFrameType.Unknown;
    public string Direction => IsRx ? "Rx" : "Tx";
    public string BusDisplay => Bus?.ToString() ?? "-";
    public string DataHex => string.Join(" ", Data.Select(b => b.ToString("X2")));
    public string IsoTpFrameTypeDisplay => IsoTpFrameType switch
    {
        IsoTpFrameType.SingleFrame => "SF",
        IsoTpFrameType.FirstFrame => "FF",
        IsoTpFrameType.ConsecutiveFrame => "CF",
        IsoTpFrameType.FlowControl => "FC",
        _ => "-"
    };
}
