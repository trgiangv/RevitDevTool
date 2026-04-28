using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Python.Runtime;
namespace DevTools.Execution.Providers.Python;

public static class PythonDebugger
{
    public static int DebugPort { get; private set; }

    public static void StartListening()
    {
        DebugPort = FindAvailablePort();

        const string debugpySetup = """
                                    import os
                                    import debugpy
                                    os.environ["PYDEVD_DISABLE_FILE_VALIDATION"] = "1"

                                    if not debugpy.is_client_connected():
                                        debugpy.listen(("localhost", __port__), in_process_debug_adapter=True)
                                    """;

        using var scope = Py.CreateScope();
        try
        {
            scope.Set("__port__", new PyInt(DebugPort));
            scope.Exec(debugpySetup);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Failed to initialize debugpy: {e.Message}{Environment.NewLine}{e.StackTrace}");
        }
    }

    public static bool IsConnected()
    {
        if (!PythonEngine.IsInitialized) return false;

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            try
            {
                scope.Exec("""
                           import sys
                           __is_connected__ = False
                           if 'debugpy' in sys.modules:
                               import debugpy
                               __is_connected__ = debugpy.is_client_connected()
                           """);
                dynamic isConnected = scope.Get("__is_connected__");
                return (bool)isConnected;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to check debugger connection: {ex.Message}");
                return false;
            }
        }
    }

    private static int FindAvailablePort(int preferredPort = 5678)
    {
        try
        {
            var tester = new TcpListener(IPAddress.Loopback, preferredPort);
            tester.Start();
            tester.Stop();
            return preferredPort;
        }
        catch (SocketException)
        {
            var fallback = new TcpListener(IPAddress.Loopback, 0);
            fallback.Start();
            var port = ((IPEndPoint)fallback.LocalEndpoint).Port;
            fallback.Stop();
            return port;
        }
    }
}
