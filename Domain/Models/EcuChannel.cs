namespace EvUdsAnalyzer.Domain.Models;

public sealed record EcuChannel(string RequestId, string ResponseId, int? Bus)
{
    public string Key => $"{Bus?.ToString() ?? "-"}:{RequestId}<->{ResponseId}";
    public string DisplayName => ResponseId == "*" ? $"{RequestId} -> functional" : $"{RequestId} <-> {ResponseId}";
}
