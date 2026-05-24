using EvUdsAnalyzer.Domain.Enums;

namespace EvUdsAnalyzer.Domain.Models;

public sealed class IsoTpMessage
{
    public string CanId { get; init; } = "";
    public uint CanIdValue { get; init; }
    public bool IsRx { get; init; }
    public int? Bus { get; init; }
    public double StartTimeMs { get; init; }
    public double EndTimeMs { get; init; }
    public int StartLineNumber { get; init; }
    public int EndLineNumber { get; init; }
    public IsoTpFrameType FrameType { get; init; }
    public IsoTpMessageStatus Status { get; init; }
    public IReadOnlyList<byte> Payload { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public EcuChannel? Channel { get; set; }
    public UdsDecodedMessage? Decoded { get; set; }
    public string Direction => IsRx ? "Rx" : "Tx";
    public string PayloadHex => string.Join(" ", Payload.Select(b => b.ToString("X2")));
    public string ChannelDisplay => Channel?.DisplayName ?? "-";
    public string ServiceDisplay => Decoded is null ? "-" : $"0x{Decoded.ServiceId:X2} {Decoded.ServiceName}";
    public string ServiceDetailsDisplay => Decoded is null
        ? "-"
        : string.Join(Environment.NewLine, new[]
        {
            $"{Decoded.MessageKind}: {Decoded.ServiceLongName}",
            string.IsNullOrWhiteSpace(Decoded.SubFunctionName) ? "" : $"Sub-function: 0x{Decoded.SubFunction:X2} {Decoded.SubFunctionName}",
            string.IsNullOrWhiteSpace(Decoded.SubFunctionMeaning) ? "" : $"Meaning: {Decoded.SubFunctionMeaning}",
            string.IsNullOrWhiteSpace(Decoded.ParameterSummary) ? "" : $"Parameters: {Decoded.ParameterSummary}",
            string.IsNullOrWhiteSpace(Decoded.NrcMeaning) ? "" : $"NRC: {Decoded.NrcMeaning} ({Decoded.NrcCategory})",
            string.IsNullOrWhiteSpace(Decoded.ServicePurpose) ? "" : $"Purpose: {Decoded.ServicePurpose}"
        }.Where(line => !string.IsNullOrWhiteSpace(line)));
}
