using System.Collections;
using System.Reflection;
using Microsoft.Scripting.Hosting;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Invokes DLR <c>ScriptEngine</c> APIs on the local IronPython 3.4 types, or via
/// <see cref="ReflectionBound"/> when the engine is a different DLR (pyRevit loader).
/// </summary>
internal static class DlrScriptHost
{
    internal const string CreateScopeName = nameof(ScriptEngine.CreateScope);
    internal const string SetVariableName = nameof(ScriptScope.SetVariable);
    internal const string GetVariableName = nameof(ScriptScope.GetVariable);
    internal const string ExecuteName = nameof(ScriptSource.Execute);
    internal const string CreateScriptSourceFromStringName = nameof(ScriptEngine.CreateScriptSourceFromString);
    internal const string GetSearchPathsName = nameof(ScriptEngine.GetSearchPaths);
    internal const string SetSearchPathsName = nameof(ScriptEngine.SetSearchPaths);

    public static object CreateScope(object engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (engine is ScriptEngine local)
            return local.CreateScope();

        return ReflectionBound.Invoke(engine, CreateScopeName)!;
    }

    public static void SetVariable(object scope, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope is ScriptScope local)
        {
            local.SetVariable(name, value);
            return;
        }

        ReflectionBound.Invoke(scope, SetVariableName, [name, value]);
    }

    public static T GetVariable<T>(object scope, string name)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope is ScriptScope local)
            return ReflectionBound.Convert<T>(local.GetVariable(name));

        var generic = ReflectionBound.FindGenericInstanceMethod(scope.GetType(), GetVariableName, 1, [typeof(string)]);
        if (generic is not null)
            return ReflectionBound.Convert<T>(ReflectionBound.Call(generic.MakeGenericMethod(typeof(T)), scope, [name]));

        return ReflectionBound.Convert<T>(ReflectionBound.Invoke(scope, GetVariableName, [name]));
    }

    public static void Execute(object engine, string code, object? scope = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        scope ??= CreateScope(engine);
        if (engine is ScriptEngine local)
        {
            local.CreateScriptSourceFromString(code).Execute((ScriptScope)scope);
            return;
        }

        var source = ReflectionBound.Invoke(engine, CreateScriptSourceFromStringName, [code]);
        ReflectionBound.Invoke(source!, ExecuteName, [scope]);
    }

    public static IList<string> GetSearchPaths(object engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (engine is ScriptEngine local)
            return local.GetSearchPaths().ToList();

        var result = ReflectionBound.Invoke(engine, GetSearchPathsName);
        return result is null
            ? []
            : ((IEnumerable)result).Cast<string>().ToList();
    }

    public static void SetSearchPaths(object engine, IList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(paths);
        if (engine is ScriptEngine local)
        {
            local.SetSearchPaths(paths);
            return;
        }

        ReflectionBound.Invoke(engine, SetSearchPathsName, [paths]);
    }

    public static void Shutdown(object engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (engine is not ScriptEngine local)
            return;

        local.Runtime.Shutdown();
    }

    internal static MethodInfo FindInstanceMethod(Type type, string name, Type[] argTypes) =>
        ReflectionBound.FindInstanceMethod(type, name, argTypes);

    internal static MethodInfo? FindGenericInstanceMethod(Type type, string name, int genericArity, Type[] argTypes) =>
        ReflectionBound.FindGenericInstanceMethod(type, name, genericArity, argTypes);
}
