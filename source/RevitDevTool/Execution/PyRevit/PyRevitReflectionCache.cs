using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
#if NET
using System.Runtime.CompilerServices;
#else
using System.Runtime.Serialization;
#endif
using DevTools.Execution.Models;
using DevTools.Execution.Providers.IronPython;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RevitDevTool.Core;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Session-cached reflection for pyRevit Labs runtime and PyRevitLoader fallback.
/// </summary>
internal sealed class PyRevitReflectionCache
{
    /// <summary>
    /// Isolated pyRevit engine-cache slot (<c>CommandExtension</c> / TypeId).
    /// </summary>
    private const string RevitDevToolExtension = "RevitDevTool-8e4c1b7a-3f92-4d6e-a1c5-7b0e9f2d4a68";

    /// <summary>
    /// Completes <c>pyrevit/__init__.py</c> (assigns <c>HOST_APP</c>) before pydevd
    /// traces. <c>pyrevit._perf.mark()</c> otherwise imports <c>coreutils</c> while
    /// the package is still loading; that <c>ImportError</c> is caught without a
    /// debugger, but pydevd pauses it on the Revit API thread.
    /// </summary>
    private const string ImportHostApp = "from pyrevit import HOST_APP";

    /// <summary>
    /// pyRevit-loader IronPython. Reuse one engine so pydevd stays attached.
    /// <c>full_frame</c> is off so pyRevit does not set <c>Tracing</c>.
    /// </summary>
    private const string EngineConfigsJson =
        "{\"clean\":false,\"persistent\":false,\"full_frame\":false,\"type\":\"IronPython\",\"type_explicit\":true}";

    private static readonly Lazy<PyRevitReflectionCache> LazyInstance = new(Create);

    private readonly RuntimeBinding? _runtime;
    private readonly LoaderBinding? _loader;
    private readonly ILogger<PyRevitReflectionCache> _logger;

    private PyRevitReflectionCache(
        RuntimeBinding? runtime,
        LoaderBinding? loader,
        ILogger<PyRevitReflectionCache> logger)
    {
        _runtime = runtime;
        _loader = loader;
        _logger = logger;
    }

    internal static PyRevitReflectionCache Instance => LazyInstance.Value;

    internal bool HasRuntime => _runtime is not null;

    internal bool HasLoader => _loader is not null;

    private static PyRevitReflectionCache Create()
    {
        PyRevitLibraryPaths.EnsureResolved();

        RuntimeBinding? runtime = null;
        if (PyRevitLibraryPaths.RuntimeAssembly is { } runtimeAssembly)
        {
            runtime = RuntimeBinding.Load(runtimeAssembly);
            ReflectionBound.Call(runtime.InitializeExecutor, null, []);
        }

        LoaderBinding? loader = null;
        if (PyRevitLibraryPaths.LoaderAssembly is { } loaderAssembly)
            loader = LoaderBinding.TryLoad(loaderAssembly);

        return new PyRevitReflectionCache(runtime, loader, NullLogger<PyRevitReflectionCache>.Instance);
    }

    internal ExecutionResult ExecuteRuntime(string scriptPath, string rootPath)
    {
        if (_runtime is null)
            throw new InvalidOperationException("pyRevit Labs runtime is not available.");

        var scriptData = _runtime.CreateScriptData(scriptPath);
        var runtimeConfigs = _runtime.CreateRuntimeConfigs(scriptPath);
        var execConfigs = Activator.CreateInstance(_runtime.ScriptExecutorConfigsType)
            ?? throw new InvalidOperationException("Failed to create ScriptExecutorConfigs.");

        var resultCode = (int)ReflectionBound.Call(_runtime.ExecuteScript, null, [scriptData, runtimeConfigs, execConfigs])!;

        return IsSuccessResultCode(resultCode)
            ? ExecutionResult.Succeeded("Script completed (pyRevit runtime).")
            : ExecutionResult.Failed($"pyRevit runtime finished with code {resultCode}.");
    }

    internal ExecutionResult ExecuteLoader(
        string scriptPath,
        string rootPath)
    {
        if (_loader is null)
            return ExecutionResult.Failed("pyRevit is not loaded in this Revit session.");

        var executor = _loader.CreateExecutor();
        var revitResult = ReflectionBound.Call(_loader.ExecuteScript, executor, [scriptPath, PyRevitSearchPaths.Build(scriptPath), null, null]);
        var message = ReflectionBound.Get<string>(executor, PyRevitNames.Message);
        if (!string.IsNullOrEmpty(message))
            Trace.Write(message);

        var resultName = revitResult?.ToString() ?? string.Empty;
        if (resultName.Contains(PyRevitNames.Succeeded, StringComparison.Ordinal))
            return ExecutionResult.Succeeded("Script completed (pyRevit loader).");

        if (!string.IsNullOrEmpty(message))
            return ExecutionResult.Failed(message!);

        return ExecutionResult.Failed($"pyRevit loader finished with {resultName}.");
    }

