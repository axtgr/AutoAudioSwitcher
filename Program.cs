using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using CoreAudioApi;


class Program : IMMNotificationClient, IDisposable
{
    private readonly string deviceName;
    private const int KEY_PROPERTY_ID = 100;

    private readonly MMDeviceEnumerator enumerator = new();

    private string? fallbackPlaybackId;
    private string? fallbackCaptureId;

    PolicyConfigClient client = new PolicyConfigClient();

    public Program(string deviceName)
    {
        this.deviceName = deviceName;
        enumerator.RegisterEndpointNotificationCallback(this);

        fallbackPlaybackId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        fallbackCaptureId = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia).ID;
    }

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: AutoAudioSwitcher <DeviceName>");
            return;
        }

        using var p = new Program(args[0]);
        Thread.Sleep(Timeout.Infinite);
    }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        var dev = SafeGetDevice(deviceId);
        if (dev == null) return;

        if (dev.DeviceFriendlyName != deviceName || key.propertyId != KEY_PROPERTY_ID)
            return;

        var propertyValue = dev.Properties[key]?.Value;

        if (propertyValue?.ToString() == "1")
        {
            SetAsDefault();
        }
        else if (propertyValue?.ToString() == "0")
        {
            RestorePlaybackFallback();
            RestoreCaptureFallback();
        }
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id)
    {
        if (flow != DataFlow.Render && flow != DataFlow.Capture) return;

        var device = SafeGetDevice(id);
        if (device == null) return;

        if (device.DeviceFriendlyName == deviceName) return;

        if (flow == DataFlow.Render) {
            if (fallbackPlaybackId == id) return;
            fallbackPlaybackId = id;
        }
        else if (flow == DataFlow.Capture) {
            if (fallbackCaptureId == id) return;
            fallbackCaptureId = id;
        }

    }

    public void OnDeviceStateChanged(string id, DeviceState state) { }
    public void OnDeviceAdded(string id) { }
    public void OnDeviceRemoved(string id) { }

    private MMDevice? SafeGetDevice(string id)
    {
        try { return enumerator.GetDevice(id); }
        catch { return null; }
    }

    private void SetAsDefault()
    {
        try
        {
            enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active).ToList().ForEach(device => {
                if (device.DeviceFriendlyName == deviceName) {
                    client.SetDefaultEndpoint(device.ID, ERole.eConsole);
                    client.SetDefaultEndpoint(device.ID, ERole.eMultimedia);
                    client.SetDefaultEndpoint(device.ID, ERole.eCommunications);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error setting default: " + ex.Message);
        }
    }

    private void RestorePlaybackFallback()
    {
        if (fallbackPlaybackId == null) return;

        try
        {
            client.SetDefaultEndpoint(fallbackPlaybackId, ERole.eConsole);
            client.SetDefaultEndpoint(fallbackPlaybackId, ERole.eMultimedia);
            client.SetDefaultEndpoint(fallbackPlaybackId, ERole.eCommunications);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error restoring playback device: " + ex.Message);
        }
    }

    private void RestoreCaptureFallback()
    {
        if (fallbackCaptureId == null) return;

        try
        {
            client.SetDefaultEndpoint(fallbackCaptureId, ERole.eConsole);
            client.SetDefaultEndpoint(fallbackCaptureId, ERole.eMultimedia);
            client.SetDefaultEndpoint(fallbackCaptureId, ERole.eCommunications);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error restoring capture device: " + ex.Message);
        }
    }

    public void Dispose()
    {
        enumerator.UnregisterEndpointNotificationCallback(this);
        enumerator.Dispose();
    }
}
