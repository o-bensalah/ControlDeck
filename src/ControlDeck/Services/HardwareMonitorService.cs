using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace ControlDeck.Services;

internal sealed record HardwareSnapshot
{
    public float CpuLoad { get; init; }
    public float? CpuTemp { get; init; }
    public float GpuLoad { get; init; }
    public float? GpuTemp { get; init; }
    public float MemoryUsedGb { get; init; }
    public float MemoryTotalGb { get; init; }
    public float? DiskUsedPercent { get; init; }
    public float? DiskTemp { get; init; }
    public float NetworkDownKBs { get; init; }
    public float NetworkUpKBs { get; init; }
    public TimeSpan Uptime { get; init; }
}

internal sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsStorageEnabled = true,
        IsNetworkEnabled = true,
    };

    public HardwareMonitorService() => _computer.Open();

    public HardwareSnapshot Read()
    {
        float cpuLoad = 0, gpuLoad = 0, memUsed = 0, memAvailable = 0, networkDown = 0, networkUp = 0;
        float? cpuTemp = null, gpuTemp = null, diskUsedPercent = null, diskTemp = null;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    cpuLoad = ReadNamed(hardware, SensorType.Load, "CPU Total") ?? cpuLoad;
                    cpuTemp = ReadNamed(hardware, SensorType.Temperature, "CPU Package") ?? ReadAny(hardware, SensorType.Temperature);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    gpuLoad = ReadNamed(hardware, SensorType.Load, "GPU Core") ?? ReadAny(hardware, SensorType.Load) ?? gpuLoad;
                    gpuTemp = ReadNamed(hardware, SensorType.Temperature, "GPU Core") ?? ReadAny(hardware, SensorType.Temperature);
                    break;
                case HardwareType.Memory:
                    memUsed = ReadNamed(hardware, SensorType.Data, "Memory Used") ?? memUsed;
                    memAvailable = ReadNamed(hardware, SensorType.Data, "Memory Available") ?? memAvailable;
                    break;
                // First storage device wins — good enough for a single-drive desktop; not worth
                // aggregating multiple drives into one tile.
                case HardwareType.Storage when diskUsedPercent is null:
                    diskUsedPercent = ReadNamed(hardware, SensorType.Load, "Used Space");
                    diskTemp = ReadAny(hardware, SensorType.Temperature);
                    break;
                // Sum across adapters rather than picking "the" active one — idle adapters
                // just report ~0, so this naturally tracks whichever link is actually busy.
                case HardwareType.Network:
                    networkDown += ReadNamed(hardware, SensorType.Throughput, "Download Speed") ?? 0;
                    networkUp += ReadNamed(hardware, SensorType.Throughput, "Upload Speed") ?? 0;
                    break;
            }
        }

        return new HardwareSnapshot
        {
            CpuLoad = cpuLoad,
            CpuTemp = cpuTemp,
            GpuLoad = gpuLoad,
            GpuTemp = gpuTemp,
            MemoryUsedGb = memUsed,
            MemoryTotalGb = memUsed + memAvailable,
            DiskUsedPercent = diskUsedPercent,
            DiskTemp = diskTemp,
            NetworkDownKBs = networkDown / 1024f,
            NetworkUpKBs = networkUp / 1024f,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
        };
    }

    private static float? ReadNamed(IHardware hardware, SensorType type, string nameContains) =>
        hardware.Sensors
            .FirstOrDefault(s => s.SensorType == type && s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static float? ReadAny(IHardware hardware, SensorType type) =>
        hardware.Sensors.FirstOrDefault(s => s.SensorType == type)?.Value;

    public void Dispose() => _computer.Close();
}
