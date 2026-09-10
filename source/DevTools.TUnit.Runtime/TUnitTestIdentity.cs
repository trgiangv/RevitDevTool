namespace DevTools.TUnit.Runtime;

internal static class TUnitTestIdentity
{
    /// <summary>TUnit.Engine deferred-placeholder UID token.</summary>
    public const string DeferredSuffix = "_Deferred";

    /// <summary>TUnit.Engine inherited-test UID token.</summary>
    public const string InheritedPrefix = "_inherited";

    public static string From(TestMetadata metadata, TUnitCombination combination)
    {
        var method = metadata.MethodMetadata;
        var id = combination.Indices;
        var identity =
            $"{method.Class.Namespace}." +
            $"{TypeNameWithGenerics(metadata.TestClassType)}" +
            $"{FormatParameters(method.Class.Parameters)}." +
            $"{id.ClassSourceIndex}." +
            $"{id.ClassLoopIndex}." +
            $"{metadata.TestMethodName}" +
            $"{FormatMethodGenerics(metadata)}" +
            $"{FormatParameters(method.Parameters)}." +
            $"{id.MethodSourceIndex}." +
            $"{id.MethodLoopIndex}." +
            $"{id.RepeatIndex}";
        return metadata.InheritanceDepth > 0
            ? $"{identity}{InheritedPrefix}{metadata.InheritanceDepth}"
            : identity;
    }

    public static string Deferred(TestMetadata metadata)
    {
        var method = metadata.MethodMetadata;
        return
            $"{method.Class.Namespace}." +
            $"{TypeNameWithGenerics(metadata.TestClassType)}" +
            $"{FormatParameters(method.Class.Parameters)}." +
            $"{metadata.TestMethodName}" +
            $"{FormatParameters(method.Parameters)}{DeferredSuffix}";
    }

    public static string Fallback(string? @namespace, string typeName, string methodName) =>
        $"{@namespace}.{typeName}.{methodName}{DeferredSuffix}";

    public static string TypeNameWithGenerics(Type type)
    {
        var parts = new Stack<string>();
        for (var current = type; current is not null; current = current.DeclaringType)
            parts.Push(AppendGenericName(current));
        return string.Join("+", parts);
    }

    private static string AppendGenericName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var backtick = name.IndexOf('`');
        var prefix = backtick > 0 ? name[..backtick] : name;
        var args = type.GetGenericArguments()
            .Select(argument => argument.FullName ?? argument.Name);
        return $"{prefix}<{string.Join(", ", args)}>";
    }

    private static string FormatMethodGenerics(TestMetadata metadata)
    {
        var args = metadata.GenericMethodTypeArguments;
        return args is not { Length: > 0 } 
            ? string.Empty 
            : $"<{string.Join(",", args.Select(argument => argument.FullName ?? argument.Name))}>";
    }

    private static string FormatParameters(ParameterMetadata[] parameters)
    {
        return parameters.Length == 0 
            ? string.Empty 
            : $"({string.Join(", ", parameters.Select(parameter => parameter.Type.ToString()))})";
    }
}
