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

        MemoryText.Text = snapshot.MemoryTotalGb > 0
            ? $"{snapshot.MemoryUsedGb:0.0} / {snapshot.MemoryTotalGb:0.0} GB"
            : "—";
        MemoryBar.Value = snapshot.MemoryTotalGb > 0 ? snapshot.MemoryUsedGb / snapshot.MemoryTotalGb * 100 : 0;
    }

    public void Dispose()
    {
        _timer.Stop();
        _hardware.Dispose();
    }
}
