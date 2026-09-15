using DevTools.Execution.Diagnostics;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

public static class PythonDebugger
{
    public const int PreferredPort = 5678;

    private const string ImportDebugpy = """
        import os
        import debugpy
        os.environ["PYDEVD_DISABLE_FILE_VALIDATION"] = "1"
        """;

    private const string ConnectedScript = $"""
                                            import sys
                                            {PythonInstances.IsConnected} = False
                                            if 'debugpy' in sys.modules:
                                                import debugpy
                                                {PythonInstances.IsConnected} = debugpy.is_client_connected()
                                            """;

    private const string ListenScript = $"""
                                         if not debugpy.is_client_connected():
                                             debugpy.listen(("127.0.0.1", {PythonInstances.Port}), in_process_debug_adapter=True)
                                         """;

    public static void StartListening(DebugEndpoint endpoint, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.IsListening)
            return;

        endpoint.Reserve(PreferredPort);

        using var scope = Py.CreateScope();
        try
        {
            scope.Exec(ImportDebugpy);
            var port = endpoint.ReleaseLease();
            try
            {
                Listen(scope, port);
            }
            catch (Exception first)
            {
                logger?.ZLogWarning($"debugpy.listen {port} failed: {first.Message}");
                using (var fallback = DebugPortLease.Acquire(0))
                    port = fallback.Port;

                Listen(scope, port);
            }

            endpoint.MarkListening(port);
            endpoint.AttachProbe = () => IsConnected(logger);
            logger?.ZLogInformation($"debugpy listening on {DebugPortLease.Host}:{port}");
        }
        catch (Exception e)
        {
            endpoint.MarkFailed();
            logger?.ZLogError($"Failed to initialize debugpy: {e.Message}{Environment.NewLine}{e.StackTrace}");
        }
    }

    public static bool IsConnected(ILogger? logger = null)
    {
        if (!PythonEngine.IsInitialized) return false;

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            try
            {
                scope.Exec(ConnectedScript);
                dynamic isConnected = scope.Get(PythonInstances.IsConnected);
                return (bool)isConnected;
            }
            catch (Exception ex)
            {
                logger?.ZLogWarning($"Failed to check debugger connection: {ex.Message}");
                return false;
            }
        }
    }

    private static void Listen(PyModule scope, int port)
    {
        scope.Set(PythonInstances.Port, new PyInt(port));
        scope.Exec(ListenScript);
    }
}
