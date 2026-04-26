using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services.Rules;

public sealed class SecurityAccessInsightRule : IDiagnosticRule
{
    public IEnumerable<DiagnosticIssue> Evaluate(DiagnosticContext context)
    {
        var denied = context.Transactions
            .Where(t => t.Response?.Decoded?.NegativeResponseCode == 0x33)
            .GroupBy(t => t.Channel.DisplayName)
            .Where(group => group.Count() >= 2);

        foreach (var group in denied)
        {
            var first = group.First();
            yield return new DiagnosticIssue
            {
                Title = "Security access failed repeatedly",
                Description = "Repeated NRC 0x33 indicates security access is denied. Check seed/key algorithm, required session, attempt counters, and security delay timing.",
                Severity = IssueSeverity.Error,
                LineNumber = first.Response?.StartLineNumber ?? first.Request?.StartLineNumber ?? 0,
                TimestampMs = first.StartTimeMs,
                Channel = group.Key
            };
        }
    }
}
