using NAudio.CoreAudioApi;

namespace ControlDeck.Services;

internal sealed class MicrophoneService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDevice? _device;

    public event Action<bool>? MuteChanged;

    public MicrophoneService()
    {
        // No fallback if there's no capture device (e.g. a desktop with no mic plugged in) —
        // IsMuted/IsMuted-set below already null-guard on _device, same as AudioService.
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
    }

    public bool IsMuted
    {
        get => _device?.AudioEndpointVolume.Mute ?? false;
        set
        {
            if (_device is null) return;
            _device.AudioEndpointVolume.Mute = value;
        }
    }

    // Fires on a COM callback thread, not the UI thread — callers must marshal to the Dispatcher.
    private void OnVolumeNotification(AudioVolumeNotificationData data) => MuteChanged?.Invoke(data.Muted);

    public void Dispose()
    {
        if (_device is not null)
        {
            _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
            _device.Dispose();
        }
        _enumerator.Dispose();
    }
}
