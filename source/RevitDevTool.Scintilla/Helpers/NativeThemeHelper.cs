using System.Runtime.InteropServices;
namespace RevitDevTool.Scintilla.Helpers;

internal static class NativeThemeHelper
{
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    internal static void ApplyNativeTheme(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero)
            return;

        _ = SetWindowTheme(hwnd, isDark ? "DarkMode_Explorer" : "Explorer", null);
        var useDarkMode = isDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
    }
}
