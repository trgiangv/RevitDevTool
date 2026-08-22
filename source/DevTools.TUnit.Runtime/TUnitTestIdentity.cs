using DevTools.Testing.Abstractions.Contracts;
using TUnit.Core;

namespace DevTools.TUnit.Runtime;

internal static class TUnitTestIdentity
{
    public static string From(TestMetadata metadata, TUnitCombination combination)
    {
        var method = metadata.MethodMetadata;
        var id =
            $"{method.Class.Namespace}." +
            $"{TypeNameWithGenerics(metadata.TestClassType)}" +
            $"{FormatParameters(method.Class.Parameters)}." +
            $"{combination.ClassSourceIndex}." +
            $"{combination.ClassLoopIndex}." +
            $"{metadata.TestMethodName}" +
            $"{FormatMethodGenerics(metadata)}" +
            $"{FormatParameters(method.Parameters)}." +
            $"{combination.MethodSourceIndex}." +
            $"{combination.MethodLoopIndex}." +
            $"{combination.RepeatIndex}";
        return metadata.InheritanceDepth > 0
            ? $"{id}_inherited{metadata.InheritanceDepth}"
            : id;
    }

    public static string Deferred(TestMetadata metadata)
    {
        var method = metadata.MethodMetadata;
        return
            $"{method.Class.Namespace}." +
            $"{TypeNameWithGenerics(metadata.TestClassType)}" +
            $"{FormatParameters(method.Class.Parameters)}." +
            $"{metadata.TestMethodName}" +
            $"{FormatParameters(method.Parameters)}_Deferred";
    }

    public static string Fallback(string? @namespace, string typeName, string methodName) =>
        $"{@namespace}.{typeName}.{methodName}_Deferred";

    public static string TypeNameWithGenerics(Type type)
    {
        var parts = new Stack<string>();
        for (var current = type; current is not null; current = current.DeclaringType)
            parts.Push(AppendGenericName(current));
        return string.Join("+", parts);
    }

    public static TestingDiscoveryHints ToHints(IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        var classes = discovered
            .Select(test => test.TypeName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var methods = discovered
            .Select(test => test.MethodName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new TestingDiscoveryHints(classes, methods);
    }

    static string AppendGenericName(Type type)
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

    static string FormatMethodGenerics(TestMetadata metadata)
    {
        var args = metadata.GenericMethodTypeArguments;
        if (args is not { Length: > 0 })
            return string.Empty;
        return $"<{string.Join(",", args.Select(argument => argument.FullName ?? argument.Name))}>";
    }

    static string FormatParameters(ParameterMetadata[] parameters)
    {
        if (parameters.Length == 0)
            return string.Empty;
        return $"({string.Join(", ", parameters.Select(parameter => parameter.Type.ToString()))})";
    }
}
