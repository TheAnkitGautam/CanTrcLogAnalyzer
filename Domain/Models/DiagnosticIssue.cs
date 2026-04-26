using EvUdsAnalyzer.Domain.Enums;

namespace EvUdsAnalyzer.Domain.Models;

public sealed class DiagnosticIssue
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public IssueSeverity Severity { get; init; }
    public int LineNumber { get; init; }
    public string Channel { get; init; } = "";
    public double TimestampMs { get; init; }
    public string UserFriendlySummary { get; set; } = "";
    public string LikelyCause { get; set; } = "";
    public string WhyDetected { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string TechnicalDetails { get; set; } = "";
    public IReadOnlyList<string> RecommendedActions { get; set; } = [];
    public string RecommendedActionsDisplay => RecommendedActions.Count == 0 ? "-" : string.Join(Environment.NewLine, RecommendedActions.Select(a => $"- {a}"));
    public string SeverityDisplay => Severity.ToString();
}
