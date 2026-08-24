using System.IO;
using System.Reflection;
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

internal sealed record StreamingServiceEntry(string Name, string Url);

// Root of the single config.json — one file for everything the app lets you hand-edit, instead of
// the three separate ones this used to be (app-launchers.json, streaming-services.json,
// display.json — ControlDeckConfig migrates those automatically the first time it runs, see
// TryMigrateLegacyFiles). Every field is optional: a missing/empty section falls back to that
// section's own built-in defaults, independent of the others. DisplayDeviceName/DisplayNumber
// unset (or the configured screen not currently connected) falls back to
// KioskWindowPlacementService's "first non-primary screen" heuristic.
internal sealed record ControlDeckConfigData(
    List<AppLauncherEntry>? AppLaunchers = null,
    List<StreamingServiceEntry>? StreamingServices = null,
    string? DisplayDeviceName = null,
    int? DisplayNumber = null);

internal static class ControlDeckConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControlDeck");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    // Built-in defaults live in Assets/defaults.json, embedded into the assembly at build time
    // (see the EmbeddedResource entry in ControlDeck.csproj) rather than hardcoded here — one
    // source of truth for "what ships out of the box" instead of a C# array that can drift from
    // whatever's documented/edited separately.
    private const string DefaultsResourceName = "ControlDeck.Assets.defaults.json";

    private sealed record DefaultsData(List<AppLauncherEntry>? AppLaunchers, List<StreamingServiceEntry>? StreamingServices);

    private static DefaultsData? _defaults;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Loaded once and reused — AppLaunchers/StreamingServices/Display are each asked for
    // separately (once per feature, at startup), and re-parsing the same file from disk three
    // times for that would be pure waste.
    private static ControlDeckConfigData? _cached;

    public static IReadOnlyList<AppLauncherEntry> LoadAppLaunchers()
        => Load().AppLaunchers is { Count: > 0 } entries ? entries : GetDefaults().AppLaunchers ?? new();

    public static IReadOnlyList<StreamingServiceEntry> LoadStreamingServices()
        => Load().StreamingServices is { Count: > 0 } entries ? entries : GetDefaults().StreamingServices ?? new();

    public static (string? DeviceName, int? DisplayNumber) LoadDisplay()
    {
        var config = Load();
        return (config.DisplayDeviceName, config.DisplayNumber);
    }

    // Windows' DeviceName (e.g. "\\.\DISPLAY2") ends in a number that, for most drivers, matches
    // what Display Settings' "Identify" overlay shows for that monitor.
    public static int? ParseDisplayNumber(string deviceName)
    {
        var digits = new string(deviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var n) ? n : null;
    }

    private static DefaultsData GetDefaults()
    {
        if (_defaults is not null) return _defaults;

        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultsResourceName);
            if (stream is not null)
            {
                var defaults = JsonSerializer.Deserialize<DefaultsData>(stream, JsonOptions);
                if (defaults is not null) return _defaults = defaults;
            }
        }
        catch (JsonException)
        {
        }

        // Unreachable in a correctly built app — defaults.json is embedded at build time — but an
        // empty fallback beats crashing the whole shortcuts/streaming page if it's ever missing.
        return _defaults = new DefaultsData(new List<AppLauncherEntry>(), new List<StreamingServiceEntry>());
    }

    private static ControlDeckConfigData Load()
    {
        if (_cached is not null) return _cached;

        try
        {
            if (File.Exists(ConfigPath))
            {
                var config = JsonSerializer.Deserialize<ControlDeckConfigData>(File.ReadAllText(ConfigPath), JsonOptions);
                if (config is not null) return _cached = config;
            }
            else if (TryMigrateLegacyFiles(out var migrated))
            {
                return _cached = migrated;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
        }

        TryWriteTemplate();
        return _cached = new ControlDeckConfigData();
    }

    // Matches the old, pre-consolidation display.json shape exactly (its keys were "DeviceName"
    // and "DisplayNumber" — the merged file renames the former to DisplayDeviceName to read
    // unambiguously alongside AppLaunchers/StreamingServices).
    private sealed record LegacyDisplayShape(string? DeviceName, int? DisplayNumber);

    // One-time migration from the three separate JSON files this app used before consolidating —
    // reads whichever of them still exist, writes their combined content as the new config.json,
    // and removes the old files so nobody keeps editing one that's no longer read. A no-op (and
    // falls through to TryWriteTemplate) if none of them exist, i.e. a genuinely fresh install.
    private static bool TryMigrateLegacyFiles(out ControlDeckConfigData migrated)
    {
        string legacyAppLaunchers = Path.Combine(ConfigDir, "app-launchers.json");
        string legacyStreaming = Path.Combine(ConfigDir, "streaming-services.json");
        string legacyDisplay = Path.Combine(ConfigDir, "display.json");

        bool anyExist = File.Exists(legacyAppLaunchers) || File.Exists(legacyStreaming) || File.Exists(legacyDisplay);
        if (!anyExist)
        {
            migrated = new ControlDeckConfigData();
            return false;
        }

        List<AppLauncherEntry>? appLaunchers = null;
        List<StreamingServiceEntry>? streamingServices = null;
        string? displayDeviceName = null;
        int? displayNumber = null;

        try
        {
            if (File.Exists(legacyAppLaunchers))
                appLaunchers = JsonSerializer.Deserialize<List<AppLauncherEntry>>(File.ReadAllText(legacyAppLaunchers), JsonOptions);
        }
        catch (JsonException)
        {
        }

        try
        {
            if (File.Exists(legacyStreaming))
                streamingServices = JsonSerializer.Deserialize<List<StreamingServiceEntry>>(File.ReadAllText(legacyStreaming), JsonOptions);
        }
        catch (JsonException)
        {
        }

        try
        {
            if (File.Exists(legacyDisplay))
            {
                var legacy = JsonSerializer.Deserialize<LegacyDisplayShape>(File.ReadAllText(legacyDisplay), JsonOptions);
                displayDeviceName = legacy?.DeviceName;
                displayNumber = legacy?.DisplayNumber;
            }
        }
        catch (JsonException)
        {
        }

        migrated = new ControlDeckConfigData(appLaunchers, streamingServices, displayDeviceName, displayNumber);

        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(migrated, JsonOptions));
            File.Delete(legacyAppLaunchers);
            File.Delete(legacyStreaming);
            File.Delete(legacyDisplay);
        }
        catch (IOException)
        {
        }

        return true;
    }

    // Only writes once, the first time the app runs with no config present at all.
    private static void TryWriteTemplate()
    {
        try
        {
            if (File.Exists(ConfigPath)) return;

            var defaults = GetDefaults();
            var template = new ControlDeckConfigData(defaults.AppLaunchers, defaults.StreamingServices);

            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(template, JsonOptions));
        }
        catch (IOException)
        {
        }
    }
}
