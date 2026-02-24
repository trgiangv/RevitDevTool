#region Copyright 2025 Simon Vonhoff & Contributors

//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#endregion

using System.Runtime.InteropServices;

namespace Serilog.Sinks.RichTextBoxForms.Native;

// ReSharper disable once PartialTypeWithSinglePart
internal static partial class NativeThemeHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

#if NET7_0_OR_GREATER
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
#else
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
#endif

    internal static void ApplyNativeTheme(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        _ = SetWindowTheme(hwnd, isDark ? "DarkMode_Explorer" : "Explorer", null);
        var useDarkMode = isDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }
}