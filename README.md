# ARC Raiders Overlay

A lightweight, OCR-based external overlay tool for ARC Raiders, inspired by [RatScanner](https://github.com/RatScanner/RatScanner).

## Features

### Event Tracking
- **Live event monitoring** - Automatically detects and displays active game events
- **Event timers** - Color-coded countdown timers (green = active, yellow = soon, red = urgent)
- **Event types** - Tracks Supply Drops, Storms, Convoys, and Extractions

### Item Scanner
- **OCR-based scanning** - Press `Shift+S` to scan item tooltips
- **Instant recommendations** - Color-coded banners tell you to KEEP, SELL, or RECYCLE
- **Recycling efficiency** - Shows percentage value (green 70%+, yellow 50-69%, red <50%)
- **Workshop tracking** - Shows which workshops need each item (Gunsmith, Gear Bench, Medical Lab, Scrappy)
- **Quest item warnings** - Orange highlight for items needed in quests
- **68+ item database** - Comprehensive item data with sell values, recycle outputs, and uses

### Multi-Monitor Support
- **Game window detection** - Automatically finds ARC Raiders on any monitor (detects `PioneerGame`, `ArcRaiders`, and variant process names)
- **Game-relative coordinates** - Calibration works regardless of where game window is positioned
- **Overlay follows game** - Optional auto-positioning when game window moves
- **DPI awareness** - Per-monitor DPI scaling support

### Quality of Life
- **Transparent overlay** - Click-through, draggable overlay that stays on top
- **System tray integration** - Runs quietly in the background
- **Admin elevation detection** - Warns if privilege mismatch with game
- **Start with Windows** - Optional auto-launch on boot

## Requirements

- **OS**: Windows 10/11 (64-bit)
- **.NET Runtime**: .NET 8.0 (included in self-contained builds - no separate install needed)
- **Game Mode**: Windowed or borderless windowed (not exclusive fullscreen)

## Installation

### Option 1: Pre-built Release (Recommended)

1. Download the latest release from the [Releases](../../releases) page
2. Extract the ZIP to a folder (e.g., `C:\Tools\ArcRaidersOverlay`)
3. Download Tesseract language data:
   - Get `eng.traineddata` from [tessdata_best](https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata) (~15 MB)
   - Place it in the `Data/tessdata/` folder
4. Run `ArcRaidersOverlay.exe`

**Folder structure after setup:**
```
ArcRaidersOverlay/
├── ArcRaidersOverlay.exe    ← Run this
└── Data/
    ├── items.json           ← Item database (included)
    ├── tessdata/
    │   └── eng.traineddata  ← Download this
    └── maps/
        └── maps.json        ← Map data (included)
```

**Note:** Settings are saved to `%AppData%/ArcRaidersOverlay/config.json` (created on first run)

### Option 2: Build from Source

**Prerequisites:**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11
- Git (optional)

**Build commands:**
```bash
# Clone the repository
git clone https://github.com/jmac122/arcscanner.git
cd arcscanner/ArcRaidersOverlay

# Restore packages
dotnet restore

# Build for development
dotnet build

# Publish as self-contained single-file EXE (recommended for distribution)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**Output location:** `bin/Release/net8.0-windows/win-x64/publish/`

The published EXE is self-contained (~60-80 MB) and includes the .NET runtime, so users don't need to install .NET separately.

The `Data` folder is automatically copied to the output directory during build.

## Setup Guide

### First-Time Setup

1. **Launch the overlay** - Run `ArcRaidersOverlay.exe`
2. **Start ARC Raiders** - Launch the game through Steam
3. **Open Settings** - Right-click tray icon → Settings

### Calibrate Screen Regions

The overlay needs to know where to look for text on your screen:

#### Events Region
1. In Settings, click **"Calibrate Events Region"**
2. A screenshot of all monitors appears
3. The game window is highlighted with a green dashed border
4. Draw a rectangle around the events/timer area in-game
5. Coordinates are saved relative to the game window

#### Tooltip Region
1. Hover over an item in-game to show its tooltip
2. Click **"Calibrate Tooltip Region"**
3. Draw a rectangle around where item tooltips appear
4. Keep the selection tight around the text for best OCR results

**Tip:** For best OCR accuracy, calibrate regions that contain only text without icons or decorations.

### Settings Reference

| Setting | Description | Default |
|---------|-------------|---------|
| **Use game-relative coordinates** | Stores calibration relative to game window position | On |
| **Overlay follows game window** | Auto-positions overlay when game moves monitors | On |
| **Overlay offset X/Y** | Fine-tune overlay position relative to game | 10, 10 |
| **Start with Windows** | Launch overlay automatically on boot | Off |
| **Start minimized** | Start minimized to system tray | Off |
| **Event Poll Interval** | How often to scan for events (seconds) | 15 |
| **Tessdata Path** | Location of Tesseract language files | ./Data/tessdata |

## Usage

### Hotkeys

| Hotkey | Action |
|--------|--------|
| `Shift + S` | Scan item tooltip |
| `Escape` | Cancel calibration |

### Overlay Controls

- **Drag title bar** - Move overlay position (when unlocked)
- **Lock button** - Toggle click-through mode
- **Minimize button** - Send to system tray

### System Tray Menu

Right-click the system tray icon:
- **Show Overlay** - Bring overlay to front
- **Hide Overlay** - Hide from screen
- **Unlock Overlay** - Make overlay interactive
- **Settings** - Open configuration window
- **Exit** - Close the application

### Reading Item Tooltips

When you scan an item with `Shift+S`, the tooltip shows:

```
┌─────────────────────────────┐
│          RECYCLE            │  ← Recommendation (color-coded)
├─────────────────────────────┤
│ Broken Electronics          │  ← Item name
│ Material - Common           │  ← Category and rarity
├─────────────────────────────┤
│ Sell Value:        70 Φ     │
│ Recycle Efficiency: 92%     │  ← Green = good to recycle
├─────────────────────────────┤
│ Workshop Uses:              │
│   Gunsmith                  │  ← Purple text
├─────────────────────────────┤
│ Recycle Outputs:            │
│   Adv. Electrical: 1        │
└─────────────────────────────┘
```

**Recommendation Colors:**
| Color | Meaning |
|-------|---------|
| Green | **KEEP** - Important component, don't sell |
| Gold | **SELL** - Better value when sold |
| Cyan | **RECYCLE** - Better value when recycled |
| Gray | **SELL OR RECYCLE** - Either option is fine |
| Orange | **KEEP FOR QUESTS** - Quest item, do not sell/recycle |

**Recycle Efficiency Colors:**
| Color | Percentage | Meaning |
|-------|------------|---------|
| Green | 70%+ | Good recycling value |
| Yellow | 50-69% | Moderate value |
| Red | <50% | Better to sell |

## Configuration

### Config File Location

Settings are stored in `%AppData%/ArcRaidersOverlay/config.json`

### Example Configuration

```json
{
  "TessdataPath": "./Data/tessdata",
  "EventsRegion": { "X": 10, "Y": 100, "Width": 300, "Height": 200 },
  "TooltipRegion": { "X": 960, "Y": 400, "Width": 400, "Height": 300 },
  "EventPollIntervalSeconds": 15,
  "UseGameRelativeCoordinates": true,
  "FollowGameWindow": true,
  "OverlayOffsetX": 10,
  "OverlayOffsetY": 10,
  "StartWithWindows": false,
  "StartMinimized": false
}
```

### Customizing Item Database

Edit `Data/items.json` to add or modify items:

```json
{
  "name": "Rubber Parts",
  "category": "Component",
  "rarity": "Common",
  "value": 45,
  "description": "Base component used in workshop upgrades",
  "recycleValuePercent": null,
  "recycleOutputs": null,
  "workshopUses": ["Gear Bench", "Scrappy"],
  "projectUses": [],
  "keepForQuests": false,
  "questUses": [],
  "recommendation": "Keep",
  "foundIn": ["All Maps", "Vehicle Wrecks"]
}
```

**Item Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Item name (must match OCR output) |
| `category` | string | Component, Material, Valuable, Quest, etc. |
| `rarity` | string | Common, Uncommon, Rare, Epic, Legendary |
| `value` | int | Sell value in Φ (phi/credits) |
| `recycleValuePercent` | int? | Recycling efficiency (70+ = good, <50 = sell) |
| `recycleOutputs` | object | Components produced when recycled |
| `workshopUses` | array | Workshop upgrades requiring this item |
| `projectUses` | array | Expedition projects requiring this item |
| `keepForQuests` | bool | If true, shows quest warning |
| `questUses` | array | Quests requiring this item |
| `recommendation` | string | "Keep", "Sell", "Recycle", or "Either" |

### Adding Map Images

1. Take screenshots of in-game maps
2. Save as PNG files (e.g., `dread_canyon.png`)
3. Place in the `Data/maps/` folder

## Technical Details

| Aspect | Details |
|--------|---------|
| **OCR Engine** | Tesseract 5.x via NuGet package |
| **Framework** | .NET 8.0 WPF |
| **Screen Capture** | Win32 API (BitBlt) |
| **Game Detection** | FindWindow + GetWindowRect |
| **Build Output** | Self-contained single-file EXE |

### Safety & Anti-Cheat

This overlay is designed to be **external and passive**:
- Does **NOT** inject into the game process
- Does **NOT** read or modify game memory
- Does **NOT** interact with game input
- Only captures screenshots and reads text via OCR

**EAC (Easy Anti-Cheat) Note:** External overlays that don't interact with the game are generally safe. The tool operates similarly to Discord overlay, OBS, or streaming software. Use at your own discretion.

## Troubleshooting

### Overlay Not Staying On Top

**Cause:** Game running as administrator, overlay is not.

**Solution:** The overlay will detect this on startup and offer to restart with admin privileges. Alternatively:
1. Right-click `ArcRaidersOverlay.exe`
2. Select **Properties** → **Compatibility**
3. Check **Run this program as an administrator**

### OCR Not Detecting Text

**Possible causes and fixes:**

| Problem | Solution |
|---------|----------|
| Tessdata missing | Download `eng.traineddata` and place in `Data/tessdata/` |
| Wrong region | Recalibrate tooltip region in settings |
| Text too small | Increase game resolution or UI scale |
| Region too large | Calibrate tighter around text only |

**Tips for better OCR:**
- Keep calibration region tight around text
- Avoid including icons or decorative elements
- Higher game resolution = better recognition
- Use borderless windowed mode

### Item Not Found in Database

If an item isn't recognized:
1. Check if OCR misread the name (common with similar letters)
2. Add the item to `Data/items.json` manually
3. The overlay uses fuzzy matching (60% similarity threshold)

### Screen Capture Not Working

| Problem | Solution |
|---------|----------|
| Elevation mismatch | Run overlay as administrator |
| Fullscreen exclusive | Use borderless windowed mode |
| Anti-virus blocking | Add overlay to exclusions |
| Multiple monitors | Ensure correct monitor detected |

### Hotkey Not Registering

1. Check if game has focus (hotkeys are global but elevation may block)
2. Ensure no other app is using `Shift+S`
3. Run overlay as administrator if game is elevated

## Building & Development

### Project Structure

```
ArcRaidersOverlay/
├── App.xaml(.cs)              # Application entry, elevation check
├── MainWindow.xaml(.cs)       # System tray management
├── OverlayWindow.xaml(.cs)    # Main overlay UI
├── SettingsWindow.xaml(.cs)   # Configuration UI
├── CalibrationWindow.xaml(.cs)# Region selection UI
├── Models/
│   ├── Item.cs                # Item data model
│   ├── Event.cs               # Game event model
│   └── MapInfo.cs             # Map configuration
├── OcrManager.cs              # Tesseract integration
├── ScreenCapture.cs           # Screen capture utilities
├── EventParser.cs             # Event text parsing
├── DataManager.cs             # Item database + fuzzy matching
├── HotkeyManager.cs           # Global hotkey registration
├── ConfigManager.cs           # Settings persistence
├── GameWindowDetector.cs      # Game window detection
├── GameProcessNames.cs        # Known game executable names
├── Theme.cs                   # UI color constants
├── GlobalUsings.cs            # Shared using directives
├── app.manifest               # UAC settings
└── Data/
    ├── items.json             # Item database (68+ items)
    └── maps/
        └── maps.json          # Map definitions
```

### Build Commands

```bash
# Debug build (for development)
dotnet build

# Release build
dotnet build -c Release

# Publish self-contained single-file EXE
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Publish with compression (smaller file)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

### Development Requirements

- Visual Studio 2022 or VS Code with C# extension
- .NET 8.0 SDK
- Windows 10/11 (WPF is Windows-only)

## Contributing

Contributions welcome! Areas that could use help:
- Expanding the item database with accurate values
- Adding map images for minimap feature
- Improving OCR accuracy for different resolutions
- Testing on various multi-monitor configurations
- Translations/localization

## License

This project is provided as-is for educational purposes. Use at your own risk.

## Disclaimer

This is an unofficial fan-made tool. It is **not** affiliated with, endorsed by, or connected to Embark Studios or ARC Raiders in any way. Use of this tool may be subject to the game's terms of service.

## Credits

- Inspired by [RatScanner](https://github.com/RatScanner/RatScanner) for Escape from Tarkov
- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) for text recognition
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) for system tray integration
- ARC Raiders community for item data and cheat sheets
