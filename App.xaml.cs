using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace EvUdsAnalyzer;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            ClearPreviousStartupError();
            base.OnStartup(e);
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex);
            Shutdown(1);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportStartupFailure(e.Exception);
        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ReportStartupFailure(ex);
        }
    }

    private static void ReportStartupFailure(Exception ex)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
        File.WriteAllText(logPath, ex.ToString());
        MessageBox.Show(
            $"EV UDS Analyzer failed to start.\n\n{ex.Message}\n\nDetails were written to:\n{logPath}",
            "Startup error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void ClearPreviousStartupError()
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }
    }
}
