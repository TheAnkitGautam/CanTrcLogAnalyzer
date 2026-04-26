using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public sealed class ExplanationService : IExplanationService
{
    public void Enrich(IReadOnlyList<DiagnosticIssue> issues, IReadOnlyList<UdsTransaction> transactions)
    {
        foreach (var issue in issues)
        {
            EnrichIssue(issue);
        }

        foreach (var transaction in transactions)
        {
            EnrichTransaction(transaction);
        }
    }

    public DashboardSummary BuildDashboard(
        AnalysisSummary summary,
        IReadOnlyList<DiagnosticIssue> issues,
        IReadOnlyList<UdsTransaction> transactions,
        IReadOnlyList<IsoTpMessage> messages)
    {
        var verdict = GetVerdict(issues);
        var topFindings = issues
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.TimestampMs)
            .Take(5)
            .Select(ToGuidedFinding)
            .ToArray();

        var negativeResponses = transactions.Count(t => t.Status == UdsTransactionStatus.NegativeResponse);
        var timeouts = transactions.Count(t => t.Status == UdsTransactionStatus.Timeout);
        var retries = issues.Count(i => i.Title.Contains("Repeated request", StringComparison.OrdinalIgnoreCase));
        var isoTpErrors = messages.Count(m => m.Status is IsoTpMessageStatus.Error or IsoTpMessageStatus.Incomplete);

        return new DashboardSummary
        {
            Verdict = verdict,
            Headline = BuildHeadline(verdict, issues),
            PlainSummary = BuildPlainSummary(summary, issues, transactions),
            TotalFrames = summary.TotalFrames,
            TotalMessages = summary.TotalMessages,
            TotalTransactions = summary.TotalTransactions,
            TimeoutCount = timeouts,
            NegativeResponseCount = negativeResponses,
            RetryPatternCount = retries,
            IsoTpErrorCount = isoTpErrors,
            CriticalIssueCount = issues.Count(i => i.Severity == IssueSeverity.Critical),
            ErrorIssueCount = issues.Count(i => i.Severity == IssueSeverity.Error),
            WarningIssueCount = issues.Count(i => i.Severity == IssueSeverity.Warning),
            TopFindings = topFindings,
            EcuHealthCards = BuildEcuCards(summary, issues)
        };
    }

    public IReadOnlyList<GlossaryTerm> GetGlossary() =>
    [
        new() { Term = "ECU", PlainMeaning = "A vehicle controller, such as battery, motor, ABS, or body controller.", TechnicalMeaning = "Electronic Control Unit communicating on the CAN bus." },
        new() { Term = "UDS", PlainMeaning = "The diagnostic language used to ask ECUs for data or actions.", TechnicalMeaning = "Unified Diagnostic Services, ISO 14229." },
        new() { Term = "ISO-TP", PlainMeaning = "The transport method that splits long diagnostic messages across multiple CAN frames.", TechnicalMeaning = "ISO 15765-2 segmentation using SF, FF, CF, and FC frames." },
        new() { Term = "NRC", PlainMeaning = "A negative response code explaining why an ECU rejected a request.", TechnicalMeaning = "UDS response 0x7F followed by original SID and NRC byte." },
        new() { Term = "Session", PlainMeaning = "A diagnostic mode that unlocks which services an ECU will accept.", TechnicalMeaning = "UDS service 0x10 DiagnosticSessionControl." },
        new() { Term = "Security Access", PlainMeaning = "A seed/key unlock step required before protected operations.", TechnicalMeaning = "UDS service 0x27 SecurityAccess." },
        new() { Term = "Timeout", PlainMeaning = "The tester sent a request, but no matching ECU response was seen in time.", TechnicalMeaning = "No matched response inside the configured transaction window." },
        new() { Term = "Retry", PlainMeaning = "The same request was sent repeatedly, often because something failed.", TechnicalMeaning = "Repeated request payload on the same channel." },
        new() { Term = "Functional Addressing", PlainMeaning = "A broadcast diagnostic request where multiple ECUs may answer.", TechnicalMeaning = "Common request ID 0x7DF with physical ECU responses such as 0x7E8." },
        new() { Term = "Flow Control", PlainMeaning = "A receiver tells the sender it may continue a long message.", TechnicalMeaning = "ISO-TP FC frame, PCI nibble 0x3." }
    ];

    private static void EnrichIssue(DiagnosticIssue issue)
    {
        issue.UserFriendlySummary = BuildIssuePlainMeaning(issue);
        issue.LikelyCause = BuildLikelyCause(issue);
        issue.WhyDetected = BuildWhyDetected(issue);
        issue.Evidence = $"Line {issue.LineNumber}, time {issue.TimestampMs:F3} ms, channel {issue.Channel}.";
        issue.TechnicalDetails = issue.Description;
        issue.RecommendedActions = BuildActions(issue);
    }

    private static void EnrichTransaction(UdsTransaction transaction)
    {
        transaction.UserFriendlySummary = transaction.Status switch
        {
            UdsTransactionStatus.Success => "The ECU answered the diagnostic request successfully.",
            UdsTransactionStatus.NegativeResponse => "The ECU answered, but rejected the request.",
            UdsTransactionStatus.Timeout => "The request did not receive a matching ECU response in time.",
            UdsTransactionStatus.UnmatchedResponse => "A response was seen but no matching request was found earlier in the log.",
            UdsTransactionStatus.IsoTpError => "This transaction is affected by an ISO-TP reconstruction problem.",
            _ => "Review this transaction for diagnostic context."
        };
        transaction.LikelyCause = transaction.Status switch
        {
            UdsTransactionStatus.NegativeResponse => transaction.Response?.Decoded?.NrcMeaning ?? "ECU rejected the request.",
            UdsTransactionStatus.Timeout => "ECU offline, wrong addressing, bus issue, or response outside the log window.",
            UdsTransactionStatus.IsoTpError => "Missing or out-of-order CAN frames during segmented transfer.",
            UdsTransactionStatus.UnmatchedResponse => "Trace started late, request filtered out, or channel mapping mismatch.",
            _ => "No fault indicated by this transaction."
        };
        transaction.WhyDetected = $"Transaction status is {transaction.StatusDisplay} on {transaction.Channel.DisplayName}.";
        transaction.Evidence = $"Request: {transaction.RequestDisplay}{Environment.NewLine}Response: {transaction.ResponseDisplay}";
        transaction.TechnicalDetails = $"Service: {transaction.ServiceDisplay}; latency: {(transaction.LatencyMs?.ToString("F3") ?? "-")} ms.";
        transaction.RecommendedActions = transaction.Status switch
        {
            UdsTransactionStatus.NegativeResponse => ["Check the NRC meaning and ECU preconditions.", "Confirm session and security access before retrying.", "Validate DID/RID/sub-function values."],
            UdsTransactionStatus.Timeout => ["Confirm the ECU is powered and on the expected CAN channel.", "Check request/response CAN IDs.", "Inspect bus load or trace loss around the timestamp."],
            UdsTransactionStatus.IsoTpError => ["Check for missing consecutive frames.", "Inspect bus load and logger drop counters.", "Repeat the capture if the trace may be truncated."],
            _ => ["Use this transaction as supporting evidence when reviewing related findings."]
        };
    }

    private static GuidedFinding ToGuidedFinding(DiagnosticIssue issue) => new()
    {
        Title = issue.Title,
        Severity = issue.SeverityDisplay,
        Channel = issue.Channel,
        TimestampMs = issue.TimestampMs,
        LineNumber = issue.LineNumber,
        PlainMeaning = issue.UserFriendlySummary,
        LikelyCause = issue.LikelyCause,
        WhyDetected = issue.WhyDetected,
        Evidence = issue.Evidence,
        TechnicalDetails = issue.TechnicalDetails,
        RecommendedActions = issue.RecommendedActions
    };

    private static IReadOnlyList<EcuHealthCard> BuildEcuCards(AnalysisSummary summary, IReadOnlyList<DiagnosticIssue> issues) =>
        summary.EcuBreakdown
            .Select(ecu =>
            {
                var ecuIssues = issues.Where(i => i.Channel == ecu.Channel).ToArray();
                var verdict = GetVerdict(ecuIssues);
                return new EcuHealthCard
                {
                    Channel = ecu.Channel,
                    Verdict = verdict,
                    Frames = ecu.Frames,
                    Messages = ecu.Messages,
                    Transactions = ecu.Transactions,
                    Issues = ecuIssues.Length,
                    Errors = ecuIssues.Count(i => i.Severity is IssueSeverity.Error or IssueSeverity.Critical),
                    Summary = ecuIssues.Length == 0 ? "No issues detected on this channel." : $"{ecuIssues.Length} issue(s), top: {ecuIssues.OrderByDescending(i => i.Severity).First().Title}"
                };
            })
            .OrderByDescending(e => e.Verdict)
            .ThenBy(e => e.Channel)
            .ToArray();

    private static HealthVerdict GetVerdict(IEnumerable<DiagnosticIssue> issues)
    {
        var list = issues.ToArray();
        if (list.Any(i => i.Severity == IssueSeverity.Critical))
        {
            return HealthVerdict.Critical;
        }

        if (list.Any(i => i.Severity == IssueSeverity.Error))
        {
            return HealthVerdict.Errors;
        }

        if (list.Any(i => i.Severity == IssueSeverity.Warning))
        {
            return HealthVerdict.Warnings;
        }

        return HealthVerdict.Healthy;
    }

    private static string BuildHeadline(HealthVerdict verdict, IReadOnlyList<DiagnosticIssue> issues) => verdict switch
    {
        HealthVerdict.Healthy => "No diagnostic communication problems detected.",
        HealthVerdict.Warnings => "The log is mostly healthy, but a few items need review.",
        HealthVerdict.Errors => "Communication or diagnostic errors were detected.",
        HealthVerdict.Critical => "Critical diagnostic communication problems were detected.",
        _ => $"{issues.Count} finding(s) detected."
    };

    private static string BuildPlainSummary(AnalysisSummary summary, IReadOnlyList<DiagnosticIssue> issues, IReadOnlyList<UdsTransaction> transactions)
    {
        if (summary.TotalFrames == 0)
        {
            return "Load a PCAN TRC file to see a guided explanation of ECU communication.";
        }

        var timeoutCount = transactions.Count(t => t.Status == UdsTransactionStatus.Timeout);
        var nrcCount = transactions.Count(t => t.Status == UdsTransactionStatus.NegativeResponse);
        return $"Analyzed {summary.TotalFrames:N0} frames and {summary.TotalTransactions:N0} request/response transactions. Found {issues.Count:N0} finding(s), including {timeoutCount:N0} timeout(s) and {nrcCount:N0} negative response(s).";
    }

    private static string BuildIssuePlainMeaning(DiagnosticIssue issue)
    {
        var title = issue.Title.ToLowerInvariant();
        if (title.Contains("negative response"))
        {
            return "The ECU received the request but rejected it.";
        }

        if (title.Contains("timeout"))
        {
            return "The tester expected an ECU answer, but none was seen in time.";
        }

        if (title.Contains("iso-tp") || title.Contains("sequence") || title.Contains("incomplete"))
        {
            return "A long diagnostic message could not be reconstructed cleanly.";
        }

        if (title.Contains("repeated"))
        {
            return "The same request was sent several times, suggesting retries or failed preconditions.";
        }

        if (title.Contains("session"))
        {
            return "A diagnostic service may have been used before the ECU was placed in the required session.";
        }

        return issue.Description;
    }

    private static string BuildLikelyCause(DiagnosticIssue issue)
    {
        var text = $"{issue.Title} {issue.Description}".ToLowerInvariant();
        if (text.Contains("0x33") || text.Contains("security"))
        {
            return "Security access was not unlocked, the key was wrong, or the ECU is enforcing an attempt/delay policy.";
        }

        if (text.Contains("timeout"))
        {
            return "ECU offline, wrong CAN IDs, bus congestion, or lost response frames.";
        }

        if (text.Contains("iso-tp") || text.Contains("sequence") || text.Contains("incomplete"))
        {
            return "Missing, reordered, or truncated CAN frames during a multi-frame message.";
        }

        if (text.Contains("session"))
        {
            return "The ECU may require DiagnosticSessionControl before this service.";
        }

        if (text.Contains("conditions not correct") || text.Contains("0x22"))
        {
            return "The ECU state or session does not satisfy the request preconditions.";
        }

        return "Review the evidence and related transaction for the most likely root cause.";
    }

    private static string BuildWhyDetected(DiagnosticIssue issue) =>
        $"Rule engine produced a {issue.SeverityDisplay} finding: {issue.Title}.";

    private static IReadOnlyList<string> BuildActions(DiagnosticIssue issue)
    {
        var text = $"{issue.Title} {issue.Description}".ToLowerInvariant();
        if (text.Contains("security") || text.Contains("0x33") || text.Contains("0x35") || text.Contains("0x36"))
        {
            return ["Confirm the ECU is in the required diagnostic session.", "Verify seed/key calculation and key freshness.", "Respect security attempt counters and delay timers."];
        }

        if (text.Contains("timeout"))
        {
            return ["Check ECU power and network presence.", "Verify request and response CAN IDs.", "Inspect bus load or logger frame loss near the timestamp."];
        }

        if (text.Contains("iso-tp") || text.Contains("sequence") || text.Contains("incomplete"))
        {
            return ["Check for missing Consecutive Frames.", "Repeat capture if the trace may be truncated.", "Investigate bus congestion or logger overflow."];
        }

        if (text.Contains("session"))
        {
            return ["Send DiagnosticSessionControl before this service.", "Confirm the ECU accepted the session change.", "Retry the request after session setup."];
        }

        return ["Open the related transaction.", "Review request/response payload and ECU channel.", "Compare against the ECU diagnostic specification."];
    }
}
