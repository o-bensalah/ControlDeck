using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ControlDeck.Services;

internal static class SystemActionsService
{
    public static void Lock() => LockWorkStation();

    // Simulating the real Win+PrtScn keystroke (the old approach here) turned out to depend on
    // Windows Settings' "Use the Print Screen key to open screen capture" toggle and/or the shell
    // build's specific handling of it — on at least one real machine it produced no file at all,
    // silently, regardless of the extended-key fix. Capturing and saving the screenshot directly
    // sidesteps all of that: it works the same regardless of what's configured in Windows.
    public static bool TryPrintScreen(out string? error)
    {
        try
        {
            var bounds = SystemInformation.VirtualScreen;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"Screenshot {DateTime.Now:yyyy-MM-dd HHmmss}.png");
            bitmap.Save(path, ImageFormat.Png);

            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    // Simulating Win+D (the old approach here) hit the same class of problem as the old PrtScn
    // simulation — a synthesized global-hotkey combo that Explorer isn't reliably recognizing, made
    // worse by a triple-monitor setup. Shell.Application's ToggleDesktop is the same COM call the
    // taskbar's own "Show Desktop" corner button makes, so it doesn't depend on hotkey simulation
    // being recognized at all.
    public static bool TryShowDesktop(out string? error)
    {
        object? shell = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application")
                ?? throw new COMException("Shell.Application COM type not found");
            shell = Activator.CreateInstance(shellType);
            shellType.InvokeMember("ToggleDesktop", BindingFlags.InvokeMethod, null, shell, null);

            error = null;
            return true;
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (shell is not null) Marshal.ReleaseComObject(shell);
        }
    }

    public static void Sleep() => SetSuspendState(false, false, false);

    public static void OpenTaskManager() => Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });

    public static void OpenFileExplorer() => Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("PowrProf.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
