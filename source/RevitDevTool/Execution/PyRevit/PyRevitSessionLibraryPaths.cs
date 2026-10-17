using System.Reflection;
using RevitDevTool.Core;

namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Library paths the running pyRevit loader already computed.
/// pyRevit 5, 6, and 7 (Python loader) and pyRevit 7 (C# loader) all pass that
/// list as the search-path argument of <c>ScriptCommand</c>. Reading one loaded
/// command replays whichever loader built the session. The C# builder methods
/// are only a fallback for a pyRevit 7 process whose command assemblies are
/// not loaded yet.
/// </summary>
internal static class PyRevitSessionLibraryPaths
{
    private const string BuilderAssemblyName = "pyRevitAssemblyBuilder";
    private const string SessionManagerPrefix = "pyRevitAssemblyBuilder.SessionManager";
    private const string ExtensionManagerTypeName = SessionManagerPrefix + ".ExtensionManagerService";
    private const string CollectTypeName = SessionManagerPrefix + ".LibraryExtensionSearchPaths";
    private const string LoggerTypeName = SessionManagerPrefix + ".ILogger";

    private static readonly Lock CacheLock = new();
    private static string? _cachedSessionId;
    private static List<string>? _cachedPaths;

    internal static List<string> Directories()
    {
        var sessionId = SessionId();
        lock (CacheLock)
        {
            if (_cachedPaths is { Count: > 0 }
                && string.Equals(_cachedSessionId, sessionId, StringComparison.Ordinal))
                return _cachedPaths;

            var paths = FromLoadedCommand();
            if (paths.Count == 0)
                paths = FromSessionBuilder();

            if (paths.Count <= 0) return paths;
            
            _cachedSessionId = sessionId;
            _cachedPaths = paths;
            return paths;
        }
    }

    private static List<string> FromLoadedCommand()
    {
        var commandType = PyRevitLibraryPaths.RuntimeAssembly?.GetType(PyRevitNames.ScriptCommand);
        if (commandType is null)
            return [];

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var paths = PathsFrom(assembly, commandType);
            if (paths is { Count: > 0 })
                return paths;
        }

        return [];
    }

    private static List<string> PathsFrom(Assembly assembly, Type commandType)
    {
        if (ReferenceEquals(assembly, commandType.Assembly) || !References(assembly, commandType.Assembly))
            return [];

        foreach (var type in GetTypes(assembly))
        {
            if (!IsCommand(type, commandType))
                continue;

            var paths = ReadSearchPaths(commandType, type);
            if (paths is { Count: > 0 })
                return paths;
        }

        return [];
    }

    private static bool IsCommand(Type type, Type commandType)
    {
        if (type.IsAbstract || type.IsGenericTypeDefinition || !commandType.IsAssignableFrom(type))
            return false;

        return type.GetConstructor(Type.EmptyTypes) is not null;
    }

    private static List<string>? ReadSearchPaths(Type commandType, Type command)
    {
        try
        {
            var instance = Activator.CreateInstance(command);
            var configs = commandType
                .GetField(PyRevitNames.ScriptRuntimeConfigsMember, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(instance);
            if (configs?.GetType().GetProperty(PyRevitNames.SearchPaths)?.GetValue(configs) is not IEnumerable<string> paths)
                return null;

            return paths.Where(path => !string.IsNullOrEmpty(path)).ToList();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<string> FromSessionBuilder()
    {
        try
        {
            var builder = BuilderAssembly();
            var managerType = builder?.GetType(ExtensionManagerTypeName);
            var collectType = builder?.GetType(CollectTypeName);
            var loggerType = builder?.GetType(LoggerTypeName);
            if (managerType is null || collectType is null || loggerType is null)
                return [];

            var uiapp = RevitContext.UiApplication;
            var manager = managerType
                .GetConstructor([typeof(int), typeof(UIApplication), loggerType])
                ?.Invoke([RevitYear(uiapp), uiapp, null]);
            var extensions = managerType
                .GetMethod(PyRevitNames.GetInstalledLibraryExtensions, BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(manager, null);
            var collected = collectType
                .GetMethod(PyRevitNames.Collect, BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, [extensions]);

            return collected is IEnumerable<string> paths
                ? paths.Where(path => !string.IsNullOrEmpty(path)).ToList()
                : [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static Assembly? BuilderAssembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly 
            => string.Equals(assembly.GetName().Name, BuilderAssemblyName, StringComparison.OrdinalIgnoreCase));
    }

    private static int RevitYear(UIApplication uiapp)
    {
        var version = uiapp.Application?.VersionNumber;
        return int.TryParse(version, out var year) ? year : 0;
    }

    private static bool References(Assembly assembly, Assembly runtime)
    {
        var name = runtime.GetName().Name;
        return assembly.GetReferencedAssemblies().Any(reference 
            => string.Equals(reference.Name, name, StringComparison.Ordinal));
    }

    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string? SessionId()
    {
        var runtime = PyRevitLibraryPaths.RuntimeAssembly;
        var envKey = runtime?.GetType(PyRevitNames.DomainStorageKeys)
            ?.GetField(PyRevitNames.EnvVarsDictKey)
            ?.GetValue(null) as string;
        var sessionKey = runtime?.GetType(PyRevitNames.EnvDictionaryKeys)
            ?.GetField(PyRevitNames.SessionUuid)
            ?.GetValue(null) as string;
        if (envKey is null || sessionKey is null)
            return null;
        if (AppDomain.CurrentDomain.GetData(envKey) is not System.Collections.IDictionary dict)
            return null;
        return dict.Contains(sessionKey) ? dict[sessionKey] as string : null;
    }
}
