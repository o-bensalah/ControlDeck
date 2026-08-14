using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ControlDeck.Services;

internal sealed record StreamingServiceEntry(string Name, string Url);

internal static class StreamingServiceCatalog
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControlDeck", "streaming-services.json");

    private static readonly StreamingServiceEntry[] Defaults =
    {
        new("Netflix", "https://www.netflix.com/"),
        new("Prime Video", "https://www.primevideo.com/"),
        new("Disney+", "https://www.disneyplus.com/"),
        new("Crave", "https://www.crave.ca/"),
        new("YouTube", "https://www.youtube.com/"),
        new("Apple TV+", "https://tv.apple.com/"),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Reads %LOCALAPPDATA%\ControlDeck\streaming-services.json so the button list is editable
    // without rebuilding the app. If it's missing, a template is written with the defaults so
    // there's something to edit; if it's malformed, defaults are used for this session instead
    // of crashing the page.
    public static IReadOnlyList<StreamingServiceEntry> Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var entries = JsonSerializer.Deserialize<StreamingServiceEntry[]>(File.ReadAllText(ConfigPath), JsonOptions);
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
