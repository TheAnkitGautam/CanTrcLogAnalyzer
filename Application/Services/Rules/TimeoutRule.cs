using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services.Rules;

public sealed class TimeoutRule : IDiagnosticRule
{
    public IEnumerable<DiagnosticIssue> Evaluate(DiagnosticContext context)
    {
        var timeouts = context.Transactions.Where(t => t.Status == UdsTransactionStatus.Timeout).ToArray();
        foreach (var transaction in timeouts)
        {
            yield return new DiagnosticIssue
            {
                Title = "UDS response timeout",
                Description = "No matching UDS response was found within the configured P2 analysis window. ECU may be busy, offline, addressed incorrectly, or bus traffic may be lost.",
                Severity = IssueSeverity.Error,
                LineNumber = transaction.Request?.StartLineNumber ?? 0,
                TimestampMs = transaction.StartTimeMs,
                Channel = transaction.Channel.DisplayName
            };
        }

        foreach (var group in timeouts.GroupBy(t => t.Channel.DisplayName).Where(g => g.Count() >= 3))
        {
            var first = group.First();
            yield return new DiagnosticIssue
            {
                Title = "Multiple ECU timeouts",
                Description = $"Channel {group.Key} has {group.Count()} timeouts. ECU not responding, incorrect addressing, or a bus-level issue is likely.",
                Severity = IssueSeverity.Critical,
                LineNumber = first.Request?.StartLineNumber ?? 0,
                TimestampMs = first.StartTimeMs,
                Channel = group.Key
            };
        }
    }
}
