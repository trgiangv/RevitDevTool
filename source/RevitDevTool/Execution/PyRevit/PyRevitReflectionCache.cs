using System.Diagnostics;
using System.IO;
using System.Reflection;
#if NET
using System.Runtime.CompilerServices;
#else
using System.Runtime.Serialization;
#endif
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Session-cached reflection for pyRevit Labs runtime and PyRevitLoader fallback.
/// </summary>
internal sealed class PyRevitReflectionCache
{
    /// <summary>Isolated engine cache key — never use the real extension name.</summary>
    private const string RevitDevToolExtensionKey = "RevitDevTool";

    private const string EngineConfigsJson =
        "{\"clean\":true,\"persistent\":false,\"full_frame\":false,\"type\":\"IronPython\",\"type_explicit\":true}";

    private static readonly object InitLock = new();
    private static PyRevitReflectionCache? _instance;

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

    internal static PyRevitReflectionCache Instance
    {
        get
        {
            EnsureInitialized();
            return _instance ?? throw new InvalidOperationException("pyRevit reflection is not initialized.");
        }
    }

    internal bool HasRuntime => _runtime is not null;

    internal bool HasLoader => _loader is not null;

    private static void EnsureInitialized()
    {
        if (_instance is not null)
            return;

        lock (InitLock)
        {
            if (_instance is not null)
                return;

            PyRevitLibraryPaths.EnsureResolved();

            RuntimeBinding? runtime = null;
            if (PyRevitLibraryPaths.RuntimeAssembly is { } runtimeAssembly)
            {
                runtime = RuntimeBinding.Load(runtimeAssembly);
                runtime.InitializeExecutor.Invoke(null, null);
            }

            LoaderBinding? loader = null;
            if (PyRevitLibraryPaths.LoaderAssembly is { } loaderAssembly)
                loader = LoaderBinding.TryLoad(loaderAssembly);

            _instance = new PyRevitReflectionCache(runtime, loader, NullLogger<PyRevitReflectionCache>.Instance);
        }
    }

    internal ExecutionResult ExecuteRuntime(string scriptPath, string rootPath, UIApplication uiApplication)
    {
        if (_runtime is null)
            throw new InvalidOperationException("pyRevit Labs runtime is not available.");

        var scriptData = _runtime.CreateScriptData(scriptPath);
        var runtimeConfigs = _runtime.CreateRuntimeConfigs(scriptPath, rootPath, uiApplication);
        var execConfigs = Activator.CreateInstance(_runtime.ScriptExecutorConfigsType)
            ?? throw new InvalidOperationException("Failed to create ScriptExecutorConfigs.");

        var resultCode = (int)_runtime.ExecuteScript.Invoke(null, [scriptData, runtimeConfigs, execConfigs])!;

        return IsSuccessResultCode(resultCode)
            ? ExecutionResult.Succeeded("Script completed (pyRevit runtime).")
            : ExecutionResult.Failed($"pyRevit runtime finished with code {resultCode}.");
    }

