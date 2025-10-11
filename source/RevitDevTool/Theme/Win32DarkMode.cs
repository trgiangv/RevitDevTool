namespace RevitDevTool.Theme ;

internal static class Win32DarkMode
{
    private const int DarkModeBefore20H1 = 19 ;
    private const int DarkMode = 20 ;
    
    [System.Runtime.InteropServices.DllImport("dwmapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public static void SetImmersiveDarkMode( IntPtr hwnd, bool enable )
    {
        if ( hwnd == IntPtr.Zero ) return ;
        var useDark = enable ? 1 : 0 ;
        _ = DwmSetWindowAttribute( hwnd, DarkMode, ref useDark, sizeof( int ) ) ;
        _ = DwmSetWindowAttribute( hwnd, DarkModeBefore20H1, ref useDark, sizeof( int ) ) ;
        SetWindowTheme( hwnd, enable ? "DarkMode_Explorer" : "Explorer", null ) ;
    }
}