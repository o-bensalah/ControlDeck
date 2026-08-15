using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ControlDeck.Services;

namespace ControlDeck.Views;

// One page class handles every "page" of shortcuts, however many the catalog needs — MainWindow
// chunks AppLauncherCatalog.Load() into groups and creates one instance per chunk. Only the first
// chunk shows metrics (RefreshMetricsAsync/_hardware are null otherwise); every instance gets its
// own MediaWidget so playback controls stay reachable no matter which shortcuts page is showing.
public partial class ShortcutsPage : UserControl, IDisposable
{
    // Fixed grid shape rather than letting UniformGrid auto-size rows from child count — a page
    // with fewer buttons (the last, partly-filled chunk) would otherwise stretch its cells bigger
    // than a full page's. Empty cells on a partial page just stay blank instead.
    internal const int GridColumns = 4;
    internal const int GridRows = 3;
    internal const int MaxEntriesPerPage = GridColumns * GridRows;

    private readonly HardwareMonitorService? _hardware;
    private readonly DispatcherTimer? _metricsTimer;

    internal ShortcutsPage(IReadOnlyList<AppLauncherEntry> entries, bool showMetrics)
    {
        InitializeComponent();

        ShortcutsGrid.Rows = GridRows;
        ShortcutsGrid.Columns = GridColumns;

        foreach (var entry in entries)
        {
            var button = new Button
            {
                Content = entry.Name,
                Style = (Style)FindResource("DeckButtonStyle"),
                Margin = new Thickness(10),
            };
            button.Click += (_, _) => LaunchApp(entry);
            ShortcutsGrid.Children.Add(button);
        }

        if (showMetrics)
        {
            _hardware = new HardwareMonitorService();
            _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _metricsTimer.Tick += async (_, _) => await RefreshMetricsAsync();
            Loaded += (_, _) => _metricsTimer.Start();
            Unloaded += (_, _) => _metricsTimer.Stop();
        }
        else
        {
            // MetricsRow keeps its height reserved (not collapsed to 0) even when hidden, so
            // Row0 gets the exact same available height on every shortcuts page — otherwise a
            // page without metrics would have extra vertical room, growing its button cells
            // taller than a page with metrics, the same inconsistency as the row-count issue.
            MetricsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void LaunchApp(AppLauncherEntry entry)
    {
        LaunchStatusText.Text = AppLauncherService.TryLaunch(entry, out var error)
            ? ""
            : $"Couldn't launch \"{entry.Name}\": {error}";
    }

    private async Task RefreshMetricsAsync()
    {
        var snapshot = await Task.Run(() => _hardware!.Read());

        CpuLoadText.Text = $"{snapshot.CpuLoad:0}%";
        CpuTempText.Text = snapshot.CpuTemp is { } cpuTemp ? $"{cpuTemp:0}°C" : "—";
        CpuLoadBar.Value = snapshot.CpuLoad;

        GpuLoadText.Text = $"{snapshot.GpuLoad:0}%";
        GpuTempText.Text = snapshot.GpuTemp is { } gpuTemp ? $"{gpuTemp:0}°C" : "—";
        GpuLoadBar.Value = snapshot.GpuLoad;

        bool hasMemory = snapshot.MemoryTotalGb > 0;
        double memoryPercent = hasMemory ? snapshot.MemoryUsedGb / snapshot.MemoryTotalGb * 100 : 0;
        MemoryText.Text = hasMemory ? $"{snapshot.MemoryUsedGb:0.0}/{snapshot.MemoryTotalGb:0.0} GB" : "—";
        MemoryPercentText.Text = hasMemory ? $"{memoryPercent:0}%" : "—";
        MemoryBar.Value = memoryPercent;

        DiskText.Text = snapshot.DiskUsedPercent is { } diskPercent ? $"{diskPercent:0}%" : "—";
        DiskTempText.Text = snapshot.DiskTemp is { } diskTemp ? $"{diskTemp:0}°C" : "—";
        DiskBar.Value = snapshot.DiskUsedPercent ?? 0;

        NetworkDownText.Text = $"↓{FormatRate(snapshot.NetworkDownKBs)}";
        NetworkUpText.Text = $"↑{FormatRate(snapshot.NetworkUpKBs)}";

        UptimeText.Text = FormatUptime(snapshot.Uptime);
    }

    private static string FormatRate(float kbPerSecond) =>
        kbPerSecond >= 1024 ? $"{kbPerSecond / 1024:0.0}MB/s" : $"{kbPerSecond:0}KB/s";

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1) return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    public void Dispose()
    {
        _metricsTimer?.Stop();
        _hardware?.Dispose();
    }
}
