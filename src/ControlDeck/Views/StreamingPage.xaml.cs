using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ControlDeck.Services;

namespace ControlDeck.Views;

public partial class StreamingPage : UserControl, IDisposable
{
    private KioskExitOverlay? _exitOverlay;
    private Process? _browserProcess;

    public StreamingPage()
    {
        InitializeComponent();

        foreach (var service in ControlDeckConfig.LoadStreamingServices())
        {
            var button = new Button
            {
                Content = service.Name,
                Style = (Style)FindResource("DeckButtonStyle"),
                Margin = new Thickness(12),
            };
            button.Click += (_, _) => OpenService(service.Url);
            ServicesGrid.Children.Add(button);
        }
    }

    private async void OpenService(string url)
    {
        if (Window.GetWindow(this) is not { } mainWindow) return;

        CloseActiveBrowser();

        var ownerHwnd = new WindowInteropHelper(mainWindow).Handle;
        var process = await KioskBrowserLauncher.LaunchAsync(url, ownerHwnd);
        if (process is null) return;

        _browserProcess = process;
        ShowExitOverlay(mainWindow, process);
    }

    private void ShowExitOverlay(Window mainWindow, Process browserProcess)
    {
        var overlay = new KioskExitOverlay { Owner = mainWindow };
        overlay.Left = mainWindow.Left + mainWindow.ActualWidth - overlay.Width - 24;
        overlay.Top = mainWindow.Top + 24;

        EventHandler? onExited = null;
        onExited = (_, _) => overlay.Dispatcher.BeginInvoke(() =>
        {
            browserProcess.Exited -= onExited;
            overlay.Close();
        });
        browserProcess.EnableRaisingEvents = true;
        browserProcess.Exited += onExited;

        overlay.ExitRequested += (_, _) =>
        {
            browserProcess.Exited -= onExited;
            TryKill(browserProcess);
            overlay.Close();
        };

        _exitOverlay = overlay;
        overlay.Show();
    }

    private void CloseActiveBrowser()
    {
        _exitOverlay?.Close();
        _exitOverlay = null;

        if (_browserProcess is not null)
        {
            TryKill(_browserProcess);
            _browserProcess = null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill();
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose() => CloseActiveBrowser();
}
