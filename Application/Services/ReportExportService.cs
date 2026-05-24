using System.Globalization;
using System.Net;
using System.Text;
using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public sealed class ReportExportService : IReportExportService
{
    public async Task ExportHtmlAsync(string filePath, DashboardSummary dashboard, IReadOnlyList<DiagnosticIssue> issues, IReadOnlyList<UdsTransaction> transactions, CancellationToken cancellationToken)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>EV UDS Diagnostic Report</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:32px;color:#172033}table{border-collapse:collapse;width:100%;margin:16px 0}th,td{border:1px solid #d8dee9;padding:8px;vertical-align:top}th{background:#eef2f7}.badge{display:inline-block;padding:6px 10px;border-radius:4px;background:#172033;color:white}.muted{color:#64748b}.finding{border:1px solid #d8dee9;padding:14px;margin:12px 0}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine($"<h1>EV UDS Diagnostic Report</h1><p class=\"badge\">{Encode(dashboard.VerdictDisplay)}</p>");
        html.AppendLine($"<h2>{Encode(dashboard.Headline)}</h2><p>{Encode(dashboard.PlainSummary)}</p>");
        html.AppendLine("<h2>Counters</h2><table><tr><th>Frames</th><th>Messages</th><th>Transactions</th><th>Timeouts</th><th>NRCs</th><th>ISO-TP Errors</th></tr>");
        html.AppendLine(CultureInfo.InvariantCulture, $"<tr><td>{dashboard.TotalFrames}</td><td>{dashboard.TotalMessages}</td><td>{dashboard.TotalTransactions}</td><td>{dashboard.TimeoutCount}</td><td>{dashboard.NegativeResponseCount}</td><td>{dashboard.IsoTpErrorCount}</td></tr></table>");
        html.AppendLine("<h2>Top Findings</h2>");
        foreach (var finding in dashboard.TopFindings)
        {
            html.AppendLine("<div class=\"finding\">");
            html.AppendLine($"<h3>{Encode(finding.Title)} <span class=\"muted\">{Encode(finding.Severity)}</span></h3>");
            html.AppendLine($"<p><strong>Meaning:</strong> {Encode(finding.PlainMeaning)}</p>");
            html.AppendLine($"<p><strong>Likely cause:</strong> {Encode(finding.LikelyCause)}</p>");
            html.AppendLine($"<p><strong>Evidence:</strong> {Encode(finding.Evidence)}</p>");
            html.AppendLine($"<p><strong>Next action:</strong> {Encode(finding.PrimaryAction)}</p>");
            html.AppendLine("</div>");
        }

        html.AppendLine("<h2>ECU Breakdown</h2><table><tr><th>Channel</th><th>Verdict</th><th>Frames</th><th>Messages</th><th>Transactions</th><th>Issues</th><th>Summary</th></tr>");
        foreach (var ecu in dashboard.EcuHealthCards)
        {
            html.AppendLine(CultureInfo.InvariantCulture, $"<tr><td>{Encode(ecu.Channel)}</td><td>{Encode(ecu.VerdictDisplay)}</td><td>{ecu.Frames}</td><td>{ecu.Messages}</td><td>{ecu.Transactions}</td><td>{ecu.Issues}</td><td>{Encode(ecu.Summary)}</td></tr>");
        }
        html.AppendLine("</table>");

        html.AppendLine("<h2>Issues</h2><table><tr><th>Severity</th><th>Channel</th><th>Line</th><th>Title</th><th>Meaning</th><th>Action</th></tr>");
        foreach (var issue in issues)
        {
            html.AppendLine($"<tr><td>{Encode(issue.SeverityDisplay)}</td><td>{Encode(issue.Channel)}</td><td>{issue.LineNumber}</td><td>{Encode(issue.Title)}</td><td>{Encode(issue.UserFriendlySummary)}</td><td>{Encode(issue.RecommendedActions.FirstOrDefault() ?? "")}</td></tr>");
        }
        html.AppendLine("</table>");

        html.AppendLine("<h2>Transactions</h2><table><tr><th>Status</th><th>Channel</th><th>Time</th><th>Service</th><th>Meaning</th><th>Request</th><th>Response</th></tr>");
        foreach (var transaction in transactions)
        {
            html.AppendLine(CultureInfo.InvariantCulture, $"<tr><td>{Encode(transaction.StatusDisplay)}</td><td>{Encode(transaction.Channel.DisplayName)}</td><td>{transaction.StartTimeMs:F3}</td><td>{Encode(transaction.ServiceDisplay)}</td><td>{Encode(transaction.UserFriendlySummary)}</td><td>{Encode(transaction.RequestDisplay)}</td><td>{Encode(transaction.ResponseDisplay)}</td></tr>");
        }
        html.AppendLine("</table>");
        html.AppendLine("</body></html>");

        await System.IO.File.WriteAllTextAsync(filePath, html.ToString(), Encoding.UTF8, cancellationToken);
    }

    public Task ExportIssuesCsvAsync(string filePath, IReadOnlyList<DiagnosticIssue> issues, CancellationToken cancellationToken)
    {
        var lines = new List<string> { "Severity,Channel,Line,TimestampMs,Title,PlainMeaning,LikelyCause,RecommendedActions,Evidence,TechnicalDetails" };
        lines.AddRange(issues.Select(i => string.Join(",", Csv(i.SeverityDisplay), Csv(i.Channel), i.LineNumber.ToString(CultureInfo.InvariantCulture), i.TimestampMs.ToString("F3", CultureInfo.InvariantCulture), Csv(i.Title), Csv(i.UserFriendlySummary), Csv(i.LikelyCause), Csv(string.Join(" | ", i.RecommendedActions)), Csv(i.Evidence), Csv(i.TechnicalDetails))));
        return System.IO.File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8, cancellationToken);
    }

    public Task ExportTransactionsCsvAsync(string filePath, IReadOnlyList<UdsTransaction> transactions, CancellationToken cancellationToken)
    {
        var lines = new List<string> { "Status,Channel,TimeMs,LatencyMs,Service,Category,Purpose,Parameters,NRC,NrcCategory,Request,Response,PlainMeaning,LikelyCause,RecommendedActions" };
        lines.AddRange(transactions.Select(t =>
        {
            var decoded = t.Request?.Decoded ?? t.Response?.Decoded;
            var nrc = t.Response?.Decoded?.NegativeResponseCode;
            return string.Join(",", Csv(t.StatusDisplay), Csv(t.Channel.DisplayName), t.StartTimeMs.ToString("F3", CultureInfo.InvariantCulture), t.LatencyMs?.ToString("F3", CultureInfo.InvariantCulture) ?? "", Csv(t.ServiceDisplay), Csv(decoded?.ServiceCategory ?? ""), Csv(decoded?.ServicePurpose ?? ""), Csv(decoded?.ParameterSummary ?? ""), Csv(nrc.HasValue ? $"0x{nrc.Value:X2} {t.Response?.Decoded?.NrcMeaning}" : ""), Csv(t.Response?.Decoded?.NrcCategory ?? ""), Csv(t.RequestDisplay), Csv(t.ResponseDisplay), Csv(t.UserFriendlySummary), Csv(t.LikelyCause), Csv(string.Join(" | ", t.RecommendedActions)));
        }));
        return System.IO.File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8, cancellationToken);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Csv(string value)
    {
        value ??= "";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
