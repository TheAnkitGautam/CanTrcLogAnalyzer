using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Interfaces;

public interface IReportExportService
{
    Task ExportHtmlAsync(string filePath, DashboardSummary dashboard, IReadOnlyList<DiagnosticIssue> issues, IReadOnlyList<UdsTransaction> transactions, CancellationToken cancellationToken);
    Task ExportIssuesCsvAsync(string filePath, IReadOnlyList<DiagnosticIssue> issues, CancellationToken cancellationToken);
    Task ExportTransactionsCsvAsync(string filePath, IReadOnlyList<UdsTransaction> transactions, CancellationToken cancellationToken);
}
