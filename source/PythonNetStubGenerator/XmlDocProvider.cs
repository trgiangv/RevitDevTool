using System.Reflection;

namespace PythonNetStubGenerator;

/// <summary>
/// Aggregates <see cref="XmlDocReader"/> instances for multiple assemblies and provides
/// a unified documentation lookup with fallback to reflection-based synthetic docs.
/// <para>
/// Priority: XML documentation file → Reflection metadata fallback
/// </para>
/// </summary>
public sealed class XmlDocProvider(Action<string>? logger = null)
{
    private readonly Dictionary<Assembly, XmlDocReader> _readers = new();

    /// <summary>
    /// Register an assembly for documentation lookup.
    /// Attempts to locate and parse the XML documentation file adjacent to the assembly DLL.
    /// </summary>
    public void AddAssembly(Assembly assembly)
    {
        if (_readers.ContainsKey(assembly)) return;

        var reader = XmlDocReader.TryCreateFromAssembly(assembly);
        if (reader != null)
        {
            _readers[assembly] = reader;
            logger?.Invoke($"  XML docs loaded: {assembly.GetName().Name} ({reader.Count} members)");
        }
        else
        {
            logger?.Invoke($"  XML docs not found: {assembly.GetName().Name} (will use reflection fallback)");
        }
    }

    /// <summary>
    /// Get documentation for a member.
    /// Priority: XML doc → Reflection-based fallback.
    /// </summary>
    public DocComment? GetDoc(MemberInfo member)
    {
        // Resolve the assembly from the member
        var assembly = (member as Type)?.Assembly ?? member.DeclaringType?.Assembly;
        if (assembly == null) return null;

        // Priority 1: XML documentation
        if (_readers.TryGetValue(assembly, out var reader))
        {
            var doc = reader.GetDoc(member);
            if (doc is { IsEmpty: false })
                return doc;
        }

        // Priority 2: Reflection-based fallback (similar to IronPython __doc__ generation)
        return GenerateReflectionDoc(member);
    }

    /// <summary>
    /// Total number of assemblies with XML docs loaded.
    /// </summary>
    public int LoadedXmlCount => _readers.Count;

    /// <summary>
    /// Generate synthetic documentation from reflection metadata.
    /// This mimics what IronPython/Python.NET exposes as __doc__:
    /// parameter types, return types, and basic structural information.
    /// </summary>
    private static DocComment? GenerateReflectionDoc(MemberInfo member)
    {
        return member switch
        {
            Type type => GenerateTypeDoc(type),
            MethodInfo method => GenerateMethodDoc(method),
            ConstructorInfo ctor => GenerateConstructorDoc(ctor),
            PropertyInfo prop => GeneratePropertyDoc(prop),
            FieldInfo field => GenerateFieldDoc(field),
            EventInfo evt => GenerateEventDoc(evt),
            _ => null
        };
    }

