using System.IO;
using DevTools.Execution.Diagnostics;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// pydevd 2.8.0 helpers, parallel to <see cref="PythonDebugger"/>.
/// Engine comes from <see cref="IronPythonInitializer"/>.
/// </summary>
public static class IronPythonDebugger
{
    public const int PreferredPort = 4567;

    private const string AttachedScript = $"""
                                           import sys
                                           {PythonInstances.IsAttached} = False
                                           if 'pydevd' in sys.modules:
                                               import pydevd
                                               {PythonInstances.IsAttached} = bool(pydevd._is_attached())
                                           """;

    // sys.settrace inside ScriptSource.Execute races IronPython RunWorker:
    // PushFrame, pydevd FrameExit pops FunctionStack, then PopFrame
    // RemoveAt throws ArgumentOutOfRangeException. Prepare the callback in
    // Python; install it via PythonContext.SetTrace after Execute returns.
    private const string TraceCurrentThreadScript = $"""
                                                     import sys
                                                     import threading
                                                     {PythonInstances.HasTrace} = False
                                                     {PythonInstances.TraceFunc} = None
                                                     if 'pydevd' in sys.modules:
                                                         import pydevd
                                                         py_db = pydevd.get_global_debugger()
                                                         if py_db is not None:
                                                             from _pydevd_bundle.pydevd_additional_thread_info import set_additional_thread_info
                                                             set_additional_thread_info(threading.currentThread())
                                                             {PythonInstances.TraceFunc} = py_db.get_thread_local_trace_func()
                                                             {PythonInstances.HasTrace} = {PythonInstances.TraceFunc} is not None
                                                     """;

    private const string RefreshUserModulesScript = $"""
                                                     import os
                                                     import sys
                                                     sep = os.sep
                                                     root = os.path.normcase(os.path.abspath({PythonInstances.RefreshRoot}))
                                                     skips = [os.path.normcase(os.path.abspath(p)) for p in {PythonInstances.RefreshSkip}]
                                                     def under(base, full):
                                                         return full == base or full.startswith(base + sep)
                                                     dead = []
                                                     for name, mod in list(sys.modules.items()):
                                                         path = getattr(mod, '{PythonInstances.File}', None)
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
                                                     """;

    private const string PrepareDebuggerScript = $"{PythonInstances.Debugger} = IpyDebugger()\n{PythonInstances.Debugger}.prepare()";

    private const string ListenScript = $"{PythonInstances.Debugger}.listen({PythonInstances.Port})";

    public static bool IsAttached(object? engine, ILogger? logger = null)
    {
        if (engine is null)
            return false;

        try
        {
            var scope = DlrScriptHost.CreateScope(engine);
            DlrScriptHost.Execute(engine, AttachedScript, scope);
            return DlrScriptHost.GetVariable<bool>(scope, PythonInstances.IsAttached);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"Failed to check IronPython pydevd attach: {ex.Message}");
            return false;
        }
    }

    public static async Task StartListeningAsync(object engine, DebugEndpoint endpoint, ILogger? logger = null)
    {
        try
        {
            await PydevdInstaller.EnsureInstalledAsync(logger).ConfigureAwait(false);
            StartListening(engine, endpoint, logger);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"IronPython pydevd init failed:{Environment.NewLine}{ex}");
        }
    }

    /// <summary>
    /// Script Run is the Revit API thread; listen ran on a background thread.
    /// Bind pydevd's trace callback on this thread via
    /// <c>PythonContext.SetTrace</c>. Do not call <c>sys.settrace</c> from
    /// <c>ScriptSource.Execute</c> (IronPython <c>PopFrame</c> then throws).
    /// </summary>
    public static void EnsureCurrentThreadTraced(object? engine, ILogger? logger = null)
    {
        if (engine is null || !PydevdInstaller.IsInstalled())
            return;

        try
        {
            var scope = DlrScriptHost.CreateScope(engine);
            DlrScriptHost.Execute(engine, TraceCurrentThreadScript, scope);
            if (!DlrScriptHost.GetVariable<bool>(scope, PythonInstances.HasTrace))
                return;

            var traceFunc = DlrScriptHost.GetVariable<object>(scope, PythonInstances.TraceFunc);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (traceFunc is not null)
                DlrScriptHost.SetTrace(engine, traceFunc);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"IronPython pydevd current-thread trace failed:{Environment.NewLine}{ex}");
        }
    }

    /// <summary>
    /// Drop modules whose <c>__file__</c> is under the refresh root so the
    /// next Run re-reads edited files.
    /// </summary>
    public static void RefreshUserModules(
        object? engine,
        string scriptPath,
        string? root = null,
        IReadOnlyList<string>? skipRoots = null,
        ILogger? logger = null)
    {
        if (engine is null)
            return;

        root ??= Path.GetDirectoryName(scriptPath);
        if (string.IsNullOrEmpty(root))
            return;

        try
        {
            var scope = DlrScriptHost.CreateScope(engine);
            DlrScriptHost.SetVariable(scope, PythonInstances.RefreshRoot, root);
            DlrScriptHost.SetVariable(scope, PythonInstances.RefreshSkip, skipRoots?.ToList() ?? []);
            DlrScriptHost.Execute(engine, RefreshUserModulesScript, scope);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"IronPython user-module refresh failed: {ex.Message}");
        }
    }

    internal static void AddPydevdSearchPath(object engine)
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

    public static void StartListening(object? engine, DebugEndpoint endpoint, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (engine is null || endpoint.IsListening || !PydevdInstaller.IsInstalled())
            return;

        AddPydevdSearchPath(engine);
        endpoint.Reserve(PreferredPort);

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
                ListenOnDedicatedThread(engine, endpoint, logger);
            }
            catch (Exception ex)
            {
                listenError = ex;
                endpoint.MarkFailed();
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
            endpoint.MarkFailed();
            logger?.ZLogError($"IronPython pydevd listen timed out on {DebugPortLease.Host}:{endpoint.Port}");
            return;
        }

        if (listenError is not null)
        {
            logger?.ZLogError($"Failed to initialize IronPython pydevd:{Environment.NewLine}{listenError}");
            return;
        }

        logger?.ZLogInformation($"IronPython pydevd listening on {DebugPortLease.Host}:{endpoint.Port}");
    }

    private static void ListenOnDedicatedThread(object engine, DebugEndpoint endpoint, ILogger? logger)
    {
        var scope = DlrScriptHost.CreateScope(engine);
        DlrScriptHost.Execute(engine, PythonEmbedded.IpyDebuggerScript, scope);
        DlrScriptHost.Execute(engine, PrepareDebuggerScript, scope);

        var port = endpoint.ReleaseLease();
        try
        {
            EnableAttach(scope, engine, port);
        }
        catch (Exception first)
        {
            logger?.ZLogWarning($"pydevd listen {port} failed: {first.Message}");
            using (var fallback = DebugPortLease.Acquire(0))
                port = fallback.Port;

            EnableAttach(scope, engine, port);
        }

        endpoint.MarkListening(port);
        endpoint.AttachProbe = () => IsAttached(engine, logger);
    }

    private static void EnableAttach(object scope, object engine, int port)
    {
        DlrScriptHost.SetVariable(scope, PythonInstances.Port, port);
        DlrScriptHost.Execute(engine, ListenScript, scope);
    }
}
