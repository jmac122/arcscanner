using System.Drawing;
using Tesseract;

namespace ArcRaidersOverlay;

public class OcrManager : IDisposable
{
    private readonly TesseractEngine _engine;
    private bool _disposed;

    public OcrManager(string tessdataPath)
    {
        // Validate tessdata path exists
        if (!Directory.Exists(tessdataPath))
        {
            throw new DirectoryNotFoundException(
                $"Tessdata directory not found: {tessdataPath}");
        }

        // Check for eng.traineddata
        var trainedDataPath = Path.Combine(tessdataPath, "eng.traineddata");
        if (!File.Exists(trainedDataPath))
        {
            throw new FileNotFoundException(
                $"eng.traineddata not found in {tessdataPath}. " +
                "Download from https://github.com/tesseract-ocr/tessdata_best");
        }

        _engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);

        // Configure for game text recognition
        _engine.SetVariable("tessedit_char_whitelist",
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 -:,.()[]'\"@#$%&*+=/<>!?");
        _engine.SetVariable("tessedit_pageseg_mode", "6"); // Assume uniform block of text
        _engine.SetVariable("preserve_interword_spaces", "1");
    }

    public string Recognize(Bitmap bitmap)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OcrManager));

        // Pre-process bitmap for better OCR
        using var processed = PreprocessImage(bitmap);
        using var pix = BitmapToPix(processed);
        using var page = _engine.Process(pix);

        var text = page.GetText();
        var confidence = page.GetMeanConfidence();

        System.Diagnostics.Debug.WriteLine($"OCR Confidence: {confidence:P0}");

        return text.Trim();
    }

    private static Bitmap PreprocessImage(Bitmap source)
    {
        // Create a copy to avoid modifying the original
        var result = new Bitmap(source.Width, source.Height);

        using var graphics = Graphics.FromImage(result);
        graphics.DrawImage(source, 0, 0);

        // Apply simple contrast enhancement for better OCR
        // This is a basic implementation - could be improved with proper image processing
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                var pixel = result.GetPixel(x, y);

                // Calculate luminance
                var luminance = (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);

                // Increase contrast - if above threshold, make white; otherwise make black
                // This helps with game UI text which often has semi-transparent backgrounds
                var newValue = luminance > 128 ? 255 : 0;

                result.SetPixel(x, y, Color.FromArgb(newValue, newValue, newValue));
            }
        }

        return result;
    }

    private static Pix BitmapToPix(Bitmap bitmap)
    {
        // Convert System.Drawing.Bitmap to Tesseract Pix
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;

        return Pix.LoadFromMemory(stream.ToArray());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _engine.Dispose();
        GC.SuppressFinalize(this);
    }
}
