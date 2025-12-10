using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using CoreAudioApi;


class Program : IMMNotificationClient, IDisposable
{
    private readonly string deviceName;
    private const int KEY_PROPERTY_ID = 100;

    private readonly MMDeviceEnumerator enumerator = new();

    private MMDevice? playbackFallback;
    private MMDevice? captureFallback;
    private bool debug = false;

    PolicyConfigClient client = new PolicyConfigClient();

    public Program(string deviceName, bool debug)
    {
        this.debug = debug;
        this.deviceName = deviceName;
        enumerator.RegisterEndpointNotificationCallback(this);

        playbackFallback = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        captureFallback = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);

        if (debug) {
            Console.WriteLine("Watching device " + deviceName + "\n");
            Console.WriteLine("Playback fallback: " + playbackFallback?.FriendlyName);
            Console.WriteLine("Capture fallback: " + captureFallback?.FriendlyName);
        }
    }

    static void Main(string[] args)
    {
        var debug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        if (args.Length < 1 || args.Length == 1 && debug)
        {
            Console.WriteLine("Usage: AutoAudioSwitcher <DeviceName>");
            return;
        }

        using var p = new Program(args[0], debug);
        Thread.Sleep(Timeout.Infinite);
    }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        var dev = SafeGetDevice(deviceId);
        if (dev == null) return;

        if (debug) Console.WriteLine("Property " + key.propertyId + " of " + dev.DeviceFriendlyName + " changed to " + dev.Properties[key]?.Value?.ToString());

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
            if (playbackFallback?.ID == id) return;
            playbackFallback = device;
        }
        else if (flow == DataFlow.Capture) {
            if (captureFallback?.ID == id) return;
            captureFallback = device;
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
                    if (debug) Console.WriteLine("Setting default device to: " + device.FriendlyName);
                    client.SetDefaultEndpoint(device.ID, ERole.eConsole);
                    client.SetDefaultEndpoint(device.ID, ERole.eMultimedia);
                    client.SetDefaultEndpoint(device.ID, ERole.eCommunications);
                }
            });
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine("Error setting default playback device: " + ex.Message);
        }
    }

    private void RestorePlaybackFallback()
    {
        if (playbackFallback == null) return;

        if (debug) Console.WriteLine("Restoring default playback device to: " + playbackFallback.FriendlyName);


        try
        {
            client.SetDefaultEndpoint(playbackFallback.ID, ERole.eConsole);
            client.SetDefaultEndpoint(playbackFallback.ID, ERole.eMultimedia);
            client.SetDefaultEndpoint(playbackFallback.ID, ERole.eCommunications);
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine("Error restoring default playback device: " + ex.Message);
        }
    }

    private void RestoreCaptureFallback()
    {
        if (captureFallback == null) return;

        if (debug) Console.WriteLine("Restoring default capture device to: " + captureFallback.FriendlyName);

        try
        {
            client.SetDefaultEndpoint(captureFallback.ID, ERole.eConsole);
            client.SetDefaultEndpoint(captureFallback.ID, ERole.eMultimedia);
            client.SetDefaultEndpoint(captureFallback.ID, ERole.eCommunications);
        }
        catch (Exception ex)
        {
            if (debug) Console.WriteLine("Error restoring default capture device: " + ex.Message);
        }
    }

    public void Dispose()
    {
        enumerator.UnregisterEndpointNotificationCallback(this);
        enumerator.Dispose();
    }
}
