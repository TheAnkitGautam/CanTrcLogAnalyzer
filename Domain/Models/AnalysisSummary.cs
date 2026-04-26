namespace EvUdsAnalyzer.Domain.Models;

public sealed class AnalysisSummary
{
    public int TotalFrames { get; init; }
    public int TotalMessages { get; init; }
    public int TotalTransactions { get; init; }
    public int TotalErrors { get; init; }
    public IReadOnlyList<EcuSummary> EcuBreakdown { get; init; } = [];
}

public sealed class EcuSummary
{
    public string Channel { get; init; } = "";
    public int Frames { get; init; }
    public int Messages { get; init; }
    public int Transactions { get; init; }
    public int Issues { get; init; }
}
