namespace EvUdsAnalyzer.Domain.Models;

public sealed class AnalysisResult
{
    public IReadOnlyList<CanFrame> Frames { get; init; } = [];
    public IReadOnlyList<IsoTpMessage> Messages { get; init; } = [];
    public IReadOnlyList<UdsTransaction> Transactions { get; init; } = [];
    public IReadOnlyList<DiagnosticIssue> Issues { get; init; } = [];
    public AnalysisSummary Summary { get; init; } = new();
}
