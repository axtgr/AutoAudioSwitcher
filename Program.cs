using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using CoreAudioApi;


class Program : IMMNotificationClient, IDisposable
{
    private readonly string deviceName;
    private const int KEY_PROPERTY_ID = 100;
    private readonly object switchLock = new();

    private readonly MMDeviceEnumerator enumerator = new();

    private MMDevice? playbackFallback;
    private MMDevice? captureFallback;

    PolicyConfigClient client = new PolicyConfigClient();

    public Program(string deviceName)
    {
        this.deviceName = deviceName;
        enumerator.RegisterEndpointNotificationCallback(this);

        playbackFallback = FindFallback(DataFlow.Render);
        captureFallback = FindFallback(DataFlow.Capture);

        Console.WriteLine("Watching device " + deviceName + "\n");
        Console.WriteLine("Playback fallback: " + playbackFallback?.FriendlyName);
        Console.WriteLine("Capture fallback: " + captureFallback?.FriendlyName);
    }

    static void Main(string[] args)
    {
        var background = false;
        string? deviceName = null;

        foreach (var arg in args)
        {
            if (arg.Equals("--bg", StringComparison.OrdinalIgnoreCase))
                background = true;
            else if (deviceName == null)
                deviceName = arg;
        }

        if (deviceName == null)
        {
            Console.WriteLine("Usage: AutoAudioSwitcher <DeviceName> [--bg]");
            Console.WriteLine();
            Console.WriteLine("  --bg  Run in the background (no console window)");
            Console.WriteLine();
            Console.WriteLine("Available devices:");
            using var enumerator = new MMDeviceEnumerator();
            foreach (var name in enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                .Select(device => device.DeviceFriendlyName)
                .Distinct())
            {
                Console.WriteLine("- " + name);
            }
            return;
        }

        if (background)
        {
            StartInBackground(deviceName);
            return;
        }

        using var p = new Program(deviceName);
        Thread.Sleep(Timeout.Infinite);
    }

    static void StartInBackground(string deviceName)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            Console.WriteLine("Unable to start in background: could not determine executable path.");
            return;
        }

        var startInfo = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.CurrentDirectory,
        };
        startInfo.ArgumentList.Add(deviceName);

        using var process = Process.Start(startInfo);
    }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        if (key.propertyId != KEY_PROPERTY_ID) return;

        // Callbacks run on a Windows audio worker that holds an internal lock.
        // Switching the default endpoint here deadlocks other WASAPI clients.
        var capturedId = deviceId;
        var capturedKey = key;
        ThreadPool.QueueUserWorkItem(_ => HandlePropertyChange(capturedId, capturedKey));
    }

    private void HandlePropertyChange(string deviceId, PropertyKey key)
    {
        var dev = SafeGetDevice(deviceId);
        if (dev == null || dev.DeviceFriendlyName != deviceName) return;

        var propertyValue = dev.Properties[key]?.Value?.ToString();

        if (propertyValue == "1")
        {
            lock (switchLock)
            {
                SetAsDefault();
            }
        }
        else if (propertyValue == "0")
        {
            lock (switchLock)
            {
                RestorePlaybackFallback();
                RestoreCaptureFallback();
            }
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

    private MMDevice? FindFallback(DataFlow flow)
    {
        try
        {
            var current = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            if (current.DeviceFriendlyName != deviceName)
                return current;
        }
        catch { }

        return enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .FirstOrDefault(device => device.DeviceFriendlyName != deviceName);
    }

    private void SetAsDefault()
    {
        try
        {
            enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                .Where(device => device.DeviceFriendlyName == deviceName)
                .ToList()
                .ForEach(device => {
                    Console.WriteLine("Setting default device to: " + device.FriendlyName);
                    SetEndpointRoles(device.ID);
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error setting default playback device: " + ex.Message);
        }
    }

    private void SetEndpointRoles(string deviceId)
    {
        // TeamSpeak 3.6.x deadlocks if the default communications endpoint changes.
        foreach (var role in new[] { ERole.eConsole, ERole.eMultimedia })
        {
            client.SetDefaultEndpoint(deviceId, role);
        }
    }

    private void RestorePlaybackFallback()
    {
        if (playbackFallback == null || playbackFallback.DeviceFriendlyName == deviceName) return;

        Console.WriteLine("Restoring default playback device to: " + playbackFallback.FriendlyName);

        try
        {
            SetEndpointRoles(playbackFallback.ID);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error restoring default playback device: " + ex.Message);
        }
    }

    private void RestoreCaptureFallback()
    {
        if (captureFallback == null || captureFallback.DeviceFriendlyName == deviceName) return;

        Console.WriteLine("Restoring default capture device to: " + captureFallback.FriendlyName);

        try
        {
            SetEndpointRoles(captureFallback.ID);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error restoring default capture device: " + ex.Message);
        }
    }

    public void Dispose()
    {
        enumerator.UnregisterEndpointNotificationCallback(this);
        enumerator.Dispose();
    }
}
