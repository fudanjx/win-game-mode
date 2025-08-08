# GameMode - Windows Key Blocker & Mouse Button Mapper for Gaming

A lightweight Windows utility that enhances your gaming experience by disabling the Windows key during gameplay and remapping mouse side buttons to custom keyboard shortcuts.

## Features

- **Windows Key Blocker**: Prevents accidental Windows key presses from interrupting your gaming sessions
- **System Tray Integration**: Runs silently in the system tray with visual indicators showing active/inactive state
- **Mouse Button Mapping**: Maps mouse side buttons to custom keyboard shortcuts
- **Gaming Profiles**: Includes specialized profiles for popular games:
  - CSGO: Maps mouse buttons to weapon quick-switch (2→1) with 1ms delay
  - Overwatch: Maps mouse buttons to left shift key
- **Special Gaming Mouse Support**: Enhanced support for gaming mice, including Razer DeathAdder V3 Pro

## How to Use

### Basic Usage

1. **Launch the application**: Run `GameModeApp.exe`
2. **Enable Game Mode**: Right-click the system tray icon and select "Enable Game Mode"
3. **Choose a profile**: Right-click the system tray icon, go to "Key Mapping Profile" and select a profile

### Key Mapping Profiles

- **Disabled**: No button remapping, only Windows key blocking
- **CSGO**: Maps side buttons to keyboard "2" followed by "1" with 1ms delay (perfect for weapon quick-switching)
- **Overwatch**: Maps side buttons to left shift key

### Advanced Input Monitor

If your mouse buttons aren't being detected correctly:

1. Right-click the system tray icon
2. Select "Advanced Input Monitor"
3. Press your mouse buttons to detect their patterns
4. Select the correct patterns and click "Apply Button Selection"

## Building from Source

### Prerequisites

- .NET 9.0 SDK or newer

### Build Commands

```
dotnet build GameModeApp.csproj --configuration Release
```

## Technical Details

- Uses low-level keyboard and mouse hooks via `SetWindowsHookEx` Win32 API
- Implements raw input monitoring for advanced device support
- Custom message filtering for specialized input detection
- Dynamically generates system tray icons
- Runs as a hidden Windows Forms application

## Requirements

- Windows 10 or Windows 11
- .NET 9.0 Runtime

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Thanks to all contributors and testers
- Special thanks to the .NET and Windows Forms communities