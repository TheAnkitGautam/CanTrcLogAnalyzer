using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services.Rules;

public sealed class RetryPatternRule : IDiagnosticRule
{
    public IEnumerable<DiagnosticIssue> Evaluate(DiagnosticContext context)
    {
        var repeated = context.Transactions
            .Where(t => t.Request is not null)
            .GroupBy(t => $"{t.Channel.Key}:{t.Request!.PayloadHex}")
            .Where(group => group.Count() >= 3);

        foreach (var group in repeated)
        {
            var first = group.First();
            yield return new DiagnosticIssue
            {
                Title = "Repeated request pattern",
                Description = $"The same request was sent {group.Count()} times on {first.Channel.DisplayName}. This usually indicates retries after timeout, busy response, or failed preconditions.",
                Severity = IssueSeverity.Warning,
                LineNumber = first.Request?.StartLineNumber ?? 0,
                TimestampMs = first.StartTimeMs,
                Channel = first.Channel.DisplayName
            };
        }
    }
}
