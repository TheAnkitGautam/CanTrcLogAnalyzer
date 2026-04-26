using EvUdsAnalyzer.Domain.Enums;

namespace EvUdsAnalyzer.Domain.Models;

public sealed class DashboardSummary
{
    public HealthVerdict Verdict { get; init; } = HealthVerdict.Healthy;
    public string VerdictDisplay => Verdict.ToString();
    public string Headline { get; init; } = "Load a TRC file to begin.";
    public string PlainSummary { get; init; } = "The analyzer will summarize ECU communication health after a file is loaded.";
    public int TotalFrames { get; init; }
    public int TotalMessages { get; init; }
    public int TotalTransactions { get; init; }
    public int TimeoutCount { get; init; }
    public int NegativeResponseCount { get; init; }
    public int RetryPatternCount { get; init; }
    public int IsoTpErrorCount { get; init; }
    public int CriticalIssueCount { get; init; }
    public int ErrorIssueCount { get; init; }
    public int WarningIssueCount { get; init; }
    public IReadOnlyList<GuidedFinding> TopFindings { get; init; } = [];
    public IReadOnlyList<EcuHealthCard> EcuHealthCards { get; init; } = [];
}

public sealed class GuidedFinding
{
    public string Title { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Channel { get; init; } = "";
    public double TimestampMs { get; init; }
    public int LineNumber { get; init; }
    public string PlainMeaning { get; init; } = "";
    public string LikelyCause { get; init; } = "";
    public string WhyDetected { get; init; } = "";
    public string Evidence { get; init; } = "";
    public string TechnicalDetails { get; init; } = "";
    public IReadOnlyList<string> RecommendedActions { get; init; } = [];
    public string PrimaryAction => RecommendedActions.FirstOrDefault() ?? "Review the related transaction and ECU channel.";
    public string RecommendedActionsDisplay => RecommendedActions.Count == 0 ? "-" : string.Join(Environment.NewLine, RecommendedActions.Select(a => $"- {a}"));
}

public sealed class EcuHealthCard
{
    public string Channel { get; init; } = "";
    public HealthVerdict Verdict { get; init; }
    public string VerdictDisplay => Verdict.ToString();
    public string Summary { get; init; } = "";
    public int Frames { get; init; }
    public int Messages { get; init; }
    public int Transactions { get; init; }
    public int Issues { get; init; }
    public int Errors { get; init; }
}

public sealed class GlossaryTerm
{
    public string Term { get; init; } = "";
    public string PlainMeaning { get; init; } = "";
    public string TechnicalMeaning { get; init; } = "";
}
