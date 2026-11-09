using System.Drawing;
using SkiaSharp;

namespace KingshotFindersFortuneBot;

/// <summary>Loads the image that is stored next to a map file (same name, any common image format).</summary>
internal static class MapImage
{
    private static readonly string[] Extensions = [".webp", ".png", ".jpg", ".jpeg", ".bmp"];

    /// <summary>Returns the image scaled down to fit into the given size, or null if there is none or it cannot be read.</summary>
    public static Bitmap? Load(string mapPath, Size maxSize)
    {
        var imagePath = Extensions
            .Select(extension => Path.ChangeExtension(mapPath, extension))
            .FirstOrDefault(File.Exists);
        if (imagePath is null)
            return null;

        try
        {
            using var source = SKBitmap.Decode(imagePath);
            if (source is null)
                return null;

            var scale = Math.Min((double)maxSize.Width / source.Width, (double)maxSize.Height / source.Height);
            var info = new SKImageInfo(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)));
            using var resized = source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            using var decoded = new Bitmap(stream);
            return new Bitmap(decoded);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
