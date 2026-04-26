using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Application.Services;
using EvUdsAnalyzer.Application.Services.Rules;
using EvUdsAnalyzer.Infrastructure.Channel;
using EvUdsAnalyzer.Infrastructure.IsoTp;
using EvUdsAnalyzer.Infrastructure.Parsers;
using EvUdsAnalyzer.UI.ViewModels;

namespace EvUdsAnalyzer;

public static class CompositionRoot
{
    public static MainViewModel CreateMainViewModel()
    {
        INrcInterpreter nrcInterpreter = new NrcInterpreter();
        IEcuChannelResolver channelResolver = new EcuChannelResolver();
        ITrcParser parser = new TrcParser();
        IIsoTpReassembler reassembler = new IsoTpReassembler();
        IUdsDecoder decoder = new UdsDecoder(nrcInterpreter);
        ITransactionMatcher matcher = new TransactionMatcher(channelResolver);
        var rules = new IDiagnosticRule[]
        {
            new NegativeResponseRule(),
            new TimeoutRule(),
            new RetryPatternRule(),
            new SessionPreconditionRule(),
            new SecurityAccessInsightRule(),
            new IsoTpIssueInsightRule()
        };

        var diagnosticAnalyzer = new DiagnosticAnalyzer(rules);
        ILogAnalysisService analysisService = new LogAnalysisService(parser, reassembler, decoder, matcher, channelResolver, diagnosticAnalyzer);
        IExplanationService explanationService = new ExplanationService();
        IReportExportService reportExportService = new ReportExportService();
        return new MainViewModel(analysisService, explanationService, reportExportService);
    }
}
