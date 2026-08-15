using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlDeck.Services;

// Implements IMMNotificationClient itself (rather than depending on AudioOutputService/
// AudioSwitcher) so it re-targets whenever the default output device changes — e.g. via the
// MediaWidget output-device picker, or the user unplugging headphones. Without this, _device stays
// pinned to whichever endpoint was default at construction time, so Volume/IsMuted silently keep
// reading and writing the old, now-inactive device after a switch.
internal sealed class AudioService : IDisposable, IMMNotificationClient
{
    // OnDefaultDeviceChanged fires on a COM callback thread and disposes/reassigns _device; Volume/
    // IsMuted are read and written from the UI thread. Without this lock, switching output and then
    // immediately touching the slider/mute button has a real window where the UI thread calls into
    // a _device that's mid-dispose on the other thread — a native use-after-dispose that crashes
    // the process outright rather than throwing a catchable exception.
    private readonly object _lock = new();
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;

    public event Action<float, bool>? VolumeChanged;

    public AudioService()
    {
        _device = GetDefaultRenderDevice();
        Subscribe(_device);
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public float Volume
    {
        get
        {
            lock (_lock) return _device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0f;
        }
        set
        {
            lock (_lock)
            {
                if (_device is null) return;
                _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
            }
        }
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

    private MMDevice? GetDefaultRenderDevice()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (COMException)
        {
            // No active render device (e.g. output was just unplugged) — Volume/IsMuted's
            // null-conditional getters already fall back sensibly until one reappears.
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
    private void OnVolumeNotification(AudioVolumeNotificationData data)
        => VolumeChanged?.Invoke(data.MasterVolume, data.Muted);

    // IMMNotificationClient — fires on a COM callback thread, synchronously, from inside whatever
    // native call is still in the middle of setting the new default device. Re-querying the device
    // enumerator (GetDefaultRenderDevice) right here is a second synchronous COM call back into
    // that same audio service mid-call, which can deadlock waiting on a lock the original call
    // already holds — this froze the app. Task.Run defers the actual swap to a fresh thread-pool
    // work item, letting the triggering call's stack unwind first.
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != DataFlow.Render || role != Role.Multimedia) return;

        Task.Run(() =>
        {
            lock (_lock)
            {
                Unsubscribe(_device);
                _device?.Dispose();
                _device = GetDefaultRenderDevice();
                Subscribe(_device);
            }

            // Same event MediaWidget already listens to for live volume/mute sync — reuse it so
            // the slider, mute button, and speaker-wave icon all refresh to the new device's
            // actual state.
            VolumeChanged?.Invoke(Volume, IsMuted);
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
