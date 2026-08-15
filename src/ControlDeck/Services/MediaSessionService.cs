using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ControlDeck.Services;

internal sealed record MediaInfo(bool IsPlaying, string? Title, string? Artist, IRandomAccessStreamReference? Thumbnail);

internal sealed class MediaSessionService
{
    public async Task<MediaInfo?> GetMediaInfoAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session is null) return null;

        bool isPlaying = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var props = await session.TryGetMediaPropertiesAsync();

        return new MediaInfo(isPlaying, props?.Title, props?.Artist, props?.Thumbnail);
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session is not null) await session.TryTogglePlayPauseAsync();
    }

    public async Task SkipNextAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session is not null) await session.TrySkipNextAsync();
    }

    public async Task SkipPreviousAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session is not null) await session.TrySkipPreviousAsync();
    }

    private static async Task<GlobalSystemMediaTransportControlsSession?> GetCurrentSessionAsync()
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return manager.GetCurrentSession();
    }
}
