namespace RevitDevTool.Theme;

internal static class Win32DarkMode
{
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        _ = SetWindowTheme(hwnd, isDark ? "DarkMode_Explorer" : "Explorer", null);
    }
}