using System.Reflection;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Overload-safe invoke through reflection. Never uses
/// <see cref="Type.GetMethod(string,Type[])"/>: generic and non-generic
/// <c>Execute</c> / <c>GetVariable</c> throw
/// <see cref="AmbiguousMatchException"/> on net48.
/// </summary>
internal static class ReflectionBound
{
    internal static object? Call(MethodBase method, object? target, object?[] args)
    {
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    internal static object? Invoke(object target, string name) =>
        Call(FindInstanceMethod(target.GetType(), name, Type.EmptyTypes), target, []);

    internal static object? Invoke(object target, string name, object?[] args)
    {
        var types = new Type[args.Length];
        for (var i = 0; i < args.Length; i++)
            types[i] = args[i]?.GetType() ?? typeof(object);

        return Call(FindInstanceMethod(target.GetType(), name, types), target, args);
    }

    internal static T? Get<T>(object target, string name)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
            return default;

        return Convert<T>(property.GetValue(target));
    }

    internal static MethodInfo FindInstanceMethod(Type type, string name, Type[] argTypes)
    {
        var matches = new List<MethodInfo>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.Name != name || method.IsGenericMethodDefinition)
                continue;
            if (!ParametersCompatible(method, argTypes))
                continue;
            matches.Add(method);
        }

        if (matches.Count == 0)
            throw new MissingMethodException(type.FullName, name);

        if (matches.Count == 1)
            return matches[0];

        var exact = matches.Where(m => ParametersExact(m, argTypes)).ToList();
        if (exact.Count == 1)
            return exact[0];

        return (exact.Count > 0 ? exact : matches)
            .OrderByDescending(m => InheritanceDistance(type, m.DeclaringType!))
            .First();
    }

    internal static MethodInfo? FindGenericInstanceMethod(Type type, string name, int genericArity, Type[] argTypes)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.Name != name || !method.IsGenericMethodDefinition)
                continue;
            if (method.GetGenericArguments().Length != genericArity)
                continue;
            if (!ParametersCompatible(method, argTypes, allowGenericParameter: true))
                continue;
            return method;
        }

        return null;
    }

    internal static T Convert<T>(object? value)
    {
        if (value is T typed)
            return typed;

        if (value is null)
            return default!;

        if (typeof(T) == typeof(bool))
            return (T)(object)System.Convert.ToBoolean(value);

        return (T)System.Convert.ChangeType(value, typeof(T));
    }

    private static bool ParametersCompatible(MethodInfo method, Type[] argTypes, bool allowGenericParameter = false)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != argTypes.Length)
            return false;

        for (var i = 0; i < argTypes.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
                parameterType = parameterType.GetElementType()!;

            if (allowGenericParameter && parameterType.IsGenericParameter)
                continue;

            if (parameterType == argTypes[i] || parameterType.IsAssignableFrom(argTypes[i]))
                continue;

            return false;
        }

        return true;
    }

    private static bool ParametersExact(MethodInfo method, Type[] argTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != argTypes.Length)
            return false;

        for (var i = 0; i < argTypes.Length; i++)
        {
            if (parameters[i].ParameterType != argTypes[i])
                return false;
        }

        return true;
    }

    private static int InheritanceDistance(Type actual, Type declaring)
    {
        var distance = 0;
        for (var current = actual; current is not null; current = current.BaseType)
        {
            if (current == declaring)
                return distance;
            distance++;
        }

        return int.MinValue;
    }
}
