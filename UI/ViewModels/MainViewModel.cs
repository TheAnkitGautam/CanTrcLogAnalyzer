using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using EvUdsAnalyzer.Application.Interfaces;
using EvUdsAnalyzer.Domain.Enums;
using EvUdsAnalyzer.Domain.Models;
using EvUdsAnalyzer.UI.Commands;
using EvUdsAnalyzer.UI.Services;
using Microsoft.Win32;

namespace EvUdsAnalyzer.UI.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ILogAnalysisService _analysisService;
    private readonly IExplanationService _explanationService;
    private readonly IReportExportService _reportExportService;
    private AnalysisResult? _currentResult;
    private string _fileName = "No file loaded";
    private string _statusText = "Ready";
    private bool _isBusy;
    private AnalysisSummary _summary = new();
    private DashboardSummary _dashboard = new();
    private CanFrame? _selectedFrame;
    private DiagnosticIssue? _selectedIssue;
    private UdsTransaction? _selectedTransaction;
    private string _searchText = "";
    private string _severityFilter = "All";
    private string _channelFilter = "All";
    private string _serviceFilter = "All";
    private string _statusFilter = "All";
    private string _messageTypeFilter = "All";
    private string _selectedTheme = "System";
    private int _selectedTabIndex;
    private IsoTpMessage? _selectedMessage;
    private UdsTransaction? _selectedIssueTransaction;
    private IsoTpMessage? _selectedIssueMessage;
    private string _selectedIssueRootCauseMap = "Select an issue to see the related request, response, and raw frame.";

    public MainViewModel(ILogAnalysisService analysisService, IExplanationService explanationService, IReportExportService reportExportService)
    {
        _analysisService = analysisService;
        _explanationService = explanationService;
        _reportExportService = reportExportService;
        LoadFileCommand = new AsyncRelayCommand(LoadFileAsync, () => !IsBusy);
        ExportHtmlCommand = new AsyncRelayCommand(ExportHtmlAsync, CanExport);
        ExportIssuesCsvCommand = new AsyncRelayCommand(ExportIssuesCsvAsync, CanExport);
        ExportTransactionsCsvCommand = new AsyncRelayCommand(ExportTransactionsCsvAsync, CanExport);
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        NavigateToIssueFrameCommand = new RelayCommand(_ => NavigateToIssueFrame(), _ => SelectedFrame is not null);
        NavigateToIssueMessageCommand = new RelayCommand(_ => NavigateToIssueMessage(), _ => SelectedIssueMessage is not null);
        NavigateToIssueTransactionCommand = new RelayCommand(_ => NavigateToIssueTransaction(), _ => SelectedIssueTransaction is not null);
        Replace(Glossary, _explanationService.GetGlossary());
        ThemeService.Apply(_selectedTheme);
        RefreshFilteredViews();
    }

    public ObservableCollection<CanFrame> Frames { get; } = [];
    public ObservableCollection<IsoTpMessage> Messages { get; } = [];
    public ObservableCollection<UdsTransaction> Transactions { get; } = [];
    public ObservableCollection<DiagnosticIssue> Issues { get; } = [];
    public ObservableCollection<EcuSummary> EcuBreakdown { get; } = [];
    public ObservableCollection<CanFrame> FilteredFrames { get; } = [];
    public ObservableCollection<IsoTpMessage> FilteredMessages { get; } = [];
    public ObservableCollection<UdsTransaction> FilteredTransactions { get; } = [];
    public ObservableCollection<DiagnosticIssue> FilteredIssues { get; } = [];
    public ObservableCollection<EcuSummary> FilteredEcuBreakdown { get; } = [];
    public ObservableCollection<GuidedFinding> TopFindings { get; } = [];
    public ObservableCollection<EcuHealthCard> EcuHealthCards { get; } = [];
    public ObservableCollection<GlossaryTerm> Glossary { get; } = [];
    public ObservableCollection<string> SeverityOptions { get; } = ["All", "Critical", "Error", "Warning", "Info"];
    public ObservableCollection<string> ChannelOptions { get; } = ["All"];
    public ObservableCollection<string> ServiceOptions { get; } = ["All"];
    public ObservableCollection<string> StatusOptions { get; } = ["All"];
    public ObservableCollection<string> MessageTypeOptions { get; } = ["All", "SingleFrame", "FirstFrame", "ConsecutiveFrame", "FlowControl"];
    public ObservableCollection<string> ThemeOptions { get; } = ["System", "Light", "Dark"];

    public ICommand LoadFileCommand { get; }
    public ICommand ExportHtmlCommand { get; }
    public ICommand ExportIssuesCsvCommand { get; }
    public ICommand ExportTransactionsCsvCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand NavigateToIssueFrameCommand { get; }
    public ICommand NavigateToIssueMessageCommand { get; }
    public ICommand NavigateToIssueTransactionCommand { get; }

    public string FileName { get => _fileName; private set => SetProperty(ref _fileName, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public AnalysisSummary Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public DashboardSummary Dashboard { get => _dashboard; private set => SetProperty(ref _dashboard, value); }
    public bool HasResults => _currentResult is not null;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, NormalizeFilter(value)))
            {
                ThemeService.Apply(_selectedTheme);
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshFilteredViews();
            }
        }
    }

    public string SeverityFilter
    {
        get => _severityFilter;
        set
        {
            if (SetProperty(ref _severityFilter, NormalizeFilter(value)))
            {
                RefreshFilteredViews();
            }
        }
    }

    public string ChannelFilter
    {
        get => _channelFilter;
        set
        {
            if (SetProperty(ref _channelFilter, NormalizeFilter(value)))
            {
                RefreshFilteredViews();
            }
        }
    }

    public string ServiceFilter
    {
        get => _serviceFilter;
        set
        {
            if (SetProperty(ref _serviceFilter, NormalizeFilter(value)))
            {
                RefreshFilteredViews();
            }
        }
    }

    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, NormalizeFilter(value)))
            {
                RefreshFilteredViews();
            }
        }
    }

    public string MessageTypeFilter
    {
        get => _messageTypeFilter;
        set
        {
            if (SetProperty(ref _messageTypeFilter, NormalizeFilter(value)))
            {
                RefreshFilteredViews();
            }
        }
    }

    public CanFrame? SelectedFrame { get => _selectedFrame; set => SetProperty(ref _selectedFrame, value); }
    public UdsTransaction? SelectedTransaction { get => _selectedTransaction; set => SetProperty(ref _selectedTransaction, value); }
    public IsoTpMessage? SelectedMessage { get => _selectedMessage; set => SetProperty(ref _selectedMessage, value); }
    public UdsTransaction? SelectedIssueTransaction { get => _selectedIssueTransaction; private set => SetProperty(ref _selectedIssueTransaction, value); }
    public IsoTpMessage? SelectedIssueMessage { get => _selectedIssueMessage; private set => SetProperty(ref _selectedIssueMessage, value); }
    public string SelectedIssueRootCauseMap { get => _selectedIssueRootCauseMap; private set => SetProperty(ref _selectedIssueRootCauseMap, value); }

    public DiagnosticIssue? SelectedIssue
    {
        get => _selectedIssue;
        set
        {
            if (SetProperty(ref _selectedIssue, value) && value is not null)
            {
                ResolveIssueNavigation(value);
            }
        }
    }

    public string SelectedIssuePrimaryEvidence => SelectedIssue is null
        ? "Select an issue to see evidence."
        : $"Line {SelectedIssue.LineNumber}, {SelectedIssue.TimestampMs:F3} ms, {SelectedIssue.Channel}";

    private async Task LoadFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load PCAN TRC log",
            Filter = "PCAN trace files (*.trc)|*.trc|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Parsing and analyzing log...";
            FileName = Path.GetFileName(dialog.FileName);

            using var cts = new CancellationTokenSource();
            var result = await _analysisService.AnalyzeAsync(dialog.FileName, cts.Token);
            _explanationService.Enrich(result.Issues, result.Transactions);
            ApplyResult(result);
            StatusText = $"Loaded {result.Summary.TotalFrames:N0} frames, {result.Summary.TotalMessages:N0} ISO-TP messages, {result.Summary.TotalErrors:N0} errors.";
        }
        catch (Exception ex)
        {
            StatusText = "Analysis failed";
            MessageBox.Show(System.Windows.Application.Current.MainWindow, ex.Message, "TRC analysis failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyResult(AnalysisResult result)
    {
        _currentResult = result;
        Replace(Frames, result.Frames);
        Replace(Messages, result.Messages);
        Replace(Transactions, result.Transactions);
        Replace(Issues, result.Issues);
        Replace(EcuBreakdown, result.Summary.EcuBreakdown);
        Summary = result.Summary;
        RebuildFilterOptions();
        RefreshFilteredViews();
        OnPropertyChanged(nameof(HasResults));
        RaiseCommandStates();
    }

    private void ResolveIssueNavigation(DiagnosticIssue issue)
    {
        SelectedFrame = Frames.FirstOrDefault(frame => frame.LineNumber == issue.LineNumber)
            ?? Frames.OrderBy(frame => Math.Abs(frame.TimestampMs - issue.TimestampMs)).FirstOrDefault();
        SelectedIssueMessage = Messages
            .Where(message => issue.LineNumber >= message.StartLineNumber && issue.LineNumber <= message.EndLineNumber)
            .OrderBy(message => Math.Abs(message.StartTimeMs - issue.TimestampMs))
            .FirstOrDefault()
            ?? Messages.OrderBy(message => Math.Abs(message.StartTimeMs - issue.TimestampMs)).FirstOrDefault();
        SelectedIssueTransaction = Transactions
            .Where(transaction => transaction.Channel.DisplayName == issue.Channel || issue.Channel.Contains(transaction.Channel.ResponseId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(transaction => Math.Abs(transaction.StartTimeMs - issue.TimestampMs))
            .FirstOrDefault();

        if (SelectedIssueMessage is not null)
        {
            SelectedMessage = SelectedIssueMessage;
        }

        if (SelectedIssueTransaction is not null)
        {
            SelectedTransaction = SelectedIssueTransaction;
        }

        SelectedIssueRootCauseMap = BuildRootCauseMap(issue);
        OnPropertyChanged(nameof(SelectedIssuePrimaryEvidence));
        RaiseNavigationCommands();
    }

    private string BuildRootCauseMap(DiagnosticIssue issue)
    {
        var request = SelectedIssueTransaction?.RequestDisplay ?? "-";
        var response = SelectedIssueTransaction?.ResponseDisplay ?? "-";
        var message = SelectedIssueMessage?.PayloadHex ?? "-";
        var frame = SelectedFrame?.DataHex ?? "-";
        return $"Finding -> {issue.Title}{Environment.NewLine}" +
               $"Likely cause -> {issue.LikelyCause}{Environment.NewLine}" +
               $"Transaction -> {SelectedIssueTransaction?.StatusDisplay ?? "No direct transaction"} / {SelectedIssueTransaction?.ServiceDisplay ?? "-"}{Environment.NewLine}" +
               $"Request -> {request}{Environment.NewLine}" +
               $"Response -> {response}{Environment.NewLine}" +
               $"Reconstructed payload -> {message}{Environment.NewLine}" +
               $"Raw frame -> {frame}";
    }

    private void RefreshFilteredViews()
    {
        var frames = Frames.Where(FrameMatches).ToArray();
        var messages = Messages.Where(MessageMatches).ToArray();
        var transactions = Transactions.Where(TransactionMatches).ToArray();
        var issues = Issues.Where(IssueMatches).ToArray();
        var channels = new HashSet<string>(transactions.Select(t => t.Channel.DisplayName).Concat(issues.Select(i => i.Channel)));
        var ecuBreakdown = EcuBreakdown.Where(e => ChannelFilter == "All" || e.Channel == ChannelFilter || channels.Contains(e.Channel)).ToArray();

        Replace(FilteredFrames, frames);
        Replace(FilteredMessages, messages);
        Replace(FilteredTransactions, transactions);
        Replace(FilteredIssues, issues);
        Replace(FilteredEcuBreakdown, ecuBreakdown);

        var filteredSummary = new AnalysisSummary
        {
            TotalFrames = frames.Length,
            TotalMessages = messages.Count(m => m.Status != IsoTpMessageStatus.FlowControl),
            TotalTransactions = transactions.Length,
            TotalErrors = issues.Count(i => i.Severity is IssueSeverity.Error or IssueSeverity.Critical),
            EcuBreakdown = ecuBreakdown
        };
        Dashboard = _explanationService.BuildDashboard(filteredSummary, issues, transactions, messages);
        Replace(TopFindings, Dashboard.TopFindings);
        Replace(EcuHealthCards, Dashboard.EcuHealthCards);
    }

    private bool FrameMatches(CanFrame frame) =>
        MatchesSearch(frame.CanId, frame.Direction, frame.IsoTpFrameTypeDisplay, frame.DataHex, frame.BusDisplay) &&
        MatchesChannel(frame.CanId) &&
        MatchesMessageType(frame.IsoTpFrameType.ToString());

    private bool MessageMatches(IsoTpMessage message) =>
        MatchesSearch(message.CanId, message.Direction, message.ChannelDisplay, message.PayloadHex, message.ServiceDisplay, message.Status.ToString(), message.FrameType.ToString()) &&
        MatchesChannel(message.ChannelDisplay) &&
        MatchesService(message.ServiceDisplay) &&
        MatchesMessageType(message.FrameType.ToString());

    private bool TransactionMatches(UdsTransaction transaction) =>
        MatchesSearch(transaction.Channel.DisplayName, transaction.ServiceDisplay, transaction.StatusDisplay, transaction.RequestDisplay, transaction.ResponseDisplay, transaction.UserFriendlySummary, transaction.LikelyCause) &&
        MatchesChannel(transaction.Channel.DisplayName) &&
        MatchesService(transaction.ServiceDisplay) &&
        MatchesStatus(transaction.StatusDisplay);

    private bool IssueMatches(DiagnosticIssue issue) =>
        MatchesSearch(issue.Title, issue.Description, issue.UserFriendlySummary, issue.LikelyCause, issue.Evidence, issue.Channel, issue.SeverityDisplay) &&
        MatchesChannel(issue.Channel) &&
        MatchesSeverity(issue.SeverityDisplay);

    private bool MatchesSearch(params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return values.Any(value => value?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true);
    }

    private bool MatchesSeverity(string? severity) => IsAll(SeverityFilter) || string.Equals(severity, SeverityFilter, StringComparison.OrdinalIgnoreCase);
    private bool MatchesChannel(string? channel) => IsAll(ChannelFilter) || (!string.IsNullOrWhiteSpace(channel) && (channel.Contains(ChannelFilter, StringComparison.OrdinalIgnoreCase) || ChannelFilter.Contains(channel, StringComparison.OrdinalIgnoreCase)));
    private bool MatchesService(string? service) => IsAll(ServiceFilter) || service?.Contains(ServiceFilter, StringComparison.OrdinalIgnoreCase) == true;
    private bool MatchesStatus(string? status) => IsAll(StatusFilter) || string.Equals(status, StatusFilter, StringComparison.OrdinalIgnoreCase);
    private bool MatchesMessageType(string? type) => IsAll(MessageTypeFilter) || string.Equals(type, MessageTypeFilter, StringComparison.OrdinalIgnoreCase);

    private static bool IsAll(string? value) => string.IsNullOrWhiteSpace(value) || value.Equals("All", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeFilter(string? value) => string.IsNullOrWhiteSpace(value) ? "All" : value;

    private void RebuildFilterOptions()
    {
        Replace(ChannelOptions, new[] { "All" }.Concat(Transactions.Select(t => t.Channel.DisplayName).Concat(Messages.Select(m => m.ChannelDisplay)).Concat(Issues.Select(i => i.Channel)).Where(s => !string.IsNullOrWhiteSpace(s) && s != "-").Distinct().OrderBy(s => s)));
        Replace(ServiceOptions, new[] { "All" }.Concat(Transactions.Select(t => t.ServiceDisplay).Concat(Messages.Select(m => m.ServiceDisplay)).Where(s => !string.IsNullOrWhiteSpace(s) && s != "-").Distinct().OrderBy(s => s)));
        Replace(StatusOptions, new[] { "All" }.Concat(Transactions.Select(t => t.StatusDisplay).Distinct().OrderBy(s => s)));
    }

    private void ClearFilters()
    {
        _searchText = "";
        _severityFilter = "All";
        _channelFilter = "All";
        _serviceFilter = "All";
        _statusFilter = "All";
        _messageTypeFilter = "All";
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SeverityFilter));
        OnPropertyChanged(nameof(ChannelFilter));
        OnPropertyChanged(nameof(ServiceFilter));
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(MessageTypeFilter));
        RefreshFilteredViews();
    }

    private async Task ExportHtmlAsync()
    {
        var dialog = new SaveFileDialog { Title = "Export diagnostic report", Filter = "HTML report (*.html)|*.html", FileName = "ev-uds-diagnostic-report.html" };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) == true)
        {
            await _reportExportService.ExportHtmlAsync(dialog.FileName, Dashboard, FilteredIssues, FilteredTransactions, CancellationToken.None);
            StatusText = $"Exported report: {dialog.FileName}";
        }
    }

    private async Task ExportIssuesCsvAsync()
    {
        var dialog = new SaveFileDialog { Title = "Export issues", Filter = "CSV files (*.csv)|*.csv", FileName = "ev-uds-issues.csv" };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) == true)
        {
            await _reportExportService.ExportIssuesCsvAsync(dialog.FileName, FilteredIssues, CancellationToken.None);
            StatusText = $"Exported issues: {dialog.FileName}";
        }
    }

    private async Task ExportTransactionsCsvAsync()
    {
        var dialog = new SaveFileDialog { Title = "Export transactions", Filter = "CSV files (*.csv)|*.csv", FileName = "ev-uds-transactions.csv" };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) == true)
        {
            await _reportExportService.ExportTransactionsCsvAsync(dialog.FileName, FilteredTransactions, CancellationToken.None);
            StatusText = $"Exported transactions: {dialog.FileName}";
        }
    }

    private bool CanExport() => !IsBusy && HasResults;

    private void RaiseCommandStates()
    {
        if (LoadFileCommand is AsyncRelayCommand load)
        {
            load.RaiseCanExecuteChanged();
        }

        if (ExportHtmlCommand is AsyncRelayCommand html)
        {
            html.RaiseCanExecuteChanged();
        }

        if (ExportIssuesCsvCommand is AsyncRelayCommand issues)
        {
            issues.RaiseCanExecuteChanged();
        }

        if (ExportTransactionsCsvCommand is AsyncRelayCommand transactions)
        {
            transactions.RaiseCanExecuteChanged();
        }

        RaiseNavigationCommands();
    }

    private void RaiseNavigationCommands()
    {
        if (NavigateToIssueFrameCommand is RelayCommand frame)
        {
            frame.RaiseCanExecuteChanged();
        }

        if (NavigateToIssueMessageCommand is RelayCommand message)
        {
            message.RaiseCanExecuteChanged();
        }

        if (NavigateToIssueTransactionCommand is RelayCommand transaction)
        {
            transaction.RaiseCanExecuteChanged();
        }
    }

    private void NavigateToIssueFrame()
    {
        SelectedTabIndex = 1;
    }

    private void NavigateToIssueMessage()
    {
        SelectedTabIndex = 2;
    }

    private void NavigateToIssueTransaction()
    {
        SelectedTabIndex = 3;
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }
}
