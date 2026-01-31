# Item Icons

This folder contains icon templates used for item recognition via OpenCV template matching.

## How It Works

When you scan an item, the overlay:
1. Captures a region around your cursor (3× the icon size)
2. Compares against all icon templates using OpenCV `MatchTemplate`
3. If pixel matching confidence is low (<70%), uses Canny edge detection for background-independent matching
4. Returns the best match above 50% confidence, or falls back to OCR

## Icon Requirements

| Property | Requirement |
|----------|-------------|
| **Format** | PNG (with transparency) |
| **Size** | 96×96 pixels (automatically resized to match resolution) |
| **Background** | Transparent |
| **Naming** | Must match the item name in `items.json` |

## File Naming Convention

Icon filenames should match the item's internal ID or display name:
- `Broken_Electronics.png`
- `Rubber_Parts.png`
- `Advanced_Electrical_Component.png`

The matcher uses the filename (without extension) to map to database items.

## Adding New Icons

1. **Extract the icon** from game files or screenshots
2. **Remove the background** - icons should have transparent backgrounds
3. **Resize to 96×96** pixels
4. **Save as PNG** with the exact item name
5. **Update `items.json`** if the item doesn't exist in the database

## Icon Sources

Icons in this folder were sourced from:
- [RaidTheory arc-raiders-data](https://github.com/RaidTheory/arc-raiders-data)
- Community contributions

## Resolution Scaling

Icons are stored at 96×96 (4K size) and automatically resized based on the selected resolution preset:
- **1080p**: Scaled to 48×48
- **1440p**: Scaled to 62×62
- **4K**: Used at native 96×96

## Edge Detection

For better matching on different backgrounds, the overlay also generates Canny edge maps of each icon at startup. This allows matching:
- Selected items (white/bright background)
- Unselected items (dark background)
- Items partially obscured by cursor

Edge-based matching is used as a fallback when standard pixel matching has low confidence.
