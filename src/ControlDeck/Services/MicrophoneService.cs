using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlDeck.Services;

// Implements IMMNotificationClient so it re-targets whenever the default capture device changes
// (e.g. the user swaps microphones) — same fix as AudioService, for the same reason: without it,
// _device stays pinned to whichever endpoint was default at construction time.
internal sealed class MicrophoneService : IDisposable, IMMNotificationClient
{
    // See AudioService's identical field for why this lock exists — OnDefaultDeviceChanged
    // disposes/reassigns _device on a COM callback thread while IsMuted is read/written from the
    // UI thread; unsynchronized, that's a use-after-dispose race that crashes the process outright.
    private readonly object _lock = new();
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;

    public event Action<bool>? MuteChanged;

    public MicrophoneService()
    {
        _device = GetDefaultCaptureDevice();
        Subscribe(_device);
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public bool IsMuted
    {
        get
        {
            lock (_lock) return _device?.AudioEndpointVolume.Mute ?? false;
        }
        set
        {
            lock (_lock)
            {
                if (_device is null) return;
                _device.AudioEndpointVolume.Mute = value;
            }
        }
    }

    private MMDevice? GetDefaultCaptureDevice()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch (COMException)
        {
            // No active capture device (e.g. no mic plugged in) — IsMuted's null-conditional
            // getter/setter already fall back sensibly until one appears.
            return null;
        }
    }

    private void Subscribe(MMDevice? device)
    {
        if (device is not null) device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
    }

    private void Unsubscribe(MMDevice? device)
    {
        if (device is not null) device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
    }

    // Fires on a COM callback thread, not the UI thread — callers must marshal to the Dispatcher.
    private void OnVolumeNotification(AudioVolumeNotificationData data) => MuteChanged?.Invoke(data.Muted);

    // See AudioService's identical method for why this defers to Task.Run — re-querying the
    // device enumerator synchronously from inside this notification can deadlock against the
    // native call that's still delivering it.
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != DataFlow.Capture || role != Role.Multimedia) return;

        Task.Run(() =>
        {
            lock (_lock)
            {
                Unsubscribe(_device);
                _device?.Dispose();
                _device = GetDefaultCaptureDevice();
                Subscribe(_device);
            }

            MuteChanged?.Invoke(IsMuted);
        });
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
    }

    public void OnDeviceRemoved(string deviceId)
    {
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
    }

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(this);
        lock (_lock)
        {
            Unsubscribe(_device);
            _device?.Dispose();
        }
        _enumerator.Dispose();
    }
}
