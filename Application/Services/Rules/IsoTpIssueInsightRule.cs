using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services.Rules;

public sealed class IsoTpIssueInsightRule : IDiagnosticRule
{
    public IEnumerable<DiagnosticIssue> Evaluate(DiagnosticContext context)
    {
        foreach (var group in context.Messages
                     .Where(m => m.Status is IsoTpMessageStatus.Error or IsoTpMessageStatus.Incomplete)
                     .GroupBy(m => m.Channel?.DisplayName ?? m.CanId)
                     .Where(g => g.Count() >= 2))
        {
            var first = group.First();
            yield return new DiagnosticIssue
            {
                Title = "Repeated ISO-TP reassembly errors",
                Description = "Multiple incomplete or invalid ISO-TP messages were detected. Possible frame loss, sequence corruption, trace truncation, or bus congestion.",
                Severity = IssueSeverity.Error,
                LineNumber = first.StartLineNumber,
                TimestampMs = first.StartTimeMs,
                Channel = group.Key
            };
        }
    }
}
