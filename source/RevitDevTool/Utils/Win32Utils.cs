using System.Runtime.InteropServices;
namespace RevitDevTool.Utils;

public static class Win32Utils
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    
    public static void SetImmersiveDarkMode(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        _ = SetWindowTheme(hwnd, isDark ? "DarkMode_Explorer" : "Explorer", null);
    }
}