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
        if (!GetWindowRect(window, out var frame) || frame.Right <= frame.Left || frame.Bottom <= frame.Top)
        {
            throw new InvalidOperationException("The window has no area to capture.");
        }

        using var full = new Bitmap(frame.Right - frame.Left, frame.Bottom - frame.Top, PixelFormat.Format32bppArgb);
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
            full.Save(path, ImageFormat.Png);
            return;
        }

        var crop = Rectangle.Intersect(new Rectangle(area.X - frame.Left, area.Y - frame.Top, area.Width, area.Height),
            new Rectangle(0, 0, full.Width, full.Height));
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            full.Save(path, ImageFormat.Png);
            return;
        }

        using var part = full.Clone(crop, full.PixelFormat);
        part.Save(path, ImageFormat.Png);
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
