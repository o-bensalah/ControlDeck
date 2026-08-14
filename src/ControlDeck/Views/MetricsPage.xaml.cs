using System.Windows.Controls;
using System.Windows.Threading;
using ControlDeck.Services;

namespace ControlDeck.Views;

public partial class MetricsPage : UserControl, IDisposable
{
    private readonly HardwareMonitorService _hardware = new();
    private readonly DispatcherTimer _timer;

    public MetricsPage()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
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
        MemoryText.Text = hasMemory ? $"{snapshot.MemoryUsedGb:0.0} / {snapshot.MemoryTotalGb:0.0} GB" : "—";
        MemoryPercentText.Text = hasMemory ? $"{memoryPercent:0}% used" : "—";
        MemoryBar.Value = memoryPercent;

        DiskText.Text = snapshot.DiskUsedPercent is { } diskPercent ? $"{diskPercent:0}%" : "—";
        DiskTempText.Text = snapshot.DiskTemp is { } diskTemp ? $"{diskTemp:0}°C" : "—";
        DiskBar.Value = snapshot.DiskUsedPercent ?? 0;

        NetworkDownText.Text = $"↓ {FormatRate(snapshot.NetworkDownKBs)}";
        NetworkUpText.Text = $"↑ {FormatRate(snapshot.NetworkUpKBs)}";

        UptimeText.Text = FormatUptime(snapshot.Uptime);
    }

    private static string FormatRate(float kbPerSecond) =>
        kbPerSecond >= 1024 ? $"{kbPerSecond / 1024:0.0} MB/s" : $"{kbPerSecond:0} KB/s";

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1) return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    public void Dispose()
    {
        _timer.Stop();
        _hardware.Dispose();
    }
}
