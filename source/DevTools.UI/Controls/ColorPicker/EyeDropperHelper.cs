using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

internal static class EyeDropperHelper
{
    private const int SrcCopy = 0x00CC0020;
    private const int CaptureBlt = 0x40000000;

    public static BitmapSource CaptureRegion(Int32Rect region)
    {
        var desktopWindow = GetDesktopWindow();
        var desktopDc = GetWindowDC(desktopWindow);
        var memoryDc = CreateCompatibleDC(desktopDc);
        var bitmap = CreateCompatibleBitmap(desktopDc, region.Width, region.Height);
        var oldBitmap = SelectObject(memoryDc, bitmap);
        var success = BitBlt(memoryDc, 0, 0, region.Width, region.Height, desktopDc, region.X, region.Y, SrcCopy | CaptureBlt);

        try
        {
            if (!success)
            {
                throw new Win32Exception();
            }

            var result = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            result.Freeze();
            return result;
        }
        finally
        {
            SelectObject(memoryDc, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(desktopWindow, desktopDc);
        }
    }

    public static Color GetPixelColor(Point point)
    {
        var hdc = GetDC(IntPtr.Zero);
        var pixel = GetPixel(hdc, (int)Math.Round(point.X), (int)Math.Round(point.Y));
        ReleaseDC(IntPtr.Zero, hdc);
        return Color.FromRgb(
            (byte)(pixel & 0x000000FF),
            (byte)((pixel & 0x0000FF00) >> 8),
            (byte)((pixel & 0x00FF0000) >> 16));
    }

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nxDest, int nyDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
}
