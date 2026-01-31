using System.IO;
using OpenCvSharp;
using Newtonsoft.Json;

namespace ArcRaidersOverlay;

/// <summary>
/// Manages icon-based item matching using OpenCV template matching.
/// Similar to RatEye's approach but simplified for Arc Raiders.
/// </summary>
public class IconManager : IDisposable
{
    private readonly Dictionary<string, Mat> _icons;
    private readonly Dictionary<string, string> _iconToItemName;
    private bool _disposed;
    private static readonly string LogFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "overlay.log");

    /// <summary>
    /// Gets the number of loaded icons.
    /// </summary>
    public int IconCount => _icons.Count;

    /// <summary>
    /// Gets whether any icons are loaded.
    /// </summary>
    public bool IsReady => _icons.Count > 0;

    public IconManager()
    {
        _icons = new Dictionary<string, Mat>(StringComparer.OrdinalIgnoreCase);
        _iconToItemName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        LoadIcons();
    }

    private void LoadIcons()
    {
        var iconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "icons");
        var iconsJsonPath = Path.Combine(iconsPath, "icons.json");

        if (!Directory.Exists(iconsPath))
        {
            LogMessage($"Icons directory not found: {iconsPath}");
            return;
        }

        // Load icons.json mapping if it exists
        if (File.Exists(iconsJsonPath))
        {
            try
            {
                var json = File.ReadAllText(iconsJsonPath);
                var mapping = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (mapping != null)
                {
                    foreach (var kvp in mapping)
                    {
                        _iconToItemName[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading icons.json: {ex.Message}");
            }
        }

        // Load all PNG files
        var iconFiles = Directory.GetFiles(iconsPath, "*.png");
        var loaded = 0;

        foreach (var iconFile in iconFiles)
        {
            try
            {
                var fileName = Path.GetFileName(iconFile);
                var mat = Cv2.ImRead(iconFile, ImreadModes.Color);

                if (mat.Empty())
                {
                    LogMessage($"Failed to load icon (empty): {fileName}");
                    continue;
                }

                // Resize icons to a standard size for faster matching (64x64)
                var resized = new Mat();
                Cv2.Resize(mat, resized, new Size(64, 64), interpolation: InterpolationFlags.Area);
                mat.Dispose();

                _icons[fileName] = resized;

                // If no mapping exists, derive item name from filename
                if (!_iconToItemName.ContainsKey(fileName))
                {
                    var itemName = Path.GetFileNameWithoutExtension(fileName);
                    _iconToItemName[fileName] = itemName;
                }

                loaded++;
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading icon {iconFile}: {ex.Message}");
            }
        }

        LogMessage($"Loaded {loaded} icons for template matching");
    }

    /// <summary>
    /// Matches a captured bitmap against loaded icons using template matching.
    /// </summary>
    /// <param name="captured">The captured screen region (should contain an item icon)</param>
    /// <returns>The matched item name and confidence score (0-1)</returns>
    public (string? itemName, float confidence) MatchIcon(System.Drawing.Bitmap captured)
    {
        if (_icons.Count == 0)
        {
            return (null, 0);
        }

        try
        {
            // Convert System.Drawing.Bitmap to OpenCV Mat
            using var sourceMat = BitmapToMat(captured);
            if (sourceMat.Empty())
            {
                return (null, 0);
            }

            // Resize source to match icon size (64x64)
            using var resizedSource = new Mat();
            Cv2.Resize(sourceMat, resizedSource, new Size(64, 64), interpolation: InterpolationFlags.Area);

            string? bestMatch = null;
            float bestConfidence = 0;

            // Compare against each icon using template matching
            foreach (var kvp in _icons)
            {
                var iconFileName = kvp.Key;
                var iconMat = kvp.Value;

                try
                {
                    // Use normalized cross-correlation
                    using var result = new Mat();
                    Cv2.MatchTemplate(resizedSource, iconMat, result, TemplateMatchModes.CCoeffNormed);

                    result.MinMaxLoc(out _, out double maxVal, out _, out _);
                    var confidence = (float)maxVal;

                    if (confidence > bestConfidence)
                    {
                        bestConfidence = confidence;
                        bestMatch = iconFileName;
                    }
                }
                catch
                {
                    // Skip this icon if matching fails
                }
            }

            // Only return if confidence is above threshold (0.7 = 70%)
            if (bestConfidence >= 0.7f && bestMatch != null)
            {
                var itemName = _iconToItemName.TryGetValue(bestMatch, out var name)
                    ? name
                    : Path.GetFileNameWithoutExtension(bestMatch);

                LogMessage($"Icon match: '{itemName}' (confidence: {bestConfidence:P0})");
                return (itemName, bestConfidence);
            }

            return (null, bestConfidence);
        }
        catch (Exception ex)
        {
            LogMessage($"Icon matching error: {ex.Message}");
            return (null, 0);
        }
    }

    /// <summary>
    /// Matches a captured bitmap, trying to find an icon within a larger region.
    /// Useful when the capture might include more than just the icon.
    /// </summary>
    public (string? itemName, float confidence, System.Drawing.Point location) MatchIconInRegion(System.Drawing.Bitmap captured)
    {
        if (_icons.Count == 0)
        {
            return (null, 0, System.Drawing.Point.Empty);
        }

        try
        {
            using var sourceMat = BitmapToMat(captured);
            if (sourceMat.Empty())
            {
                return (null, 0, System.Drawing.Point.Empty);
            }

            string? bestMatch = null;
            float bestConfidence = 0;
            var bestLocation = System.Drawing.Point.Empty;

            foreach (var kvp in _icons)
            {
                var iconFileName = kvp.Key;
                var iconMat = kvp.Value;

                // Skip if source is smaller than icon
                if (sourceMat.Width < iconMat.Width || sourceMat.Height < iconMat.Height)
                    continue;

                try
                {
                    using var result = new Mat();
                    Cv2.MatchTemplate(sourceMat, iconMat, result, TemplateMatchModes.CCoeffNormed);

                    result.MinMaxLoc(out _, out double maxVal, out _, out var maxLoc);
                    var confidence = (float)maxVal;

                    if (confidence > bestConfidence)
                    {
                        bestConfidence = confidence;
                        bestMatch = iconFileName;
                        bestLocation = new System.Drawing.Point(maxLoc.X, maxLoc.Y);
                    }
                }
                catch
                {
                    // Skip this icon if matching fails
                }
            }

            if (bestConfidence >= 0.7f && bestMatch != null)
            {
                var itemName = _iconToItemName.TryGetValue(bestMatch, out var name)
                    ? name
                    : Path.GetFileNameWithoutExtension(bestMatch);

                return (itemName, bestConfidence, bestLocation);
            }

            return (null, bestConfidence, bestLocation);
        }
        catch (Exception ex)
        {
            LogMessage($"Icon region matching error: {ex.Message}");
            return (null, 0, System.Drawing.Point.Empty);
        }
    }

    /// <summary>
    /// Converts a System.Drawing.Bitmap to OpenCV Mat.
    /// </summary>
    private static Mat BitmapToMat(System.Drawing.Bitmap bitmap)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        try
        {
            var mat = new Mat(bitmap.Height, bitmap.Width, MatType.CV_8UC3, bmpData.Scan0, bmpData.Stride);
            return mat.Clone(); // Clone to own the data
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }

    private static void LogMessage(string message)
    {
        try
        {
            var logLine = $"[{DateTime.Now:HH:mm:ss}] [IconManager] {message}\n";
            File.AppendAllText(LogFilePath, logLine);
            System.Diagnostics.Debug.WriteLine($"[IconManager] {message}");
        }
        catch { /* Ignore logging errors */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var mat in _icons.Values)
        {
            mat.Dispose();
        }
        _icons.Clear();

        GC.SuppressFinalize(this);
    }
}
