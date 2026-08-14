using System.Diagnostics;
using System.IO;

namespace ControlDeck.Services;

internal static class AppLauncherService
{
    public static bool TryLaunch(AppLauncherEntry entry, out string? error)
    {
        error = null;
        switch (entry.Type)
        {
            case "command":
                return TryLaunchCommand(entry.Command, entry.Arguments, out error);
            case "printscreen":
                SystemActionsService.PrintScreen();
                return true;
            case "lock":
                SystemActionsService.Lock();
                return true;
            case "showdesktop":
                SystemActionsService.ShowDesktop();
                return true;
            case "sleep":
                SystemActionsService.Sleep();
                return true;
            default:
                error = $"Unknown launcher type \"{entry.Type}\"";
                return false;
        }
    }

    private static bool TryLaunchCommand(string command, string? arguments, out string? error)
    {
        try
        {
            var psi = new ProcessStartInfo(command) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(arguments)) psi.Arguments = arguments;
            Process.Start(psi);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            error = ex.Message;
            return false;
        }
    }
}
