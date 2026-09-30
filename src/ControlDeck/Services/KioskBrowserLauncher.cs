using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ControlDeck.Services;

// Streaming sites need a real, unrestricted browser (multi-window OAuth/login flows, whatever
// chrome/extensions the site expects) — embedding one via WebView2 inside this kiosk window fought
// a losing battle with popups: MainWindow is forced HWND_TOPMOST across the whole screen (see
// KioskWindowPlacementService), so any window WebView2 opened on its own rendered invisibly behind
// it, and a second embedded WebView2 as an overlay didn't work either, since two WebView2 controls
// are both native HWNDs whose stacking follows raw Win32 z-order, not WPF's visual tree.
//
// Launching the user's actual browser as a separate process sidesteps both problems: it gets its
// own real top-level window, and once explicitly owned under MainWindow (see OwnAndPosition) it
// stacks correctly above the kiosk window despite MainWindow's topmost status, the same way a
// WPF-owned window would.
internal static class KioskBrowserLauncher
{
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const int GwlpHwndParent = -8;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;

    private static readonly HashSet<string> ChromiumProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge", "chrome", "brave", "opera", "opera_gx", "vivaldi", "chromium",
    };

    // A dedicated, persistent profile directory (rather than the user's real browser profile)
    // isolates the streaming-site session from their everyday browsing AND guarantees Chromium
    // treats this as a genuinely separate process instead of just messaging an already-running
    // instance of the same browser — without that, Process.Start could hand back a process that
    // exits immediately while the actual window belongs to the pre-existing instance, and we'd
    // have no reliable handle to own/position/kill later. Persistent (not a temp dir wiped per
    // run) so streaming site logins actually stick across launches.
    private static readonly string ProfileRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControlDeck", "BrowserProfile");

    public static async Task<Process?> LaunchAsync(string url, IntPtr ownerHwnd)
    {
        string exePath = ResolveBrowserExecutable();
        string browserName = Path.GetFileNameWithoutExtension(exePath);
        string profileDir = Path.Combine(ProfileRoot, browserName);
        Directory.CreateDirectory(profileDir);

        var bounds = KioskWindowPlacementService.GetTargetScreen().Bounds;

        var startInfo = new ProcessStartInfo { FileName = exePath, UseShellExecute = false };
        bool isFirefox = browserName.Equals("firefox", StringComparison.OrdinalIgnoreCase);
        if (isFirefox)
        {
            // Firefox's kiosk flag predates ArgumentList-style parsing conventions and uses a
            // single dash, unlike every Chromium-family browser. It also has no equivalent of
            // Chromium's --window-position, so it can only be corrected after the fact below.
            startInfo.ArgumentList.Add("-kiosk");
            startInfo.ArgumentList.Add("-profile");
            startInfo.ArgumentList.Add(profileDir);
            startInfo.ArgumentList.Add("-new-instance");
            startInfo.ArgumentList.Add(url);
        }
        else
        {
            // Deliberately NOT passing --window-position/--window-size: those are interpreted by
            // Chromium as DIP coordinates in its own internal multi-monitor layout, not raw
            // physical pixels — on a system where the kiosk display and the main display have
            // different DPI scaling, physical-pixel bounds get reinterpreted and can land the
            // window on the wrong monitor (this is exactly what happened when they were passed
            // here). SetWindowPos below has no such ambiguity: it's always physical pixels at the
            // OS level, so positioning is done there instead, post-launch.
            startInfo.ArgumentList.Add("--kiosk");
            startInfo.ArgumentList.Add("--new-window");
            startInfo.ArgumentList.Add("--no-first-run");
            startInfo.ArgumentList.Add($"--user-data-dir={profileDir}");
            startInfo.ArgumentList.Add(url);
        }

        var launchStarted = DateTime.Now;
        var launchedProcess = Process.Start(startInfo);
        if (launchedProcess is null) return null;

        var process = await WaitForBrowserProcessAsync(launchedProcess, browserName, launchStarted, TimeSpan.FromSeconds(15));
        if (process is null) return null;

        var hwnd = process.MainWindowHandle;
        if (hwnd != IntPtr.Zero)
        {
            // Kiosk/fullscreen transition happens asynchronously right after the window is
            // created, and can override a single post-launch SetWindowPos if it lands mid-
            // transition. Reasserting repeatedly for a few seconds reliably wins that race
            // instead of guessing the one right instant to catch it.
            for (int i = 0; i < 8; i++)
            {
                OwnAndPosition(hwnd, ownerHwnd, bounds);
                await Task.Delay(300);
            }
        }

        return process;
    }

    // Resolves the user's actual default browser via the same registry path Windows itself
    // consults (UserChoice's ProgId, then that ProgId's shell\open\command) rather than
    // ShellExecute — ShellExecute can't be handed --kiosk/--user-data-dir, it just invokes the
    // registered handler with the bare URL. Firefox and every Chromium-family browser understand
    // a kiosk flag; anything else (or a resolution failure) falls back to Edge, which ships with
    // every supported Windows install.
    private static string ResolveBrowserExecutable()
    {
        string? exePath = TryGetDefaultBrowserPath();
        if (exePath is not null)
        {
            string name = Path.GetFileNameWithoutExtension(exePath);
            if (ChromiumProcessNames.Contains(name) || name.Equals("firefox", StringComparison.OrdinalIgnoreCase))
                return exePath;
        }

        return FindEdgeFallback()
            ?? exePath
            ?? throw new InvalidOperationException("No usable browser found: couldn't resolve the default browser and Edge isn't installed.");
    }

    private static string? TryGetDefaultBrowserPath()
    {
        using var userChoiceKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
        if (userChoiceKey?.GetValue("ProgId") is not string progId || string.IsNullOrEmpty(progId)) return null;

        using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
        if (commandKey?.GetValue(null) is not string command || string.IsNullOrEmpty(command)) return null;

        command = command.Trim();
        string candidate;
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end < 0) return null;
            candidate = command[1..end];
        }
        else
        {
            int space = command.IndexOf(' ');
            candidate = space > 0 ? command[..space] : command;
        }

        return File.Exists(candidate) ? candidate : null;
    }

    private static string? FindEdgeFallback()
    {
        return new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(Environment.GetFolderPath)
            .Select(root => Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"))
            .FirstOrDefault(File.Exists);
    }

    // The process Process.Start hands back isn't always the one that ends up owning the window.
    // Chromium-family browsers with an isolated --user-data-dir keep it as the same process the
    // whole way, so the fast path (poll its own MainWindowHandle) is all they ever need. Firefox
    // doesn't: even with -new-instance forcing a genuinely separate instance, firefox.exe acts as
    // a launcher that forks the real browser process and exits (cleanly, code 0) almost
    // immediately — confirmed live, the tracked process was gone before it ever got a window,
    // while the actual browser window showed up seconds later under a different PID entirely. If
    // the launched process exits without ever getting a window, fall back to searching for a
    // same-named process that started at (or after) launch time and has one — a launch-time
    // cutoff so this can't accidentally pick up one of the user's own pre-existing everyday
    // browser windows running under the same process name.
    private static async Task<Process?> WaitForBrowserProcessAsync(Process launchedProcess, string processName, DateTime launchStarted, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            launchedProcess.Refresh();
            if (!launchedProcess.HasExited)
            {
                if (launchedProcess.MainWindowHandle != IntPtr.Zero) return launchedProcess;
            }
            else
            {
                foreach (var candidate in Process.GetProcessesByName(processName))
                {
                    if (IsRecentlyLaunchedWithWindow(candidate, launchStarted)) return candidate;
                }
            }

            await Task.Delay(150);
        }

        return null;
    }

    private static bool IsRecentlyLaunchedWithWindow(Process candidate, DateTime launchStarted)
    {
        try
        {
            return candidate.StartTime >= launchStarted.AddSeconds(-1) && candidate.MainWindowHandle != IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            // StartTime throws for processes that exited between GetProcessesByName and here, or
            // ones we don't have permission to query (e.g. a differently-elevated instance).
            return false;
        }
    }

    // Making the browser window "owned" by MainWindow keeps it above MainWindow in z-order even
    // though MainWindow is forced topmost — an owned window is always kept above its owner
    // regardless of the topmost/normal band split, the same mechanism WPF's own Window.Owner uses
    // for windows in this process. Positioned onto the kiosk's configured target monitor (not
    // wherever the browser happened to open it), matching MainWindow's own placement.
    private static void OwnAndPosition(IntPtr hwnd, IntPtr ownerHwnd, System.Drawing.Rectangle bounds)
    {
        SetWindowLongPtr(hwnd, GwlpHwndParent, ownerHwnd);
        SetWindowPos(hwnd, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate);
    }
}