    internal ExecutionResult ExecuteLoader(
        string scriptPath,
        string rootPath,
        UIApplication uiApplication)
    {
        if (_loader is null)
            return ExecutionResult.Failed("pyRevit is not loaded in this Revit session.");

        var executor = _loader.CreateExecutor(uiApplication);
        var sysPaths = PyRevitSearchPaths.Build(scriptPath, rootPath);
        var revitResult = _loader.ExecuteScript.Invoke(executor, [scriptPath, sysPaths, null, null]);

        var message = _loader.MessageProperty.GetValue(executor) as string;
        if (!string.IsNullOrEmpty(message))
            Trace.Write(message);

        var resultName = revitResult?.ToString() ?? string.Empty;
        if (resultName.Contains("Succeeded", StringComparison.Ordinal))
            return ExecutionResult.Succeeded("Script completed (pyRevit loader).");

        if (!string.IsNullOrEmpty(message))
            return ExecutionResult.Failed(message!);

        return ExecutionResult.Failed($"pyRevit loader finished with {resultName}.");
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
            var scriptExecutorType = ResolveType(runtimeAssembly, "PyRevitLabs.PyRevit.Runtime.ScriptExecutor");
            var scriptDataType = ResolveType(runtimeAssembly, "PyRevitLabs.PyRevit.Runtime.ScriptData");
            var configsType = ResolveType(runtimeAssembly, "PyRevitLabs.PyRevit.Runtime.ScriptRuntimeConfigs");
            var execConfigsType = ResolveType(runtimeAssembly, "PyRevitLabs.PyRevit.Runtime.ScriptExecutorConfigs");

            var initialize = scriptExecutorType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("ScriptExecutor.Initialize was not found.");

            var execute = scriptExecutorType.GetMethod(
                "ExecuteScript",
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

            Apply(_scriptDataSetters, scriptData, "ScriptPath", scriptPath);
            Apply(_scriptDataSetters, scriptData, "ConfigScriptPath", scriptPath);
            Apply(_scriptDataSetters, scriptData, "CommandUniqueId", Guid.NewGuid().ToString());
            Apply(_scriptDataSetters, scriptData, "CommandControlId", commandName);
            Apply(_scriptDataSetters, scriptData, "CommandName", commandName);
            Apply(_scriptDataSetters, scriptData, "CommandBundle", commandBundle);
            Apply(_scriptDataSetters, scriptData, "CommandExtension", RevitDevToolExtensionKey);
            Apply(_scriptDataSetters, scriptData, "CommandContext", string.Empty);
            Apply(_scriptDataSetters, scriptData, "HelpSource", string.Empty);
            Apply(_scriptDataSetters, scriptData, "Tooltip", string.Empty);
            return scriptData;
        }

        internal object CreateRuntimeConfigs(string scriptPath, string rootPath, UIApplication uiApplication)
        {
            var searchPaths = PyRevitSearchPaths.Build(scriptPath, rootPath).ToList();
            var commandData = CreateCommandData(uiApplication);

            var configs = Activator.CreateInstance(ScriptRuntimeConfigsType)
                ?? throw new InvalidOperationException("Failed to create ScriptRuntimeConfigs.");

            Apply(_runtimeConfigSetters, configs, "UIApp", uiApplication);
            Apply(_runtimeConfigSetters, configs, "CommandData", commandData);
            Apply(_runtimeConfigSetters, configs, "SelectedElements", null);
            Apply(_runtimeConfigSetters, configs, "SearchPaths", searchPaths);
            Apply(_runtimeConfigSetters, configs, "Arguments", new List<string>());
            Apply(_runtimeConfigSetters, configs, "Variables", null);
            Apply(_runtimeConfigSetters, configs, "EngineConfigs", EngineConfigsJson);
            Apply(_runtimeConfigSetters, configs, "RefreshEngine", true);
            Apply(_runtimeConfigSetters, configs, "DebugMode", false);
            Apply(_runtimeConfigSetters, configs, "ConfigMode", false);
            Apply(_runtimeConfigSetters, configs, "ExecutedFromUI", false);
            Apply(_runtimeConfigSetters, configs, "SuppressOutput", false);
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
        internal PropertyInfo MessageProperty { get; }

        private readonly ConstructorInfo _executorConstructor;

        private LoaderBinding(ConstructorInfo executorConstructor, MethodInfo executeScript, PropertyInfo messageProperty)
        {
            _executorConstructor = executorConstructor;
            ExecuteScript = executeScript;
            MessageProperty = messageProperty;
        }

        internal static LoaderBinding? TryLoad(Assembly loaderAssembly)
        {
            var executorType = loaderAssembly.GetType("PyRevitLoader.ScriptExecutor", throwOnError: false);
            if (executorType is null)
                return null;

            var constructor = executorType.GetConstructor([typeof(UIApplication), typeof(bool)]);
            var execute = executorType.GetMethod(
                "ExecuteScript",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                [typeof(string), typeof(IEnumerable<string>), typeof(string), typeof(IDictionary<string, object>)],
                modifiers: null);
            var message = executorType.GetProperty("Message", BindingFlags.Instance | BindingFlags.Public);

            if (constructor is null || execute is null || message is null)
                return null;

            return new LoaderBinding(constructor, execute, message);
        }

        internal object CreateExecutor(UIApplication uiApplication) =>
            _executorConstructor.Invoke([uiApplication, false])
            ?? throw new InvalidOperationException("Could not create PyRevitLoader.ScriptExecutor.");
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
            _instance!._logger.ZLogDebug($"Warning: Member '{memberName}' was not found on type '{instance.GetType().FullName}'.");
            return;
        }

        setter(instance, value);
    }
}