    /// <summary>
    /// DLR engine for this add-in's <c>CommandExtension</c> slot — the same
    /// cache ScriptExecutor uses on Run.
    /// </summary>
    internal object EnsureIronPythonEngine(ILogger? logger = null)
    {
        var existing = TryGetIronPythonEngine();
        if (existing is not null)
            return existing;

        var dir = Path.GetTempPath();
        var scriptPath = Path.Combine(dir, "warmup_ipy_script.py");
        File.WriteAllText(scriptPath, ImportHostApp);

        var result = PyRevitScriptExecutor.Execute(scriptPath, dir, logger);
        if (!result.Success)
            throw new InvalidOperationException($"pyRevit IronPython engine warmup failed: {result.Message}");

        return TryGetIronPythonEngine()
            ?? throw new InvalidOperationException(
                "pyRevit IronPythonEngine.Engine was not found after ScriptExecutor warmup.");
    }

    /// <summary>
    /// Load pyRevit on this engine with tracing off. Safe to call every Run
    /// (cached <c>sys.modules</c> hit after warmup).
    /// </summary>
    internal void EnsureHostAppImported()
    {
        var engine = TryGetIronPythonEngine();
        if (engine is null)
            return;

        DlrScriptHost.Execute(engine, ImportHostApp);
    }

    private static object? TryGetIronPythonEngine()
    {
        if (AppDomain.CurrentDomain.GetData(PyRevitLibraryPaths.EnginesDictKey) is not IDictionary dict)
            return null;

        foreach (var wrapper in dict.Values)
        {
            if (EngineIfOurs(wrapper) is { } engine)
                return engine;
        }

        return null;
    }

    private static object? EngineIfOurs(object? wrapper)
    {
        if (wrapper is null)
            return null;

        var type = wrapper.GetType();
        if (!string.Equals(type.Name, PyRevitNames.IronPythonEngine, StringComparison.Ordinal))
            return null;

        if (ReflectionBound.Get<string>(wrapper, PyRevitNames.TypeId) is not { } typeId)
            return null;
        if (typeId.IndexOf(RevitDevToolExtension, StringComparison.OrdinalIgnoreCase) < 0)
            return null;

        return ReflectionBound.Get<object>(wrapper, PyRevitNames.Engine);
    }

    /// <summary>pyRevit <c>ScriptExecutorResultCodes</c>: Succeeded=0, SysExited=1.</summary>
    private static bool IsSuccessResultCode(int resultCode) => resultCode is 0 or 1;

    private delegate void MemberSetter(object instance, object? value);

    private sealed class RuntimeBinding
    {
        private readonly Dictionary<string, MemberSetter> _scriptDataSetters;
        private readonly Dictionary<string, MemberSetter> _runtimeConfigSetters;

        internal MethodInfo InitializeExecutor { get; }
        internal MethodInfo ExecuteScript { get; }
        internal Type ScriptExecutorConfigsType { get; }

        private Type ScriptDataType { get; }
        private Type ScriptRuntimeConfigsType { get; }
        private PropertyInfo CommandDataApplicationProperty { get; }

        private RuntimeBinding(
            MethodInfo initializeExecutor,
            MethodInfo executeScript,
            Type scriptDataType,
            Type scriptRuntimeConfigsType,
            Type scriptExecutorConfigsType,
            PropertyInfo commandDataApplicationProperty)
        {
            InitializeExecutor = initializeExecutor;
            ExecuteScript = executeScript;
            ScriptDataType = scriptDataType;
            ScriptRuntimeConfigsType = scriptRuntimeConfigsType;
            ScriptExecutorConfigsType = scriptExecutorConfigsType;
            CommandDataApplicationProperty = commandDataApplicationProperty;
            _scriptDataSetters = BuildSetters(scriptDataType);
            _runtimeConfigSetters = BuildSetters(scriptRuntimeConfigsType);
        }

