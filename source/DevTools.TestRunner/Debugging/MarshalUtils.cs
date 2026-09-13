using System.Runtime.InteropServices;

namespace DevTools.TestRunner.Debugging;

/// <summary>
/// netcore stand-in for netfx <c>Marshal.GetActiveObject</c>.
/// https://github.com/ricaun-io/ricaun.RevitTest/blob/master/ricaun.RevitTest.Console/Revit/Utils/MarshalUtils.cs
/// </summary>
internal static class MarshalUtils
{
    private const string Ole32 = "ole32.dll";
    private const string OleAut32 = "oleaut32.dll";

    public static object GetActiveObject(string progId)
    {
        Guid clsid;
        try
        {
            CLSIDFromProgIDEx(progId, out clsid);
        }
        catch (Exception)
        {
            CLSIDFromProgID(progId, out clsid);
        }

        GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
        return obj;
    }

#pragma warning disable SYSLIB1054
    [DllImport(Ole32, PreserveSig = false)]
    private static extern void CLSIDFromProgIDEx(
        [MarshalAs(UnmanagedType.LPWStr)] string progId,
        out Guid clsid);

    [DllImport(Ole32, PreserveSig = false)]
    private static extern void CLSIDFromProgID(
        [MarshalAs(UnmanagedType.LPWStr)] string progId,
        out Guid clsid);

    [DllImport(OleAut32, PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.Interface)] out object ppunk);
#pragma warning restore SYSLIB1054
}
