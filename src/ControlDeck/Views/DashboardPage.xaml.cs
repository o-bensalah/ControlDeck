using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ControlDeck.Services;

namespace ControlDeck.Views;

public partial class DashboardPage : UserControl, IDisposable
{
    private readonly AudioService _audio = new();
    private readonly HardwareMonitorService _hardware = new();
    private readonly DispatcherTimer _metricsTimer;
    private bool _suppressSliderEvent;

    public DashboardPage()
    {
        InitializeComponent();

        VolumeSlider.Value = _audio.Volume * 100;
        MuteButton.IsChecked = _audio.IsMuted;
        _audio.VolumeChanged += OnSystemVolumeChanged;

        _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _metricsTimer.Tick += async (_, _) => await RefreshMetricsAsync();
        Loaded += (_, _) => _metricsTimer.Start();
        Unloaded += (_, _) => _metricsTimer.Stop();
    }

    private void OnSystemVolumeChanged(float level, bool muted)
    {
        Dispatcher.Invoke(() =>
        {
            _suppressSliderEvent = true;
            VolumeSlider.Value = level * 100;
            MuteButton.IsChecked = muted;
            _suppressSliderEvent = false;
        });
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderEvent) return;
        _audio.Volume = (float)(e.NewValue / 100.0);
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e) => _audio.IsMuted = MuteButton.IsChecked == true;

    private void PrintScreen_Click(object sender, RoutedEventArgs e) => SystemActionsService.PrintScreen();
    private void ShowDesktop_Click(object sender, RoutedEventArgs e) => SystemActionsService.ShowDesktop();
    private void Lock_Click(object sender, RoutedEventArgs e) => SystemActionsService.Lock();
    private void Sleep_Click(object sender, RoutedEventArgs e) => SystemActionsService.Sleep();
    private void TaskManager_Click(object sender, RoutedEventArgs e) => SystemActionsService.OpenTaskManager();
    private void FileExplorer_Click(object sender, RoutedEventArgs e) => SystemActionsService.OpenFileExplorer();

    private async Task RefreshMetricsAsync()
    {
        var snapshot = await Task.Run(() => _hardware.Read());

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
        _audio.VolumeChanged -= OnSystemVolumeChanged;
        _audio.Dispose();
        _metricsTimer.Stop();
        _hardware.Dispose();
    }
}
