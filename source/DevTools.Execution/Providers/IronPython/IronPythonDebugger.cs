using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging;
using Microsoft.Scripting.Hosting;
using ZLogger;
using Ipy = IronPython.Hosting.Python;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Session-lifetime pydevd 2.8.0. Engine comes from either embedded 3.4.2
/// (<see cref="GetOrCreateEngine"/>) or an existing pyRevit ScriptExecutor
/// engine (<see cref="InitializeAsync(object)"/>). One listener.
/// </summary>
public sealed class IronPythonDebugger(ILogger<IronPythonDebugger>? logger = null)
{
    public const int PreferredPort = 4567;
    private static readonly Lock SessionLock = new();
    private static object? sessionEngine;
    private static bool ownsEngine;
    private static bool listening;
    private static int debugPortValue;
    private static string? lastAttachCheckError;

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

    public int DebugPort
    {
        get
        {
            lock (SessionLock)
                return debugPortValue;
        }
    }

    public ScriptEngine? Engine
    {
        get
        {
            lock (SessionLock)
                return sessionEngine as ScriptEngine;
        }
    }

    public bool IsAttached
    {
        get
        {
            object? engine;
            lock (SessionLock)
                engine = sessionEngine;

            if (engine is null)
                return false;

            try
            {
                var scope = DlrScriptHost.CreateScope(engine);
                DlrScriptHost.Execute(engine, AttachedScript, scope);
                lastAttachCheckError = null;
                return DlrScriptHost.GetVariable<bool>(scope, "__is_attached__");
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (string.Equals(lastAttachCheckError, message, StringComparison.Ordinal)) 
                    return false;
                lastAttachCheckError = message;
                logger?.ZLogWarning($"Failed to check IronPython pydevd attach: {message}");
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

    /// <summary>
    /// Listen on an existing DLR engine (pyRevit ScriptExecutor). Does not
    /// <c>CreateEngine</c> and does not own shutdown of that engine.
    /// </summary>
    public async Task InitializeAsync(object engine)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(engine);
            await PydevdInstaller.EnsureInstalledAsync(logger).ConfigureAwait(false);
            lock (SessionLock)
            {
                sessionEngine = engine;
                ownsEngine = false;
            }

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
            if (sessionEngine is ScriptEngine typed)
                return typed;

            if (sessionEngine is not null)
            {
                throw new InvalidOperationException(
                    "IronPython debugger already holds a non-embedded engine.");
            }

            var engine = Ipy.CreateEngine(new Dictionary<string, object>
            {
                ["Frames"] = true,
                ["FullFrames"] = true,
            });
            bridge.ConfigureEngine(engine);
            IronPythonInitializer.AddStdLib(engine);
            IronPythonInitializer.Setup(engine);
            ConfigureEngine(engine);
            sessionEngine = engine;
            ownsEngine = true;

            // Listen is InitializeAsync-only. GetOrCreateEngine is also used by
            // headless Execute; starting pydevd here races pythonnet in testhost.
            return engine;
        }
    }

    /// <summary>
    /// Script Run is the Revit API thread; listen ran on a background thread.
    /// Install pydevd tracing with <c>enable_tracing</c>, not full
    /// <c>settrace</c> (that walks <c>f_back</c> and throws on IronPython 3.4).
    /// </summary>
    public void EnsureCurrentThreadTraced()
    {
        object? engine;
        lock (SessionLock)
            engine = sessionEngine;

        if (engine is null || !PydevdInstaller.IsInstalled())
            return;

        try
        {
            DlrScriptHost.Execute(engine, TraceCurrentThreadScript);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"IronPython pydevd current-thread trace failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Drop modules whose <c>__file__</c> is under the refresh root so the
    /// next Run re-reads edited files.
    /// </summary>
    public void RefreshUserModules(
        string scriptPath,
        string? root = null,
        IReadOnlyList<string>? skipRoots = null)
    {
        object? engine;
        lock (SessionLock)
            engine = sessionEngine;

        if (engine is null)
            return;

        root ??= Path.GetDirectoryName(scriptPath);
        if (string.IsNullOrEmpty(root))
            return;

        try
        {
            var scope = DlrScriptHost.CreateScope(engine);
            DlrScriptHost.SetVariable(scope, "__refresh_root__", root);
            DlrScriptHost.SetVariable(scope, "__refresh_skip__", skipRoots?.ToList() ?? []);
            DlrScriptHost.Execute(engine, """
                import os
                import sys
                sep = os.sep
                root = os.path.normcase(os.path.abspath(__refresh_root__))
                skips = [os.path.normcase(os.path.abspath(p)) for p in __refresh_skip__]
                def under(base, full):
                    return full == base or full.startswith(base + sep)
                dead = []
                for name, mod in list(sys.modules.items()):
                    path = getattr(mod, '__file__', None)
                    if not path:
                        continue
                    try:
                        full = os.path.normcase(os.path.abspath(path))
                    except Exception:
                        continue
                    if not under(root, full):
                        continue
                    if any(under(s, full) for s in skips):
                        continue
                    dead.append(name)
                for name in dead:
                    sys.modules.pop(name, None)
                """, scope);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"IronPython user-module refresh failed: {ex.Message}");
        }
    }

    public void ConfigureEngine(object engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!PydevdInstaller.IsInstalled())
            return;

        var root = PydevdInstaller.ExtractRoot;
        var paths = DlrScriptHost.GetSearchPaths(engine);
        if (!paths.Any(p => string.Equals(p, root, StringComparison.OrdinalIgnoreCase)))
            paths.Insert(0, root);

        DlrScriptHost.SetSearchPaths(engine, paths);
    }

    public void StartListening(int port = PreferredPort)
    {
        object engine;
        int debugPort;
        lock (SessionLock)
        {
            if (listening || sessionEngine is null || !PydevdInstaller.IsInstalled())
                return;

            engine = sessionEngine;
            ConfigureEngine(engine);
            debugPort = PythonDebugger.FindAvailablePort(port);
            debugPortValue = debugPort;
        }

        // _enable_attach calls settrace on *this* thread. Host.Start uses
        // RunBlocking, so listen on the caller would leave the Revit UI
        // thread traced. IronPython SetTrace is thread-local; a dedicated
        // thread exits after listen. Do not use Task.Run: a pool worker
        // keeps settrace after return. CPython debugpy.listen is the same
        // idea (listener threads, not the API thread).
        Exception? listenError = null;
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var scope = DlrScriptHost.CreateScope(engine);
                DlrScriptHost.SetVariable(scope, "__port__", debugPort);
                DlrScriptHost.Execute(engine, PythonEmbedded.IpyDebuggerScript, scope);
            }
            catch (Exception ex)
            {
                listenError = ex;
            }
            finally
            {
                ready.TrySetResult(true);
            }
        })
        {
            IsBackground = true,
            Name = "IronPython pydevd listen",
        };
        thread.Start();

        if (!ready.Task.Wait(TimeSpan.FromSeconds(30)))
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
            listening = true;

        logger?.ZLogInformation($"IronPython pydevd listening on 127.0.0.1:{debugPort}");
    }

    public void Shutdown()
    {
        lock (SessionLock)
        {
            if (ownsEngine && sessionEngine is not null)
            {
                try
                {
                    DlrScriptHost.Shutdown(sessionEngine);
                }
                catch
                {
                    // ignored
                }
            }

            sessionEngine = null;
            ownsEngine = false;
            listening = false;
            debugPortValue = 0;
            lastAttachCheckError = null;
        }
    }
}
