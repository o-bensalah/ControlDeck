using NAudio.CoreAudioApi;

namespace ControlDeck.Services;

internal sealed class AudioService : AudioEndpointServiceBase
{
    public event Action<float, bool>? VolumeChanged;

    public AudioService() : base(DataFlow.Render)
    {
    }

    public float Volume
    {
        get
        {
            lock (Lock) return Device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0f;
        }
        set
        {
            lock (Lock)
            {
                if (Device is null) return;
                Device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
            }
        }
    }

    protected override void OnVolumeNotification(AudioVolumeNotificationData data)
        => VolumeChanged?.Invoke(data.MasterVolume, data.Muted);

    // Same event MediaWidget already listens to for live volume/mute sync — reuse it so the
    // slider, mute button, and speaker-wave icon all refresh to the new device's actual state.
    protected override void OnDeviceSwapped() => VolumeChanged?.Invoke(Volume, IsMuted);
}
