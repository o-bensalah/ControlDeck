using Windows.Media.Control;

namespace ControlDeck.Services;

internal sealed class MediaSessionService
{
    public async Task<bool> IsPlayingAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session is null) return false;
        return session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
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
