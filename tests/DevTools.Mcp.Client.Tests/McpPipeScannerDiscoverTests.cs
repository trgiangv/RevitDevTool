using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using DevTools.Ipc;
using DevTools.Mcp.Client;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class McpPipeScannerDiscoverTests
{
    [TestMethod]
    public void Discover_FindsLiveMcpPipeForCurrentProcess()
    {
        var pipeName = HostPipeName.FormatMcp("Revit", Guid.NewGuid().ToString("N")[..8], Environment.ProcessId);
        using var server = CreateServerPipe(pipeName);
        var scanner = new McpPipeScanner(NullLogger<McpPipeScanner>.Instance);

        var discovered = scanner.Discover();

        Assert.Contains(pipeName, discovered, StringComparer.OrdinalIgnoreCase);
    }

    private static NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent();
        Assert.IsNotNull(currentUser.User);
        security.AddAccessRule(new PipeAccessRule(
            currentUser.User,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }
}
