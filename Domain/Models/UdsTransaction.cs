using EvUdsAnalyzer.Domain.Enums;

namespace EvUdsAnalyzer.Domain.Models;

public sealed class UdsTransaction
{
    public EcuChannel Channel { get; init; } = new("", "", null);
    public IsoTpMessage? Request { get; init; }
    public IsoTpMessage? Response { get; init; }
    public UdsTransactionStatus Status { get; init; }
    public double StartTimeMs => Request?.StartTimeMs ?? Response?.StartTimeMs ?? 0;
    public double? LatencyMs => Request is null || Response is null ? null : Response.StartTimeMs - Request.StartTimeMs;
    public string StatusDisplay => Status.ToString();
    public string RequestDisplay => Request?.PayloadHex ?? "-";
    public string ResponseDisplay => Response?.PayloadHex ?? "-";
    public string ServiceDisplay => Request?.ServiceDisplay ?? Response?.ServiceDisplay ?? "-";
    public string UserFriendlySummary { get; set; } = "";
    public string LikelyCause { get; set; } = "";
    public string WhyDetected { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string TechnicalDetails { get; set; } = "";
    public IReadOnlyList<string> RecommendedActions { get; set; } = [];
    public string RecommendedActionsDisplay => RecommendedActions.Count == 0 ? "-" : string.Join(Environment.NewLine, RecommendedActions.Select(a => $"- {a}"));
}
