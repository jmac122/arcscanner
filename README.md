# ARC Raiders Overlay

A lightweight, OCR-based external overlay tool for ARC Raiders, inspired by [RatScanner](https://github.com/RatScanner/RatScanner).

## Features

- **Transparent Overlay** - Click-through, draggable overlay that stays on top of your game
- **Event Timers** - Real-time event tracking with countdowns (Supply Drops, Storms, Convoys, etc.)
- **Interactive Minimap** - Automatic map detection with POI display
- **Item Scanner** - Press a hotkey to scan items and see detailed tooltips with:
  - Item value
  - Recycle outputs
  - Project uses
  - Rarity and category
- **System Tray Integration** - Runs quietly in the background
- **Calibration Tool** - Easy screen region setup for your resolution

## Requirements

- Windows 10/11
- .NET 8.0 Runtime (included in self-contained builds)
- ARC Raiders game running in windowed or borderless windowed mode

## Installation

### Option 1: Pre-built Release (Recommended)

1. Download the latest release from the [Releases](../../releases) page
2. Extract to a folder of your choice
3. Download `eng.traineddata` from [tessdata_best](https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata)
4. Place `eng.traineddata` in the `Data/tessdata/` folder
5. Run `ArcRaidersOverlay.exe`

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/arcscanner.git
cd arcscanner/ArcRaidersOverlay

# Restore packages and build
dotnet restore
dotnet build

# Or publish as single-file executable
dotnet publish -c Release -r win-x64 --self-contained true
```

## Usage

### First Run Setup

1. Launch the overlay application
2. Right-click the tray icon and select **Settings**
3. Click **Calibrate Events Region** and draw a rectangle around the in-game events panel
4. Click **Calibrate Tooltip Region** and draw a rectangle around where item tooltips appear
5. Save your settings

### Hotkeys

| Hotkey | Action |
|--------|--------|
| `Shift + S` | Scan item under cursor |

### Overlay Controls

- **Lock/Unlock Button** - Toggle click-through mode
- **Minimize Button** - Minimize the overlay
- **Drag** - When unlocked, drag the title bar to reposition

### System Tray Menu

- **Show/Hide Overlay** - Toggle overlay visibility
- **Unlock Overlay** - Make overlay interactive for repositioning
- **Settings** - Open calibration and settings window
- **Exit** - Close the application

## Configuration

Settings are stored in `%AppData%/ArcRaidersOverlay/config.json`:

```json
{
  "TessdataPath": "./Data/tessdata",
  "EventsRegion": { "X": 10, "Y": 100, "Width": 300, "Height": 200 },
  "TooltipRegion": { "X": 960, "Y": 400, "Width": 400, "Height": 300 },
  "EventPollIntervalSeconds": 15,
  "StartWithWindows": false,
  "StartMinimized": false
}
```

## Adding Map Images

1. Take screenshots of in-game maps
2. Save as PNG files with names like `dread_canyon.png`
3. Place in the `Data/maps/` folder

See `Data/maps/README.md` for supported map names.

## Customizing Item Database

Edit `Data/items.json` to add or modify items. Each item supports:

```json
{
  "name": "Item Name",
  "category": "Material",
  "rarity": "Rare",
  "value": 500,
  "description": "Item description",
  "recycleOutputs": { "Material1": 2, "Material2": 1 },
  "projectUses": ["Project 1", "Project 2"],
  "foundIn": ["Map 1", "Map 2"]
}
```

## Technical Details

- **OCR Engine**: Tesseract 5.x via the Tesseract NuGet package
- **Framework**: .NET 8.0 WPF
- **No Memory Reading**: This tool only uses screen capture and OCR - no game memory access
- **Safe to Use**: External overlay that doesn't modify game files or memory

## Troubleshooting

### OCR Not Working

1. Ensure `eng.traineddata` is in the `Data/tessdata/` folder
2. Check that the calibrated regions match the actual UI positions
3. Try recalibrating with the game running

### Overlay Not Visible

1. Check if the game is running in fullscreen (use borderless windowed instead)
2. Right-click tray icon → Show/Hide Overlay
3. Check if overlay is positioned off-screen (reset settings to defaults)

### Hotkey Not Working

1. Ensure the overlay is running (check system tray)
2. Try running as administrator
3. Check if another application is using the same hotkey

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## License

This project is provided as-is for educational purposes. Use at your own risk.

## Disclaimer

This is an unofficial fan-made tool. It is not affiliated with, endorsed by, or connected to Embark Studios or ARC Raiders in any way. Use of this tool may be subject to the game's terms of service.

## Credits

- Inspired by [RatScanner](https://github.com/RatScanner/RatScanner) for Escape from Tarkov
- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) for text recognition
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) for system tray integration
