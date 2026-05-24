namespace EvUdsAnalyzer.Domain.Models;

public sealed record UdsServiceInfo(
    byte ServiceId,
    string Name,
    string LongName,
    string Category,
    string Purpose,
    bool IsStandardized = true,
    bool AllowsSuppressPositiveResponse = false);

public sealed record UdsSubFunctionInfo(
    byte ServiceId,
    byte SubFunction,
    string Name,
    string Meaning);