    /// <summary>
    /// Generate a synthetic class/struct/interface/enum doc from reflection.
    /// </summary>
    private static DocComment GenerateTypeDoc(Type type)
    {
        var kind = type switch
        {
            _ when type.IsEnum => "Enum",
            _ when type.IsInterface => "Interface",
            _ when type.IsValueType => "Struct", { IsAbstract: true, IsSealed: true } => "Static class",
            _ when type.IsAbstract => "Abstract class",
            _ => "Class"
        };

        var sb = new System.Text.StringBuilder(kind);

        var baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object)
                             && baseType != typeof(ValueType) && baseType != typeof(Enum))
        {
            sb.Append($", derived from {baseType.Name}");
        }

        var directInterfaces = type.GetInterfaces()
            .Except(baseType?.GetInterfaces() ?? [])
            .Where(i => i.IsPublic)
            .Select(i => i.Name.Split('`')[0])
            .Take(5)
            .ToArray();

        if (directInterfaces.Length > 0)
            sb.Append($", implements {string.Join(", ", directInterfaces)}");

        sb.Append('.');

        var obsolete = type.GetCustomAttribute<ObsoleteAttribute>();
        return new DocComment
        {
            Summary = sb.ToString(),
            Remarks = obsolete != null ? $"Deprecated: {obsolete.Message ?? "This type is obsolete."}" : null
        };
    }

    /// <summary>
    /// Generate synthetic method doc: parameter types, return type.
    /// Similar to IronPython's restore_clr() approach.
    /// </summary>
    private static DocComment? GenerateMethodDoc(MethodInfo method)
    {
        var parameters = BuildParameterDocs(method.GetParameters());

        string? returns = null;
        if (method.ReturnType != typeof(void))
            returns = FormatTypeName(method.ReturnType);

        string? remarks = null;
        var obsolete = method.GetCustomAttribute<ObsoleteAttribute>();
        if (obsolete != null)
            remarks = $"Deprecated: {obsolete.Message ?? "This method is obsolete."}";

        // Don't generate empty docs for simple methods without useful info
        if (parameters.Count == 0 && returns == null && remarks == null)
            return null;

        return new DocComment { Parameters = parameters, Returns = returns, Remarks = remarks };
    }

    /// <summary>
    /// Generate synthetic constructor doc.
    /// </summary>
    private static DocComment? GenerateConstructorDoc(ConstructorInfo ctor)
    {
        var parameters = BuildParameterDocs(ctor.GetParameters());

        if (parameters.Count == 0) return null;

        return new DocComment { Parameters = parameters };
    }

    /// <summary>
    /// Generate synthetic property doc: type info, read/write access.
    /// </summary>
    private static DocComment GeneratePropertyDoc(PropertyInfo prop)
    {
        var access = (prop.CanRead, prop.CanWrite) switch
        {
            (true, true) => "get/set",
            (true, false) => "get",
            (false, true) => "set",
            _ => ""
        };
        return new DocComment { Summary = $"{FormatTypeName(prop.PropertyType)} ({access})." };
    }

    /// <summary>
    /// Generate synthetic field doc.
    /// </summary>
    private static DocComment GenerateFieldDoc(FieldInfo field)
    {
        var mod = field.IsLiteral ? " (const)" : field.IsInitOnly ? " (readonly)" : "";
        return new DocComment { Summary = $"{FormatTypeName(field.FieldType)}{mod}." };
    }

    /// <summary>
    /// Generate synthetic event doc.
    /// </summary>
    private static DocComment GenerateEventDoc(EventInfo evt)
    {
        var handlerType = evt.EventHandlerType;
        var typeName = FormatTypeName(handlerType);
        return new DocComment { Summary = $"Event with handler type {typeName}." };
    }

    /// <summary>
    /// Build parameter documentation from ParameterInfo[].
    /// </summary>
    private static Dictionary<string, string> BuildParameterDocs(ParameterInfo[] parameters)
    {
        var docs = new Dictionary<string, string>(parameters.Length);
        foreach (var param in parameters)
        {
            var name = param.Name ?? $"arg{param.Position}";
            var desc = FormatTypeName(param.ParameterType);

            if (param.IsOut) desc += " (out)";
            else if (param.ParameterType.IsByRef) desc += " (ref)";

            if (param.HasDefaultValue)
                desc += param.DefaultValue == null ? " (optional)" : $" (default: {param.DefaultValue})";

            docs[name] = desc;
        }
        return docs;
    }

    /// <summary>
    /// Format a .NET type name to a readable short form.
    /// </summary>
    private static string FormatTypeName(Type type)
    {
        if (type == typeof(void)) return "None";
        if (type == typeof(string)) return "str";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "int";
        if (type == typeof(double)) return "float";
        if (type == typeof(float)) return "float";
        if (type == typeof(object)) return "object";

        if (type.IsByRef) return FormatTypeName(type.GetElementType()!);
        if (type.IsArray) return $"Array[{FormatTypeName(type.GetElementType()!)}]";

        if (!type.IsGenericType) return type.Name;
        var name = type.Name.Split('`')[0];
        var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
        return $"{name}[{args}]";

    }
}
