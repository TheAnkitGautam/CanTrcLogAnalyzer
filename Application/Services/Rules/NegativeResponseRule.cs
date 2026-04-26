using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services.Rules;

public sealed class NegativeResponseRule : IDiagnosticRule
{
    public IEnumerable<DiagnosticIssue> Evaluate(DiagnosticContext context)
    {
        foreach (var transaction in context.Transactions.Where(t => t.Status == UdsTransactionStatus.NegativeResponse && t.Response?.Decoded is not null))
        {
            var decoded = transaction.Response!.Decoded!;
            var nrc = decoded.NegativeResponseCode ?? 0;
            yield return new DiagnosticIssue
            {
                Title = $"Negative response NRC 0x{nrc:X2}",
                Description = $"{decoded.NrcMeaning}. Suggested action: {decoded.SuggestedAction}",
                Severity = nrc is 0x33 or 0x35 or 0x36 ? IssueSeverity.Error : IssueSeverity.Warning,
                LineNumber = transaction.Response.LineNumberOrStart(),
                TimestampMs = transaction.Response.StartTimeMs,
                Channel = transaction.Channel.DisplayName
            };
        }
    }
}
