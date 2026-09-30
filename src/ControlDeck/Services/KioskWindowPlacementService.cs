using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace ControlDeck.Services;

internal static class KioskWindowPlacementService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopmost = new(-1);

    // Screen.Bounds is only guaranteed to be in raw pixels (matching SetWindowPos) when the
    // process declares Per-Monitor V2 DPI awareness in app.manifest.
    public static void PlaceOnTargetScreen(Window window)
    {
        var bounds = GetTargetScreen().Bounds;

        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowPos(hwnd, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate);
    }

    // Exposed so anything else that needs to land on the same physical monitor as the kiosk
    // window (e.g. KioskBrowserLauncher positioning the external browser) uses the identical
    // configured-screen/fallback logic instead of guessing independently.
    public static Screen GetTargetScreen()
    {
        var (deviceName, displayNumber) = ControlDeckConfig.LoadDisplay();
        return FindConfiguredScreen(deviceName, displayNumber) ?? FindFallbackScreen();
    }

    // DeviceName checked first (more precise), then DisplayNumber — null if config.json has
    // neither set, or if the configured screen isn't currently connected (e.g. the kiosk monitor
    // got unplugged), in which case the caller falls back to the default heuristic below.
    private static Screen? FindConfiguredScreen(string? deviceName, int? displayNumber)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            var match = Screen.AllScreens.FirstOrDefault(s =>
                string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        // Resolved via the Windows Display Configuration API (the same numbering Settings'
        // Identify overlay uses), not the monitor's GDI device name — those diverge in practice.
        if (displayNumber is int number && DisplayConfigService.GetDeviceNameForDisplayNumber(number) is string gdiName)
        {
            var match = Screen.AllScreens.FirstOrDefault(s =>
                string.Equals(s.DeviceName, gdiName, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return null;
    }

    // No config, or the configured screen isn't connected — same guess as before: the first
    // non-primary screen, or the primary if that's all there is (single-monitor dev machines).
    private static Screen FindFallbackScreen()
        => Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? Screen.AllScreens.First();
}
