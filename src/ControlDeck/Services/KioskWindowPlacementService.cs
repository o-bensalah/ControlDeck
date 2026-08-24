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
        var (deviceName, displayNumber) = ControlDeckConfig.LoadDisplay();
        var target = FindConfiguredScreen(deviceName, displayNumber) ?? FindFallbackScreen();
        var bounds = target.Bounds;

        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowPos(hwnd, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate);
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

        if (displayNumber is int number)
        {
            var match = Screen.AllScreens.FirstOrDefault(s => ControlDeckConfig.ParseDisplayNumber(s.DeviceName) == number);
            if (match is not null) return match;
        }

        return null;
    }

    // No config, or the configured screen isn't connected — same guess as before: the first
    // non-primary screen, or the primary if that's all there is (single-monitor dev machines).
    private static Screen FindFallbackScreen()
        => Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? Screen.AllScreens.First();
}
