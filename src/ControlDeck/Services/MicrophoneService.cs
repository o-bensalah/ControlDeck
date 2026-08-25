using NAudio.CoreAudioApi;

namespace ControlDeck.Services;

internal sealed class MicrophoneService : AudioEndpointServiceBase
{
    public event Action<bool>? MuteChanged;

    public MicrophoneService() : base(DataFlow.Capture)
    {
    }

    protected override void OnVolumeNotification(AudioVolumeNotificationData data) => MuteChanged?.Invoke(data.Muted);

    protected override void OnDeviceSwapped() => MuteChanged?.Invoke(IsMuted);
}
