namespace EvUdsAnalyzer.Domain.Models;

public sealed class UdsDecodedMessage
{
    public byte ServiceId { get; init; }
    public byte? OriginalServiceId { get; init; }
    public bool IsPositiveResponse { get; init; }
    public bool IsNegativeResponse { get; init; }
    public byte? NegativeResponseCode { get; init; }
    public string ServiceName { get; init; } = "";
    public string Description { get; init; } = "";
    public string NrcMeaning { get; init; } = "";
    public string SuggestedAction { get; init; } = "";
}
