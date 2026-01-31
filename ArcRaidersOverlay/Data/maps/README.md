# Map Images

This folder contains map configuration and images for a future minimap feature.

**Note:** The minimap feature is currently hidden/disabled in the overlay UI. Map images are stored here for future use when the feature is implemented.

## Current Status

The minimap feature requires:
1. High-resolution map images for each game map
2. A way to track player position (not currently available via external means)

Since ARC Raiders doesn't expose player coordinates externally, the minimap would only be useful as a static reference image. The feature is hidden until a practical implementation is determined.

## File Format

- **Format**: PNG (recommended) or JPG
- **Naming**: Use lowercase with hyphens
  - Example: `dam-battlegrounds.png`, `buried-city.png`

## Map Configuration

Maps are defined in `maps.json`:

```json
{
  "maps": [
    {
      "id": "dam-battlegrounds",
      "name": "Dam Battlegrounds",
      "imageFile": "dam-battlegrounds.png"
    }
  ]
}
```

## Creating Map Images

1. Take high-resolution screenshots of the in-game map
2. Crop to show only the map area (remove UI elements)
3. Save with the correct filename
4. Place in this folder

## Recommended Resolution

- Minimum: 800x600 pixels
- Recommended: 1920x1080 pixels or higher
- The overlay will scale images to fit the display area
