using NAudio.CoreAudioApi;

namespace ControlDeck.Services;

internal sealed class AudioService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDevice? _device;

    public event Action<float, bool>? VolumeChanged;

    public AudioService()
    {
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
    }

    public float Volume
    {
        get => _device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0f;
        set
        {
            if (_device is null) return;
            _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
        }
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
    private void OnVolumeNotification(AudioVolumeNotificationData data)
        => VolumeChanged?.Invoke(data.MasterVolume, data.Muted);

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
