using System.Net;
using System.Net.Sockets;
using DevTools.Execution.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Scripting.Hosting;
using ZLogger;
using Ipy = IronPython.Hosting.Python;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Session-lifetime embedded IronPython engine with PyDev.Debugger 2.8.0 listen-on-port (no wait).
/// </summary>
public sealed class IronPythonDebugger(ILogger<IronPythonDebugger>? logger = null)
{
    public const int PreferredPort = 5680;

    private static readonly object SessionLock = new();
    private static ScriptEngine? SessionEngine;
    private static bool Listening;
    private static int DebugPortValue;

    // IronPython 3.4 reports sys.platform as win32. pydevd 2.8.0 sets
    // IS_IRONPYTHON from platform == 'cli' (IronPython 2) and IS_WINDOWS from
    // platform == 'win32'. Those two cannot be true at once, so: import
    // constants under cli, force IS_WINDOWS, restore platform, then import
    // pydevd. Leaving IS_WINDOWS false makes breakpoint path matching
    // case-sensitive (Samples vs samples, C: vs c:) and never hits.
    private const string ListenScript = """
        import sys
        __pydevd_orig_platform = sys.platform
        sys.platform = 'cli'
        from _pydevd_bundle import pydevd_constants
        pydevd_constants.IS_WINDOWS = True
        sys.platform = __pydevd_orig_platform
        from _pydevd_bundle.pydevd_constants import HTTP_JSON_PROTOCOL
        from _pydevd_bundle.pydevd_defaults import PydevdCustomization
        PydevdCustomization.DEFAULT_PROTOCOL = HTTP_JSON_PROTOCOL
        import pydevd
        pydevd._enable_attach(("127.0.0.1", __port__))
        # 2.8 HTTP_JSON make_thread_suspend_message is NULL_NET_COMMAND.
        # DAP StoppedEvent is only sent when this flag is True. 3.4.1
        # (VS Code adapter) still emits StoppedEvent when the flag is False;
        # 2.8 does not. Adapter never sends multiThreadsSingleNotification.
        pydevd.get_global_debugger().multi_threads_single_notification = True
        """;

    private const string AttachedScript = """
        import sys
        __is_attached__ = False
        if 'pydevd' in sys.modules:
            import pydevd
            __is_attached__ = bool(pydevd._is_attached())
        """;

    // pydevd.settrace() walks get_frame().f_back. IronPython 3.4 hosted
    // DLR stacks throw ArgumentOutOfRangeException (Parameter 'index') on
    // that walk. User code is compiled after this call, so existing frames
    // do not need f_trace. enable_tracing() is sys.settrace on this thread.
    private const string TraceCurrentThreadScript = """
        import sys
        import threading
        if 'pydevd' in sys.modules:
            import pydevd
            from _pydevd_bundle.pydevd_additional_thread_info import set_additional_thread_info
            py_db = pydevd.get_global_debugger()
            if py_db is not None:
                set_additional_thread_info(threading.currentThread())
                py_db.enable_tracing()
        """;

    public int DebugPort => DebugPortValue;

    public ScriptEngine? Engine
    {
        get
        {
            lock (SessionLock)
                return SessionEngine;
        }
    }

    public bool IsAttached
    {
        get
        {
            ScriptEngine? engine;
            lock (SessionLock)
                engine = SessionEngine;

            if (engine is null)
                return false;

            try
            {
                var scope = engine.CreateScope();
                engine.CreateScriptSourceFromString(AttachedScript).Execute(scope);
                return scope.GetVariable<bool>("__is_attached__");
            }
            catch (Exception ex)
            {
                logger?.ZLogWarning($"Failed to check IronPython pydevd attach: {ex.Message}");
                return false;
            }
        }
    }

