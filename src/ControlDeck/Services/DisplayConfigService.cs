using System.Runtime.InteropServices;

namespace ControlDeck.Services;

// Resolves a "Display N" number to the monitor Windows itself means by it — the number shown in
// Settings > Display's "Identify" overlay. That numbering comes from the 1-based order of active
// paths returned by the Windows Display Configuration API (QueryDisplayConfig), NOT from the
// trailing digit of a monitor's GDI device name (\\.\DISPLAY2's "2"). Those two numbering schemes
// are assigned independently and can diverge — confirmed on a real 3-monitor setup where Identify's
// "3" was a monitor GDI called \\.\DISPLAY2, while \\.\DISPLAY3 was a completely different monitor.
// KioskWindowPlacementService matches against Screen.AllScreens by DeviceName, so this only needs
// to bridge "Identify number" -> GDI device name; everything downstream already worked correctly
// once handed the right name.
internal static class DisplayConfigService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigRational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DisplayConfigRational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo SourceInfo;
        public DisplayConfigPathTargetInfo TargetInfo;
        public uint Flags;
    }

    // DISPLAYCONFIG_MODE_INFO's real shape is a tagged union of mode-specific structs we never
    // read — QueryDisplayConfig still requires a correctly *sized* buffer to write into, so this
    // stands in as an opaque 64-byte (its true size) element purely to keep the array stride right.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct DisplayConfigModeInfoRaw
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    private const uint QdcOnlyActivePaths = 0x00000002;
    private const uint DisplayConfigDeviceInfoGetSourceName = 1;
    private const int ErrorSuccess = 0;

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfoRaw[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

    // displayNumber is 1-based, matching what Settings' Identify overlay shows. Returns null on any
    // API failure, an out-of-range number, or a path whose GDI name can't be resolved — callers
    // already treat "couldn't resolve the configured display" as "fall back to the default screen".
    public static string? GetDeviceNameForDisplayNumber(int displayNumber)
    {
        if (displayNumber < 1) return null;

        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount) != ErrorSuccess)
            return null;

        if (displayNumber > pathCount) return null;

        var paths = new DisplayConfigPathInfo[pathCount];
        var modes = new DisplayConfigModeInfoRaw[modeCount];
        if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != ErrorSuccess)
            return null;

        var source = paths[displayNumber - 1].SourceInfo;
        var request = new DisplayConfigSourceDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoGetSourceName,
                Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                AdapterId = source.AdapterId,
                Id = source.Id,
            },
        };

        return DisplayConfigGetDeviceInfo(ref request) == ErrorSuccess ? request.ViewGdiDeviceName : null;
    }
}
