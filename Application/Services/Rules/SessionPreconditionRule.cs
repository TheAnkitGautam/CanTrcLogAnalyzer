using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services.Rules;

public sealed class SessionPreconditionRule : IDiagnosticRule
{
    private static readonly HashSet<byte> SessionSensitiveServices = [0x22, 0x27, 0x2E, 0x31, 0x34, 0x36, 0x37];

    public IEnumerable<DiagnosticIssue> Evaluate(DiagnosticContext context)
    {
        var sessionInitialized = new HashSet<string>();

        foreach (var transaction in context.Transactions.OrderBy(t => t.StartTimeMs))
        {
            var request = transaction.Request;
            if (request?.Decoded is null)
            {
                continue;
            }

            if (request.Decoded.ServiceId == 0x10 && transaction.Status == UdsTransactionStatus.Success)
            {
                sessionInitialized.Add(transaction.Channel.Key);
                continue;
            }

            if (SessionSensitiveServices.Contains(request.Decoded.ServiceId) && !sessionInitialized.Contains(transaction.Channel.Key))
            {
                yield return new DiagnosticIssue
                {
                    Title = "Service used before session initialization",
                    Description = $"Service 0x{request.Decoded.ServiceId:X2} {request.Decoded.ServiceName} was requested before a successful DiagnosticSessionControl was observed on this channel.",
                    Severity = IssueSeverity.Info,
                    LineNumber = request.StartLineNumber,
                    TimestampMs = request.StartTimeMs,
                    Channel = transaction.Channel.DisplayName
                };
            }
        }
    }
}
