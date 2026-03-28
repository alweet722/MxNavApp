# MxNavApp - Navigation Application

A .NET MAUI-based navigation application designed to provide real-time turn-by-turn routing with intelligent detection of off-route, wrong-way, and route deviations. This application works in conjunction with the [MxNavDevice](https://github.com/alweet722/MxNavDevice) firmware for ESP32.

## Features

- **Real-Time Navigation**: Turn-by-turn navigation with multiple instruction types (left, right, sharp turns, roundabouts, U-turns, etc.)
- **Smart Route Detection**: 
  - Off-route detection with adaptive distance thresholds based on vehicle speed
  - Wrong-way detection with angle-based algorithm
  - Automatic reroute detection
- **Speed-Adaptive Parameters**: Navigation thresholds dynamically adjust based on current vehicle speed (0-130 km/h)
- **Bluetooth Connectivity**: Communicates with the MxNavDevice controller via BLE
- **Cross-Platform Support**: Built with .NET MAUI for Windows and other platforms

## Architecture

The application is organized into the following key components:

### Navigation Engine
- **WrongWayDetector**: Detects when the vehicle is heading in the wrong direction with configurable angle thresholds
- **OffRouteDetector**: Identifies when the vehicle deviates from the planned route based on GPS coordinates
- **RouteNavigation**: Manages the current route state (NORMAL, WRONG_WAY, OFF_ROUTE, REROUTE) and step-by-step navigation

### Services
- **NavigationService**: Central service managing navigation logic and device communication
- **Bluetooth Service**: Handles BLE communication with the MxNavDevice controller

### Configuration
- **Constants**: Centralized configuration for navigation thresholds:
  - Off-route detection distance: 30m (city) to 150m (highway)
  - Lookahead distance: 15m (city) to 50m (highway)
  - Configurable speed-dependent parameters for adaptive behavior

## Requirements

- **.NET 10** SDK
- **Visual Studio 2022** or later (with .NET MAUI workload)
- **Android 13+** (primary target platform)
- Bluetooth 4.0+ hardware support

## Getting Started

### Clone the Repository

```bash
git clone https://github.com/alweet722/MxNavApp.git
cd NBNavApp
```

### Build the Project

```bash
dotnet build
```

### Run the Application

```bash
dotnet maui run -f net10.0-android
```

### Run Tests

```bash
dotnet test
```

## Hardware Integration

This application is designed to work with the **[MxNavDevice](https://github.com/alweet722/MxNavDevice)** firmware for ESP32. 

Refer to the MxNavDevice repository for firmware setup and communication protocol details.

## Bluetooth Communication

The application communicates with the MxNavDevice via BLE using the following identifiers:

- **Service UUID**: `6b7b3c93-1fdc-4f5b-97be-14adb4ffbf4d`
- **Navigation UUID**: `6b7b3c94-1fdc-4f5b-97be-14adb4ffbf4d`

## Navigation Thresholds

Speed-dependent parameters are automatically adjusted based on the current vehicle speed:

| Metric | City (0 km/h) | Highway (130 km/h) |
|--------|---------------|-------------------|
| Off-route Detection | 30m | 150m |
| Next Segment Detection | 5m | - |
| Wrong-way Detection | - | 20m |
| Lookahead Distance | 15m | 50m |

These values can be customized in `Common/Util/Constants.cs`.

## Development

### Project Structure

```
NBNavApp/
├── Common/
│   ├── Navigation/          # Core navigation logic
│   │   ├── WrongWayDetector.cs
│   │   ├── OffRouteDetector.cs
│   │   └── RouteNavigation.cs
│   ├── Services/            # Application services
│   │   └── NavigationService.cs
│   └── Util/
│       └── Constants.cs      # Configuration and thresholds
├── ViewModels/              # MVVM view models
├── Views/                   # MAUI pages and controls
├── MauiProgram.cs           # Application startup
└── App.xaml                 # Application resources
```

### Code Style

- Single-statement blocks use opening brace on new line with statement and closing brace inline
- Multi-statement blocks keep braces on separate lines
- Follow standard C# naming conventions

## Branch Structure

- **main**: Stable release branch
- **dev-thresholds**: Active development branch for navigation threshold optimization

## Contributing

Contributions are welcome! Please ensure:

1. Code follows the existing style conventions
2. Changes are tested thoroughly
3. Commits are descriptive and atomic
4. Pull requests reference any related issues

## License

This project is provided as-is for personal and educational use.

## Related Projects

- **[MxNavDevice](https://github.com/alweet722/MxNavDevice)**: Firmware for the navigation controller hardware

## Support

For issues, feature requests, or questions about this application, please open an issue on the GitHub repository.

---

**Note**: This application is actively being developed with a focus on optimizing navigation thresholds and making parameters adaptive based on vehicle speed and driving conditions.
