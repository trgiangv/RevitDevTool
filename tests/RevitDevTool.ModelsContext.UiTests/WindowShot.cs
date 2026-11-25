using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Rectangle = System.Drawing.Rectangle;

namespace RevitDevTool.ModelsContext.UiTests;

internal static class WindowShot
{
    private const uint RenderFullContent = 2;

    public static void Save(IntPtr window, string path, Rectangle? screenArea = null)
    {
        using var image = Capture(window, screenArea);
        image.Save(path, ImageFormat.Png);
    }

    public static Bitmap Capture(IntPtr window, Rectangle? screenArea = null)
    {
        if (!GetWindowRect(window, out var frame) || frame.Right <= frame.Left || frame.Bottom <= frame.Top)
        {
            throw new InvalidOperationException("The window has no area to capture.");
        }

        var full = new Bitmap(frame.Right - frame.Left, frame.Bottom - frame.Top, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(full))
        {
            var context = graphics.GetHdc();
            try
            {
                if (!PrintWindow(window, context, RenderFullContent))
                {
                    throw new InvalidOperationException("PrintWindow failed.");
                }
            }
            finally
            {
                graphics.ReleaseHdc(context);
            }
        }

        if (screenArea is not { } area)
        {
            return full;
        }

        var crop = Rectangle.Intersect(new Rectangle(area.X - frame.Left, area.Y - frame.Top, area.Width, area.Height),
            new Rectangle(0, 0, full.Width, full.Height));
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            return full;
        }

        using (full)
        {
            return full.Clone(crop, full.PixelFormat);
        }
    }

    public static int CountHue(Bitmap image, Color target, float degrees)
    {
        var hue = target.GetHue();
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image.GetPixel(x, y);
                var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var distance = Math.Abs(pixel.GetHue() - hue);
                if (max >= 64 && max - min >= max / 2 && Math.Min(distance, 360 - distance) <= degrees)
                {
                    count++;
                }
            }
        }

        return count;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Bounds bounds);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr context, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Bounds
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
