using System.Drawing;
using System.Drawing.Imaging;

namespace KingshotFindersFortuneBot;

internal static class ScreenCapture
{
    public static Bitmap Capture(Rectangle region)
    {
        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
        return bitmap;
    }
}
