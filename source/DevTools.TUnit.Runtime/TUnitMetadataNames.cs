using System.Collections.Concurrent;
using System.Text;

namespace DevTools.TUnit.Runtime;

/// <summary>
/// ECMA-335 names for MTP <c>TestMethodIdentifierProperty</c>, matching
/// TUnit.Engine <c>MetadataTypeNameFormatter</c> (vstest RFC 0017).
/// </summary>
internal static class TUnitMetadataNames
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    public static string Of(Type type) => Cache.GetOrAdd(type, Format);

    public static string[] Parameters(ParameterMetadata[] parameters)
    {
        if (parameters.Length == 0)
            return [];

        var names = new string[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
            names[index] = Of(parameters[index].Type);
        return names;
    }

    public static string ReturnType(MethodMetadata method) =>
        method.ReturnType is null ? "System.Void" : Of(method.ReturnType);

    private static string Format(Type type)
    {
        var builder = new StringBuilder();
        Append(builder, type);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, Type type)
    {
        if (type.IsGenericParameter)
        {
            AppendGenericParameter(builder, type);
            return;
        }

        if (type.HasElementType)
        {
            AppendElementType(builder, type);
            return;
        }

        if (type is { IsGenericType: true, IsGenericTypeDefinition: false })
        {
            AppendConstructedGeneric(builder, type);
            return;
        }

        builder.Append(type.FullName ?? type.Name);
    }

    private static void AppendGenericParameter(StringBuilder builder, Type type)
    {
        builder.Append(type.DeclaringMethod is null ? '!' : "!!");
        builder.Append(type.GenericParameterPosition);
    }

    private static void AppendElementType(StringBuilder builder, Type type)
    {
        Append(builder, type.GetElementType()!);
        if (type.IsArray)
        {
            builder.Append('[');
            builder.Append(',', type.GetArrayRank() - 1);
            builder.Append(']');
            return;
        }

        if (type.IsPointer)
            builder.Append('*');
        else if (type.IsByRef)
            builder.Append('&');
    }

    private static void AppendConstructedGeneric(StringBuilder builder, Type type)
    {
        Append(builder, type.GetGenericTypeDefinition());
        builder.Append('<');
        var arguments = type.GetGenericArguments();
        for (var index = 0; index < arguments.Length; index++)
        {
            if (index > 0)
                builder.Append(',');
            Append(builder, arguments[index]);
        }

        builder.Append('>');
    }
}
