using System.Drawing;
using System.Drawing.Imaging;

namespace ArcRaidersOverlay;

public class ScreenCapture
{
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

        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);

        return bitmap;
    }

    /// <summary>
    /// Captures a region from a RegionConfig.
    /// </summary>
    public Bitmap CaptureRegion(RegionConfig config)
    {
        return CaptureRegion(new Rectangle(config.X, config.Y, config.Width, config.Height));
    }

    /// <summary>
    /// Captures the primary screen.
    /// </summary>
    public Bitmap CaptureFullScreen()
    {
        var screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        var screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

        return CaptureRegion(new Rectangle(0, 0, screenWidth, screenHeight));
    }

    /// <summary>
    /// Captures all screens (virtual screen).
    /// </summary>
    public Bitmap CaptureAllScreens()
    {
        var virtualWidth = (int)System.Windows.SystemParameters.VirtualScreenWidth;
        var virtualHeight = (int)System.Windows.SystemParameters.VirtualScreenHeight;
        var virtualLeft = (int)System.Windows.SystemParameters.VirtualScreenLeft;
        var virtualTop = (int)System.Windows.SystemParameters.VirtualScreenTop;

        return CaptureRegion(new Rectangle(virtualLeft, virtualTop, virtualWidth, virtualHeight));
    }
}
