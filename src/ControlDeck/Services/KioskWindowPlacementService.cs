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
        var target = Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? Screen.AllScreens.First();
        var bounds = target.Bounds;

        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowPos(hwnd, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate);
    }
}
