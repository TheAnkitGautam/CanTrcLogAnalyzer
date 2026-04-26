using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;

namespace EvUdsAnalyzer.Application.Services;

public sealed class LogAnalysisService(
    ITrcParser parser,
    IIsoTpReassembler reassembler,
    IUdsDecoder decoder,
    ITransactionMatcher matcher,
    IEcuChannelResolver channelResolver,
    DiagnosticAnalyzer diagnosticAnalyzer) : ILogAnalysisService
{
    public async Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken)
    {
        var frames = await parser.ParseAsync(filePath, cancellationToken);
        foreach (var frame in frames)
        {
            _ = channelResolver.ResolveForFrame(frame);
        }

        var reassembly = reassembler.Reassemble(frames);
        foreach (var message in reassembly.Messages)
        {
            message.Decoded = decoder.Decode(message);
            message.Channel = channelResolver.ResolveForMessage(message);
        }

        var transactions = matcher.Match(reassembly.Messages);
        var issues = diagnosticAnalyzer.Analyze(new DiagnosticContext
        {
            Frames = frames,
            Messages = reassembly.Messages,
            Transactions = transactions,
            ExistingIssues = reassembly.Issues
        });

        var summary = BuildSummary(frames, reassembly.Messages, transactions, issues);
        return new AnalysisResult
        {
            Frames = frames,
            Messages = reassembly.Messages,
            Transactions = transactions,
            Issues = issues,
            Summary = summary
        };
    }

    private static AnalysisSummary BuildSummary(
        IReadOnlyList<CanFrame> frames,
        IReadOnlyList<IsoTpMessage> messages,
        IReadOnlyList<UdsTransaction> transactions,
        IReadOnlyList<DiagnosticIssue> issues)
    {
        var ecuBreakdown = transactions
            .GroupBy(t => t.Channel.DisplayName)
            .Select(group => new EcuSummary
            {
                Channel = group.Key,
                Transactions = group.Count(),
                Messages = messages.Count(m => m.Channel?.DisplayName == group.Key),
                Frames = frames.Count(f => group.Key.Contains(f.CanId, StringComparison.OrdinalIgnoreCase)),
                Issues = issues.Count(i => i.Channel == group.Key || group.Key.Contains(i.Channel, StringComparison.OrdinalIgnoreCase))
            })
            .OrderBy(e => e.Channel)
            .ToArray();

        return new AnalysisSummary
        {
            TotalFrames = frames.Count,
            TotalMessages = messages.Count(m => m.Status != IsoTpMessageStatus.FlowControl),
            TotalTransactions = transactions.Count,
            TotalErrors = issues.Count(i => i.Severity is IssueSeverity.Error or IssueSeverity.Critical),
            EcuBreakdown = ecuBreakdown
        };
    }
}
