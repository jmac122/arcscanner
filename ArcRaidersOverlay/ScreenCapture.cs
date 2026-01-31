using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ArcRaidersOverlay;

public class ScreenCapture
{
    #region Win32 API for cursor position

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    #endregion

    /// <summary>
    /// Gets the current mouse cursor position.
    /// </summary>
    public static System.Drawing.Point GetCursorPosition()
    {
        if (GetCursorPos(out POINT point))
        {
            return new System.Drawing.Point(point.X, point.Y);
        }
        return System.Drawing.Point.Empty;
    }

    /// <summary>
    /// Captures a region of the screen.
    /// </summary>
    /// <param name="region">The screen region to capture.</param>
    /// <returns>A bitmap of the captured region.</returns>
    public Bitmap CaptureRegion(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentException("Region must have positive dimensions.");
        }

        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);

        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Captures a region from a RegionConfig.
    /// </summary>
    public Bitmap CaptureRegion(RegionConfig config)
    {
        return CaptureRegion(new Rectangle(config.X, config.Y, config.Width, config.Height));
    }

    /// <summary>
    /// Captures a region centered on a specific position.
    /// </summary>
    /// <param name="centerX">Center X coordinate</param>
    /// <param name="centerY">Center Y coordinate</param>
    /// <param name="width">Width of capture region</param>
    /// <param name="height">Height of capture region</param>
    /// <returns>A bitmap of the captured region.</returns>
    public Bitmap CaptureAtPosition(int centerX, int centerY, int width, int height)
    {
        var screenBounds = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(centerX, centerY)).Bounds;
        var captureWidth = Math.Min(width, screenBounds.Width);
        var captureHeight = Math.Min(height, screenBounds.Height);

        // Calculate top-left corner from center point
        int x = centerX - captureWidth / 2;
        int y = centerY - captureHeight / 2;

        // Ensure we don't go off-screen (clamp to screen bounds)
        x = Math.Max(screenBounds.X, Math.Min(x, screenBounds.Right - captureWidth));
        y = Math.Max(screenBounds.Y, Math.Min(y, screenBounds.Bottom - captureHeight));

        return CaptureRegion(new Rectangle(x, y, captureWidth, captureHeight));
    }

    /// <summary>
    /// Captures a region centered on the current cursor position.
    /// </summary>
    /// <param name="width">Width of capture region</param>
    /// <param name="height">Height of capture region</param>
    /// <returns>A bitmap of the captured region.</returns>
    public Bitmap CaptureAtCursor(int width, int height)
    {
        var cursor = GetCursorPosition();
        return CaptureAtPosition(cursor.X, cursor.Y, width, height);
    }

    /// <summary>
    /// Captures a region to the right of the current cursor position.
    /// This is useful for tooltips that appear to the right of the hovered item.
    /// </summary>
    /// <param name="width">Width of capture region</param>
    /// <param name="height">Height of capture region</param>
    /// <param name="offsetX">Horizontal offset from cursor (positive = right)</param>
    /// <param name="offsetY">Vertical offset from cursor (positive = down)</param>
    /// <returns>A bitmap of the captured region.</returns>
    public Bitmap CaptureTooltipAtCursor(int width, int height, int offsetX = 20, int offsetY = -50)
    {
        var cursor = GetCursorPosition();
        var screenBounds = System.Windows.Forms.Screen.FromPoint(cursor).Bounds;

        // Start capture at cursor + offset, tooltip is to the right
        int x = cursor.X + offsetX;
        int y = cursor.Y + offsetY;

        // Clamp to screen bounds
        x = Math.Max(screenBounds.X, Math.Min(x, screenBounds.Right - width));
        y = Math.Max(screenBounds.Y, Math.Min(y, screenBounds.Bottom - height));

        return CaptureRegion(new Rectangle(x, y, width, height));
    }
}
