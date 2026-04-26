using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface IExplanationService
{
    void Enrich(IReadOnlyList<DiagnosticIssue> issues, IReadOnlyList<UdsTransaction> transactions);
    DashboardSummary BuildDashboard(
        AnalysisSummary summary,
        IReadOnlyList<DiagnosticIssue> issues,
        IReadOnlyList<UdsTransaction> transactions,
        IReadOnlyList<IsoTpMessage> messages);
    IReadOnlyList<GlossaryTerm> GetGlossary();
}
