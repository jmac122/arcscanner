using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Tesseract;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

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

        // Set native library search path for single-file publishing support
        // This tells Tesseract where to find leptonica and tesseract DLLs
        TesseractEnviornment.CustomSearchPath = AppDomain.CurrentDomain.BaseDirectory;

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
        // Create result bitmap with same dimensions and pixel format
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, source.Width, source.Height);

        BitmapData? sourceData = null;
        BitmapData? resultData = null;
        var success = false;

        try
        {
            // Lock bits for direct memory access (much faster than GetPixel/SetPixel)
            sourceData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            resultData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int byteCount = sourceData.Stride * source.Height;
            byte[] pixels = new byte[byteCount];

            // Copy source pixels to array
            Marshal.Copy(sourceData.Scan0, pixels, 0, byteCount);

            // Process pixels - apply contrast enhancement for OCR
            // Format is BGRA (4 bytes per pixel)
            for (int i = 0; i < byteCount; i += 4)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                // Alpha at pixels[i + 3] is preserved

                // Calculate luminance
                var luminance = 0.299 * r + 0.587 * g + 0.114 * b;

                // Threshold to black or white for better OCR
                var newValue = (byte)(luminance > 128 ? 255 : 0);

                pixels[i] = newValue;     // B
                pixels[i + 1] = newValue; // G
                pixels[i + 2] = newValue; // R
                // Alpha unchanged
            }

            // Copy processed pixels to result
            Marshal.Copy(pixels, 0, resultData.Scan0, byteCount);
            success = true;
        }
        finally
        {
            if (sourceData != null)
                source.UnlockBits(sourceData);
            if (resultData != null)
                result.UnlockBits(resultData);

            // Dispose result bitmap if processing failed
            if (!success)
                result.Dispose();
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
