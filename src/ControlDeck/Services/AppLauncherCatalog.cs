using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ControlDeck.Services;

// Type is a discriminator for action kinds beyond "run a command" — built-in system actions
// (printscreen/lock/showdesktop/sleep) are their own types since they call into
// SystemActionsService directly rather than launching a process; Command is unused for those and
// defaults to empty. AppLauncherService switches on Type; unrecognized types fail gracefully with
// an error message rather than crashing, so the JSON format can grow without a breaking migration.
internal sealed record AppLauncherEntry(string Name, string Type, string Command = "", string? Arguments = null);

internal static class AppLauncherCatalog
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControlDeck", "app-launchers.json");

    private static readonly AppLauncherEntry[] Defaults =
    {
        new("PrtScn", "printscreen"),
        new("Lock", "lock"),
        new("Show Desktop", "showdesktop"),
        new("Sleep", "sleep"),
        new("Task Manager", "command", "taskmgr.exe"),
        new("File Explorer", "command", "explorer.exe"),
        new("Notepad", "command", "notepad.exe"),
        new("Calculator", "command", "calc.exe"),
        new("Paint", "command", "mspaint.exe"),
        new("Command Prompt", "command", "cmd.exe"),
        new("Settings", "command", "ms-settings:"),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Reads %LOCALAPPDATA%\ControlDeck\app-launchers.json so the button list is editable without
    // rebuilding the app. If it's missing, a template is written with the defaults so there's
    // something to edit; if it's malformed, defaults are used for this session instead of
    // crashing the page.
    public static IReadOnlyList<AppLauncherEntry> Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var entries = JsonSerializer.Deserialize<AppLauncherEntry[]>(File.ReadAllText(ConfigPath), JsonOptions);
                if (entries is { Length: > 0 }) return entries;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
        }

        TryWriteDefaults();
        return Defaults;
    }

    private static void TryWriteDefaults()
    {
        try
        {
            if (File.Exists(ConfigPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Defaults, JsonOptions));
        }
        catch (IOException)
        {
        }
    }
}
