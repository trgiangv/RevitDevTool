using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DevTools.Ipc;

/// <summary>Opens a current-user named pipe for a DevTools host listener.</summary>
public static class NamedPipes
{
    public const int DefaultMaxInstances = 8;

    public static NamedPipeServerStream Open(string pipeName, int maxInstances = DefaultMaxInstances)
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent();
        if (currentUser.User is null)
            throw new InvalidOperationException("Cannot determine current user SID for pipe ACL.");

        security.AddAccessRule(new PipeAccessRule(
            currentUser.User,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

#if NETFRAMEWORK
        return new NamedPipeServerStream(pipeName, PipeDirection.InOut, maxInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#else
        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, maxInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
#endif
    }

    public static bool IsBusy(IOException ex)
    {
        const int allPipeInstancesBusy = 231;
        var win32Code = ex.HResult & 0xFFFF;
        return win32Code == allPipeInstancesBusy ||
               ex.Message.Contains("All pipe instances are busy", StringComparison.OrdinalIgnoreCase);
    }
}
