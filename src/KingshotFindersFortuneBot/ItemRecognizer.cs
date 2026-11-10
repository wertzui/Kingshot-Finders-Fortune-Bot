using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace KingshotFindersFortuneBot;

/// <summary>Reads the item names from a grid of buttons using the built-in Windows OCR.</summary>
public sealed class ItemRecognizer
{
    private readonly OcrEngine _engine;

    public ItemRecognizer()
    {
        _engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(
                "No Windows OCR language is installed. Install an English language pack in the Windows settings.");
    }

    /// <summary>Returns the text of each grid cell (row by row, left to right); empty if a cell has no text.</summary>
    public async Task<string[]> ReadItemsAsync(Bitmap image, int columns, int rows)
    {
        var scaled = FitToEngine(image);
        try
        {
            using var software = ToSoftwareBitmap(scaled);
            var result = await _engine.RecognizeAsync(software);
            return SplitIntoCells(result, software.PixelWidth, software.PixelHeight, columns, rows);
        }
        finally
        {
            if (!ReferenceEquals(scaled, image))
                scaled.Dispose();
        }
    }

    private static string[] SplitIntoCells(OcrResult result, int width, int height, int columns, int rows)
    {
        var cells = new List<(double X, double Y, double Height, string Text)>[columns * rows];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = [];

        foreach (var word in result.Lines.SelectMany(l => l.Words))
        {
            var rect = word.BoundingRect;
            var centerX = rect.X + rect.Width / 2;
            var centerY = rect.Y + rect.Height / 2;
            var column = Math.Clamp((int)(centerX / (width / (double)columns)), 0, columns - 1);
            var row = Math.Clamp((int)(centerY / (height / (double)rows)), 0, rows - 1);
            cells[row * columns + column].Add((centerX, centerY, rect.Height, word.Text));
        }

        return cells.Select(JoinInReadingOrder).ToArray();
    }

    private static string JoinInReadingOrder(List<(double X, double Y, double Height, string Text)> words)
    {
        var lines = new List<List<(double X, double Y, double Height, string Text)>>();
        foreach (var word in words.OrderBy(w => w.Y))
        {
            var line = lines.Count > 0 ? lines[^1] : null;
            if (line is not null && word.Y - line[0].Y <= word.Height * 0.6)
                line.Add(word);
            else
                lines.Add([word]);
        }

        return string.Join(' ', lines.SelectMany(l => l.OrderBy(w => w.X)).Select(w => w.Text));
    }

    private static SoftwareBitmap ToSoftwareBitmap(Bitmap bitmap)
    {
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return SoftwareBitmap.CreateCopyFromBuffer(
                bytes.AsBuffer(), BitmapPixelFormat.Bgra8, bitmap.Width, bitmap.Height, BitmapAlphaMode.Premultiplied);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private Bitmap FitToEngine(Bitmap image)
    {
        var max = (double)OcrEngine.MaxImageDimension;
        var factor = Math.Min(1, Math.Min(max / image.Width, max / image.Height));
        if (factor >= 1)
            return image;

        var scaled = new Bitmap((int)(image.Width * factor), (int)(image.Height * factor), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(scaled);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(image, 0, 0, scaled.Width, scaled.Height);
        return scaled;
    }
}

