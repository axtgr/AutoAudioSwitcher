# Auto Audio Switcher

Some wireless headsets have a minor inconvenience where Windows doesn't automatically switch to them when you turn them on. This program aims to fix that by monitoring events and switching to the new device when its name matches the specified pattern.

Warning: this is vibe-coded. I haven't the slightest idea about .NET, C# or Windows APIs. Use at your own risk.


## Usage

Run from the console without any arguments:

```
AutoAudioSwitcher
```

This will print a list of available devices:

```
Available devices:
- Logitech PRO X Wireless Gaming Headset
- Realtek USB2.0 Audio
- Logi C525 HD WebCam
```

Pick a device you want to automatically switch to and copy its name. Then run the app with the device name as an argument:

```
AutoAudioSwitcher "Logitech PRO X Wireless Gaming Headset"
```

This will run the program in the foreground, monitor the device events and print them. Try turning the device on/off. The program should print a message, and the default audio device in the system should change.

To run the program in the background, pass `--bg`:

```
AutoAudioSwitcher "Logitech PRO X Wireless Gaming Headset" --bg
```

This should immediately return and let you close the console window. The process, however, should stay visible in Task Manager.

Add the last command to Windows startup to run it automatically on system start.


## Development

```
dotnet build -c Release
```


## License

ISC