        internal static RuntimeBinding Load(Assembly runtimeAssembly)
        {
            var scriptExecutorType = ResolveType(runtimeAssembly, PyRevitNames.ScriptExecutor);
            var scriptDataType = ResolveType(runtimeAssembly, PyRevitNames.ScriptData);
            var configsType = ResolveType(runtimeAssembly, PyRevitNames.ScriptRuntimeConfigs);
            var execConfigsType = ResolveType(runtimeAssembly, PyRevitNames.ScriptExecutorConfigs);

            var initialize = scriptExecutorType.GetMethod(PyRevitNames.Initialize, BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("ScriptExecutor.Initialize was not found.");

            var execute = scriptExecutorType.GetMethod(
                PyRevitNames.ExecuteScript,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                [scriptDataType, configsType, execConfigsType],
                modifiers: null) ?? throw new InvalidOperationException("ScriptExecutor.ExecuteScript was not found.");

            var commandDataApp = typeof(ExternalCommandData).GetProperty(nameof(ExternalCommandData.Application))
                ?? throw new InvalidOperationException("ExternalCommandData.Application was not found.");

            return new RuntimeBinding(initialize, execute, scriptDataType, configsType, execConfigsType, commandDataApp);
        }

        internal object CreateScriptData(string scriptPath)
        {
            var scriptDir = Path.GetDirectoryName(scriptPath);
            var commandName = Path.GetFileNameWithoutExtension(scriptPath);
            var commandBundle = scriptDir is not null ? Path.GetFileName(scriptDir) : string.Empty;

            var scriptData = Activator.CreateInstance(ScriptDataType)
                ?? throw new InvalidOperationException("Failed to create ScriptData.");

            Apply(_scriptDataSetters, scriptData, PyRevitNames.ScriptPath, scriptPath);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.ConfigScriptPath, scriptPath);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.CommandUniqueId, Guid.NewGuid().ToString());
            Apply(_scriptDataSetters, scriptData, PyRevitNames.CommandControlId, commandName);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.CommandName, commandName);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.CommandBundle, commandBundle);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.CommandExtension, RevitDevToolExtension);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.CommandContext, string.Empty);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.HelpSource, string.Empty);
            Apply(_scriptDataSetters, scriptData, PyRevitNames.Tooltip, string.Empty);
            return scriptData;
        }

        internal object CreateRuntimeConfigs(string scriptPath)
        {
            var searchPaths = PyRevitSearchPaths.Build(scriptPath);
            var commandData = CreateCommandData(RevitContext.UiApplication);

            var configs = Activator.CreateInstance(ScriptRuntimeConfigsType)
                ?? throw new InvalidOperationException("Failed to create ScriptRuntimeConfigs.");

            Apply(_runtimeConfigSetters, configs, PyRevitNames.UiApp, RevitContext.UiApplication);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.CommandData, commandData);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.SelectedElements, null);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.SearchPaths, searchPaths);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.Arguments, new List<string>());
            Apply(_runtimeConfigSetters, configs, PyRevitNames.Variables, null);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.EngineConfigs, EngineConfigsJson);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.RefreshEngine, false);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.DebugMode, false);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.ConfigMode, false);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.ExecutedFromUi, false);
            Apply(_runtimeConfigSetters, configs, PyRevitNames.SuppressOutput, false);
            return configs;
        }

        private object CreateCommandData(UIApplication uiApplication)
        {
#if NET
            var commandData = RuntimeHelpers.GetUninitializedObject(typeof(ExternalCommandData));
#else
            var commandData = FormatterServices.GetUninitializedObject(typeof(ExternalCommandData));
#endif
            CommandDataApplicationProperty.SetValue(commandData, uiApplication);
            return commandData;
        }
    }

    private sealed class LoaderBinding
    {
        internal MethodInfo ExecuteScript { get; }

        private readonly ConstructorInfo _executorConstructor;

        private LoaderBinding(ConstructorInfo executorConstructor, MethodInfo executeScript)
        {
            _executorConstructor = executorConstructor;
            ExecuteScript = executeScript;
        }

        internal static LoaderBinding? TryLoad(Assembly loaderAssembly)
        {
            var executorType = loaderAssembly.GetType(PyRevitNames.LoaderScriptExecutor, throwOnError: false);
            if (executorType is null)
                return null;

            var constructor = executorType.GetConstructor([typeof(UIApplication), typeof(bool)]);
            var execute = executorType.GetMethod(
                PyRevitNames.ExecuteScript,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                [typeof(string), typeof(IEnumerable<string>), typeof(string), typeof(IDictionary<string, object>)],
                modifiers: null);

            if (constructor is null || execute is null)
                return null;

            return new LoaderBinding(constructor, execute);
        }

        internal object CreateExecutor() =>
            ReflectionBound.Call(_executorConstructor, null, [RevitContext.UiApplication, false])
            ?? throw new InvalidOperationException($"Could not create {PyRevitNames.LoaderScriptExecutor}.");
    }

    private static Type ResolveType(Assembly assembly, string fullName) =>
        assembly.GetType(fullName, throwOnError: true)
        ?? throw new InvalidOperationException($"{fullName} was not found.");

    private static Dictionary<string, MemberSetter> BuildSetters(Type type)
    {
        var setters = new Dictionary<string, MemberSetter>(StringComparer.Ordinal);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanWrite)
                setters[property.Name] = (instance, value) => property.SetValue(instance, value);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            setters[field.Name] = (instance, value) => field.SetValue(instance, value);

        return setters;
    }

    private static void Apply(Dictionary<string, MemberSetter> setters, object instance, string memberName, object? value)
    {
        if (!setters.TryGetValue(memberName, out var setter))
        {
            Instance._logger.ZLogDebug($"Warning: Member '{memberName}' was not found on type '{instance.GetType().FullName}'.");
            return;
        }

        setter(instance, value);
    }
}
