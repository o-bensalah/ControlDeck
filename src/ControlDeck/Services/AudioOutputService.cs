using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

namespace ControlDeck.Services;

internal sealed record AudioOutputDevice(Guid Id, string Name, bool IsDefault);

// Windows has no public API to *set* the default output device, only to read it — this wraps
// AudioSwitcher.AudioApi.CoreAudio, which drives the same undocumented IPolicyConfig COM interface
// every third-party audio-switcher app (EarTrumpet, etc.) relies on for this.
internal sealed class AudioOutputService : IDisposable
{
    private readonly CoreAudioController _controller = new();

    public IReadOnlyList<AudioOutputDevice> GetPlaybackDevices() =>
        _controller.GetPlaybackDevices(DeviceState.Active)
            .Select(d => new AudioOutputDevice(d.Id, d.FullName, d.IsDefaultDevice))
            .ToList();

    public async Task SetDefaultAsync(Guid id)
    {
        var device = _controller.GetPlaybackDevices(DeviceState.Active).FirstOrDefault(d => d.Id == id);
        if (device is not null) await _controller.SetDefaultDeviceAsync(device);
    }

    public void Dispose() => _controller.Dispose();
}
