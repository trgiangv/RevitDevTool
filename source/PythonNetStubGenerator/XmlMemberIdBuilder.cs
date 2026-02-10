using System.Reflection;
using System.Text;

namespace PythonNetStubGenerator;

/// <summary>
/// Builds XML documentation member IDs from System.Reflection objects,
/// following the C# XML documentation specification (ECMA-334 §D.4.2).
/// <para>
/// Format: {prefix}:{qualifiedName}
/// T: = Type, M: = Method/Constructor, P: = Property, F: = Field, E: = Event
/// </para>
/// </summary>
public static class XmlMemberIdBuilder
{
    /// <summary>
    /// Build the XML documentation member ID for any supported MemberInfo.
    /// </summary>
    public static string GetMemberId(MemberInfo member)
    {
        return member switch
        {
            Type type => $"T:{GetTypeId(type)}",
            ConstructorInfo ctor => $"M:{GetMethodId(ctor)}",
            MethodInfo method => $"M:{GetMethodId(method)}",
            PropertyInfo prop => $"P:{GetPropertyId(prop)}",
            FieldInfo field => $"F:{GetMemberQualifiedName(field)}",
            EventInfo evt => $"E:{GetMemberQualifiedName(evt)}",
            _ => throw new ArgumentException($"Unsupported member type: {member.GetType().Name}")
        };
    }

    /// <summary>
    /// Build the full type ID including generic arity.
    /// Examples: "System.String", "System.Collections.Generic.List`1",
    /// "Namespace.Outer.Inner" (nested types use . not +).
    /// </summary>
    private static string GetTypeId(Type type)
    {
        if (type.IsGenericParameter) return type.Name;

        // For nested types, recursively build parent chain with '.' separator
        if (type.DeclaringType != null)
            return $"{GetTypeId(type.DeclaringType)}.{GetSimpleTypeName(type)}";

        var ns = type.Namespace;
        var name = GetSimpleTypeName(type);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <summary>
    /// Get the simple type name with generic arity backtick.
    /// e.g. "List`1", "Dictionary`2", "String".
    /// </summary>
    private static string GetSimpleTypeName(Type type)
    {
        if (!type.IsGenericType) return type.Name;

        // For nested generic types, only count the "new" generic params
        var arity = type.GetGenericArguments().Length;
        if (type.DeclaringType is { IsGenericType: true })
            arity -= type.DeclaringType.GetGenericArguments().Length;

        return arity > 0 ? $"{type.Name.Split('`')[0]}`{arity}" : type.Name.Split('`')[0];
    }

    /// <summary>
    /// Build a qualified name for a non-method member (field, event).
    /// </summary>
    private static string GetMemberQualifiedName(MemberInfo member)
    {
        var declaringTypeId = GetTypeId(member.DeclaringType!);
        return $"{declaringTypeId}.{member.Name}";
    }

    /// <summary>
    /// Build a property ID, including indexer parameters if present.
    /// e.g. "Namespace.Class.PropertyName", "Namespace.Class.Item(System.Int32)"
    /// </summary>
    private static string GetPropertyId(PropertyInfo property)
    {
        var baseId = GetMemberQualifiedName(property);
        var indexParams = property.GetIndexParameters();

        if (indexParams.Length == 0) return baseId;

        var paramTypes = string.Join(",",
            indexParams.Select(p => GetParameterTypeId(p.ParameterType)));
        return $"{baseId}({paramTypes})";
    }

    /// <summary>
    /// Build a method/constructor ID with parameter types.
    /// Handles: constructors (#ctor), generic methods (``N), overloads via param types.
    /// </summary>
    private static string GetMethodId(MethodBase method)
    {
        var sb = new StringBuilder();

        // Declaring type
        sb.Append(GetTypeId(method.DeclaringType!));
        sb.Append('.');

        // Method name: constructors → #ctor, others → name
        if (method.IsConstructor)
        {
            sb.Append("#ctor");
        }
        else
        {
            sb.Append(method.Name);

            // Generic method arity: ``N
            if (method is MethodInfo { IsGenericMethodDefinition: true } mi)
            {
                sb.Append("``");
                sb.Append(mi.GetGenericArguments().Length);
            }
        }

        // Parameters
        var parameters = method.GetParameters();
        if (parameters.Length <= 0) return sb.ToString();
        sb.Append('(');
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(GetParameterTypeId(parameters[i].ParameterType));
        }
        sb.Append(')');

        return sb.ToString();
    }

    /// <summary>
    /// Build the XML type reference for a parameter type.
    /// Handles: by-ref (@), pointers (*), arrays ([]), generics ({T1,T2}),
    /// generic parameters (`N for type, ``N for method).
    /// </summary>
    private static string GetParameterTypeId(Type type)
    {
        // By-ref (ref/out): System.Int32@ 
        if (type.IsByRef)
            return GetParameterTypeId(type.GetElementType()!) + "@";

        // Pointer: System.Int32*
        if (type.IsPointer)
            return GetParameterTypeId(type.GetElementType()!) + "*";

        // Array: System.Int32[] or System.Int32[,]
        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            var suffix = rank == 1
                ? "[]"
                : "[" + new string(',', rank - 1) + "]";
            return GetParameterTypeId(type.GetElementType()!) + suffix;
        }

        // Generic parameter from method: ``N (double backtick + position)
        if (type.IsGenericParameter)
        {
            return type.DeclaringMethod != null
                ? $"``{type.GenericParameterPosition}"
                : $"`{type.GenericParameterPosition}";
        }

        // Constructed generic type: System.Collections.Generic.Dictionary`2{`0,System.String}
        if (!type.IsGenericType) return GetTypeId(type);
        var def = type.GetGenericTypeDefinition();
        var defId = GetTypeId(def);
        var args = type.GetGenericArguments();
        var argsStr = string.Join(",", args.Select(GetParameterTypeId));
        return $"{defId}{{{argsStr}}}";
    }
}
