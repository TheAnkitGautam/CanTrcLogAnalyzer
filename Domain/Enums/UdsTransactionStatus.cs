namespace EvUdsAnalyzer.Domain.Enums;

public enum UdsTransactionStatus
{
    Success,
    NegativeResponse,
    Timeout,
    ResponsePending,
    UnmatchedResponse,
    IsoTpError
}
