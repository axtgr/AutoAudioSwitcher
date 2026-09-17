# Auto Audio Switcher

Some wireless headsets have a minor inconvenience where Windows doesn't automatically switch to them when you turn them on. This program aims to fix that by monitoring events and switching to the new device when its name matches the specified pattern.

Warning: this is vibe-coded. I haven't the slightest idea about .NET, C# or Windows APIs. Use at your own risk.

## Usage

```
AutoAudioSwitcher "Logitech PRO X Wireless Gaming Headset"
```

This runs the program in the background. Add it to Windows startup to run it automatically.

Run with no arguments to print a list of available device names:

```
AutoAudioSwitcher
```

## Development

```
dotnet build -c Release
```

## License

ISC
