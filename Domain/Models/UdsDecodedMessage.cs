namespace EvUdsAnalyzer.Domain.Models;

public sealed class UdsDecodedMessage
{
    public byte ServiceId { get; init; }
    public byte? OriginalServiceId { get; init; }
    public byte? SubFunction { get; init; }
    public bool IsPositiveResponse { get; init; }
    public bool IsNegativeResponse { get; init; }
    public byte? NegativeResponseCode { get; init; }
    public string ServiceName { get; init; } = "";
    public string ServiceLongName { get; init; } = "";
    public string ServiceCategory { get; init; } = "";
    public string ServicePurpose { get; init; } = "";
    public string MessageKind { get; init; } = "";
    public string SubFunctionName { get; init; } = "";
    public string SubFunctionMeaning { get; init; } = "";
    public string ParameterSummary { get; init; } = "";
    public string Description { get; init; } = "";
    public string NrcMeaning { get; init; } = "";
    public string NrcCategory { get; init; } = "";
    public string SuggestedAction { get; init; } = "";
}
