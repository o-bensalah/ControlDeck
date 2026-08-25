using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlDeck.Services;

// Shared plumbing for AudioService (render) and MicrophoneService (capture): both need a live
// MMDevice reference that re-targets on default-device changes, and both hit the identical
// threading hazards found while debugging that —
//   - OnDefaultDeviceChanged disposes/reassigns Device on a COM callback thread while IsMuted (and
//     AudioService's Volume) are read/written from the UI thread; unsynchronized, that's a
//     use-after-dispose race that crashes the process outright.
//   - Re-querying the device enumerator synchronously from inside OnDefaultDeviceChanged is a
//     second COM call back into the same audio service mid-call, which can deadlock against the
//     native call still delivering the notification. Task.Run defers the swap to a fresh
//     thread-pool item so that call's stack unwinds first.
// Centralizing this means those fixes only need to exist once.
internal abstract class AudioEndpointServiceBase : IDisposable, IMMNotificationClient
{
    private readonly DataFlow _flow;
    private readonly MMDeviceEnumerator _enumerator = new();

    protected readonly object Lock = new();
    protected MMDevice? Device;

    protected AudioEndpointServiceBase(DataFlow flow)
    {
        _flow = flow;
        Device = GetDefaultDevice();
        Subscribe(Device);
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public bool IsMuted
    {
        get
        {
            lock (Lock) return Device?.AudioEndpointVolume.Mute ?? false;
        }
        set
        {
            lock (Lock)
            {
                if (Device is null) return;
                Device.AudioEndpointVolume.Mute = value;
            }
        }
    }

    private MMDevice? GetDefaultDevice()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(_flow, Role.Multimedia);
        }
        catch (COMException)
        {
            // No active device for this flow (e.g. output/input was just unplugged) — IsMuted's
            // (and Volume's) null-conditional getters already fall back sensibly until one
            // reappears.
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
    protected abstract void OnVolumeNotification(AudioVolumeNotificationData data);

    // IMMNotificationClient — fires on a COM callback thread, synchronously, from inside whatever
    // native call is still in the middle of setting the new default device.
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != _flow || role != Role.Multimedia) return;

        Task.Run(() =>
        {
            lock (Lock)
            {
                Unsubscribe(Device);
                Device?.Dispose();
                Device = GetDefaultDevice();
                Subscribe(Device);
            }

            OnDeviceSwapped();
        });
    }

    // Reuse whatever change event the derived class already exposes, so subscribers (MediaWidget)
    // refresh to the new device's actual state the same way they do for an ordinary mute/volume
    // change.
    protected abstract void OnDeviceSwapped();

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
        lock (Lock)
        {
            Unsubscribe(Device);
            Device?.Dispose();
        }
        _enumerator.Dispose();
    }
}