    public async Task InitializeAsync(IIronPythonBridge bridge)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(bridge);
            await PydevdInstaller.EnsureInstalledAsync(logger).ConfigureAwait(false);
            GetOrCreateEngine(bridge);
            StartListening();
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"IronPython pydevd init failed: {ex.Message}");
        }
    }

    public ScriptEngine GetOrCreateEngine(IIronPythonBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        lock (SessionLock)
        {
            if (SessionEngine is null)
            {
                var engine = Ipy.CreateEngine(new Dictionary<string, object>
                {
                    ["Frames"] = true,
                    ["FullFrames"] = true,
                });
                bridge.ConfigureEngine(engine);
                IronPythonInitializer.AddStdLib(engine);
                IronPythonInitializer.Setup(engine);
                ConfigureEngine(engine);
                SessionEngine = engine;
            }

            // Listen is InitializeAsync-only. GetOrCreateEngine is also used by
            // headless Execute; starting pydevd here races pythonnet in testhost.
            return SessionEngine;
        }
    }

    /// <summary>
    /// Script Run is the Revit API thread; listen ran on a background thread.
    /// Install pydevd tracing with <c>enable_tracing</c>, not full
    /// <c>settrace</c> (that walks <c>f_back</c> and throws on IronPython 3.4).
    /// </summary>
    public void EnsureCurrentThreadTraced()
    {
        ScriptEngine? engine;
        lock (SessionLock)
            engine = SessionEngine;

        if (engine is null || !PydevdInstaller.IsInstalled())
            return;

        try
        {
            var scope = engine.CreateScope();
            engine.CreateScriptSourceFromString(TraceCurrentThreadScript).Execute(scope);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"IronPython pydevd current-thread trace failed: {ex.Message}");
        }
    }

    public void ConfigureEngine(ScriptEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!PydevdInstaller.IsInstalled())
            return;

        var root = PydevdInstaller.ExtractRoot;
        var paths = engine.GetSearchPaths().ToList();
        if (!paths.Any(p => string.Equals(p, root, StringComparison.OrdinalIgnoreCase)))
            paths.Insert(0, root);

        engine.SetSearchPaths(paths);
    }

    public void StartListening(int port = PreferredPort)
    {
        ScriptEngine engine;
        int debugPort;
        lock (SessionLock)
        {
            if (Listening || SessionEngine is null || !PydevdInstaller.IsInstalled())
                return;

            engine = SessionEngine;
            ConfigureEngine(engine);
            debugPort = FindAvailablePort(port);
            DebugPortValue = debugPort;
        }

        // _enable_attach calls settrace on *this* thread. Host.Start uses
        // RunBlocking, so listen on the caller would leave the Revit UI
        // thread traced. IronPython SetTrace is thread-local; a dedicated
        // thread exits after listen. Do not use Task.Run: a pool worker
        // keeps settrace after return. CPython debugpy.listen is the same
        // idea (listener threads, not the API thread).
        Exception? listenError = null;
        using var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                var scope = engine.CreateScope();
                scope.SetVariable("__port__", debugPort);
                engine.CreateScriptSourceFromString(ListenScript).Execute(scope);
            }
            catch (Exception ex)
            {
                listenError = ex;
            }
            finally
            {
                ready.Set();
            }
        })
        {
            IsBackground = true,
            Name = "IronPython pydevd listen",
        };
        thread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(30)))
        {
            logger?.ZLogError($"IronPython pydevd listen timed out on 127.0.0.1:{debugPort}");
            return;
        }

        if (listenError is not null)
        {
            logger?.ZLogError(
                $"Failed to initialize IronPython pydevd: {listenError.Message}{Environment.NewLine}{listenError.StackTrace}");
            return;
        }

        lock (SessionLock)
            Listening = true;

        logger?.ZLogInformation($"IronPython pydevd listening on 127.0.0.1:{debugPort}");
    }

    public void Shutdown()
    {
        lock (SessionLock)
        {
            try
            {
                SessionEngine?.Runtime.Shutdown();
            }
            catch
            {
                // ignored
            }

            SessionEngine = null;
            Listening = false;
            DebugPortValue = 0;
        }
    }

    private static int FindAvailablePort(int preferredPort)
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
