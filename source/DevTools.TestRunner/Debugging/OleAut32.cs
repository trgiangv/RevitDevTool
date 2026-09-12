using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
namespace DevTools.TestRunner.Debugging;

internal static class OleAut32
{
    private const string Ole32 = "ole32.dll";
    private const string OleAut32Dll = "oleaut32.dll";

    public static object GetActiveObject(string progId)
    {
        Guid clsid;
        try
        {
            CLSIDFromProgIDEx(progId, out clsid);
        }
        catch (COMException)
        {
            CLSIDFromProgID(progId, out clsid);
        }

        GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
        return obj;
    }
    
 #pragma warning disable SYSLIB1054
    [DllImport(Ole32)]
    internal static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);

    [DllImport(Ole32)]
    internal static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

    [DllImport(Ole32, CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgIDEx(string progId, out Guid clsid);

    [DllImport(Ole32, CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport(OleAut32Dll, PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.Interface)] out object ppunk);
 #pragma warning restore SYSLIB1054
}
