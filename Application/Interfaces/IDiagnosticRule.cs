using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface IDiagnosticRule
{
    IEnumerable<DiagnosticIssue> Evaluate(DiagnosticContext context);
}

public sealed class DiagnosticContext
{
    public IReadOnlyList<CanFrame> Frames { get; init; } = [];
    public IReadOnlyList<IsoTpMessage> Messages { get; init; } = [];
    public IReadOnlyList<UdsTransaction> Transactions { get; init; } = [];
    public IReadOnlyList<DiagnosticIssue> ExistingIssues { get; init; } = [];
}
