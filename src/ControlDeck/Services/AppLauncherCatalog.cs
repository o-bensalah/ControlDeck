using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ControlDeck.Services;

// Type is a discriminator for action kinds beyond "run a command" — built-in system actions
// (printscreen/lock/showdesktop/sleep) are their own types since they call into
// SystemActionsService directly rather than launching a process; Command is unused for those and
// defaults to empty. AppLauncherService switches on Type; unrecognized types fail gracefully with
// an error message rather than crashing, so the JSON format can grow without a breaking migration.
// Icon is a plain Unicode glyph/emoji (not an icon-font codepoint) since this file is meant to be
// hand-edited — "paste an emoji" beats "look up a hex code" for a config file. Optional: entries
// without one just show the label alone.
internal sealed record AppLauncherEntry(string Name, string Type, string Command = "", string? Arguments = null, string? Icon = null);

internal static class AppLauncherCatalog
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControlDeck", "app-launchers.json");

    private static readonly AppLauncherEntry[] Defaults =
    {
        new("PrtScn", "printscreen", Icon: "📷"),
        new("Lock", "lock", Icon: "🔒"),
        new("Show Desktop", "showdesktop", Icon: "🖥"),
        new("Sleep", "sleep", Icon: "🌙"),
        new("Task Manager", "command", "taskmgr.exe", Icon: "📊"),
        new("File Explorer", "command", "explorer.exe", Icon: "📁"),
        new("Notepad", "command", "notepad.exe", Icon: "📝"),
        new("Calculator", "command", "calc.exe", Icon: "🧮"),
        new("Paint", "command", "mspaint.exe", Icon: "🎨"),
        new("Command Prompt", "command", "cmd.exe", Icon: "⌨"),
        new("Settings", "command", "ms-settings:", Icon: "⚙"),
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
