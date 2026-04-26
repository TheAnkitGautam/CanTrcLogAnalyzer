using System.Windows;
using EvUdsAnalyzer.UI.ViewModels;

namespace EvUdsAnalyzer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = CompositionRoot.CreateMainViewModel();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.SelectedFrame) && viewModel.SelectedFrame is not null)
            {
                Dispatcher.BeginInvoke(() => FramesGrid.ScrollIntoView(viewModel.SelectedFrame));
            }

            if (args.PropertyName == nameof(MainViewModel.SelectedMessage) && viewModel.SelectedMessage is not null)
            {
                Dispatcher.BeginInvoke(() => MessagesGrid.ScrollIntoView(viewModel.SelectedMessage));
            }

            if (args.PropertyName == nameof(MainViewModel.SelectedTransaction) && viewModel.SelectedTransaction is not null)
            {
                Dispatcher.BeginInvoke(() => TransactionsGrid.ScrollIntoView(viewModel.SelectedTransaction));
            }
        };

        DataContext = viewModel;
    }
}
