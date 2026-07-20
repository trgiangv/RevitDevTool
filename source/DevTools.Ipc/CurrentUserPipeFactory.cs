using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DevTools.Ipc;

public static class CurrentUserPipeFactory
{
    public static NamedPipeServerStream CreateDuplexServer(string pipeName, int maxInstances = 8)
    {
        var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User ?? throw new InvalidOperationException("Current Windows user has no SID.");
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            sid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

#if NETFRAMEWORK
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
#else
        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
#endif
    }
}
