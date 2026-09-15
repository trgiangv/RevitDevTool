using System.Diagnostics;
using DevTools.Execution.Diagnostics;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;
using IronPython.Hosting;
using IronPython.Modules;
using Microsoft.Extensions.Logging;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using ZLogger;
using Ipy = IronPython.Hosting.Python;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Session IronPython engine, parallel to <c>PythonInitializer</c>:
/// create or attach on the CAD start thread. pydevd is
/// <see cref="IronPythonDebugger"/>.
/// </summary>
public sealed class IronPythonInitializer(
    IIronPythonBridge bridge,
    DebugEndpoints endpoints,
    ILogger<IronPythonInitializer> logger)
{
    private const string StdLibResourceSuffix = "IronPython.StdLib.3.4.2.zip";
    private readonly Lock gate = new();
    private static string? stdlibResourceName;

    private object? engine;
    private bool ownsEngine;

    internal object? Engine
    {
        get
        {
            lock (gate)
                return engine;
        }
    }

    public void ReserveDebugPort() => endpoints.IronPython.Reserve(IronPythonDebugger.PreferredPort);

    public async Task InitializeAsync()
    {
        EnsureEngine();
        var session = Engine;
        if (session is null)
            return;

        await IronPythonDebugger.StartListeningAsync(session, endpoints.IronPython, logger).ConfigureAwait(false);
    }

    /// <summary>
    /// CAD start thread: before any await hop.
    /// </summary>
    internal void EnsureEngine()
    {
        lock (gate)
        {
            if (engine is not null)
                return;

            try
            {
                var hostEngine = bridge.TryGetHostEngine();
                if (hostEngine is not null)
                {
                    engine = hostEngine;
                    ownsEngine = false;
                    return;
                }

                logger.ZLogInformation($"IronPython debug uses embedded 3.4.2.");
                GetOrCreateEngineNoLock(bridge);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"IronPython engine init failed:{Environment.NewLine}{ex}");
            }
        }
    }

    public Task ShutdownAsync()
    {
        lock (gate)
        {
            if (ownsEngine && engine is not null)
            {
                try
                {
                    DlrScriptHost.Shutdown(engine);
                }
                catch
                {
                    // ignored
                }
            }

            engine = null;
            ownsEngine = false;
            endpoints.IronPython.Reset();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Embedded 3.4.2 with Frames. Do not call when a pyRevit engine is attached.
    /// CAD start thread: before any await hop.
    /// </summary>
    public ScriptEngine GetOrCreateEngine(IIronPythonBridge engineBridge)
    {
        ArgumentNullException.ThrowIfNull(engineBridge);
        lock (gate)
            return GetOrCreateEngineNoLock(engineBridge);
    }

    private ScriptEngine GetOrCreateEngineNoLock(IIronPythonBridge engineBridge)
    {
        if (engine is ScriptEngine typed)
            return typed;

        if (engine is not null)
        {
            throw new InvalidOperationException(
                "IronPython session already holds a non-embedded engine.");
        }

        var created = Ipy.CreateEngine(new Dictionary<string, object>
        {
            ["Frames"] = true,
            ["FullFrames"] = true,
        });
        engineBridge.ConfigureEngine(created);
        AddStdLib(created);
        Setup(created);
        IronPythonDebugger.AddPydevdSearchPath(created);
        engine = created;
        ownsEngine = true;
        return created;
    }

    private static void Setup(ScriptEngine created)
    {
        var builtin = created.GetBuiltinModule();
        // ReSharper disable once ConvertToLocalFunction
        Action<object> logFunction = obj =>
        {
            if (obj is string str)
                Trace.Write(str);
            else
                Trace.Write(obj);
        };

        builtin.SetVariable("__log_func__", logFunction);
        var script = created.CreateScriptSourceFromString(
            PythonEmbedded.SetupScript,
            PythonEmbedded.SetupScriptFileName,
            SourceCodeKind.File);
        script.Execute(created.CreateScope());
    }

    private static void AddStdLib(ScriptEngine created)
    {
        var asm = typeof(IronPythonInitializer).Assembly;
        stdlibResourceName ??= asm.GetManifestResourceNames().SingleOrDefault(static n =>
            n.EndsWith(StdLibResourceSuffix, StringComparison.Ordinal));

        if (stdlibResourceName is null)
            throw new InvalidOperationException($"Manifest resource ending with '{StdLibResourceSuffix}' was not found.");

        var importer = new ResourceMetaPathImporter(asm, stdlibResourceName);
        dynamic sys = created.GetSysModule();
        sys.meta_path.append(importer);
    }
}
