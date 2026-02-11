using System.Reflection;
// ReSharper disable ConvertToExtensionBlock

namespace PythonNetStubGenerator;

public static class PythonTypes
{
    private static readonly HashSet<Type> AllExportedTypes = [];
    private static readonly HashSet<string?> DirtyNamespaces = [];
    private static readonly HashSet<Type> CurrentTypes = [];
    private static readonly HashSet<string?> CurrentNamespaces = [];
    private static readonly HashSet<Type> OverloadedNonGenericTypes = [];

    public static void CacheOverloadedNonGenericTypes(IEnumerable<Type> stubTypes)
    {
        var namesByNamespace = new Dictionary<string, Dictionary<string, List<Type>>>();
        foreach (var type in stubTypes)
        {
            if (type.DeclaringType != null)
            {
                CacheNestedTypeOverload(type);
                continue;
            }
            CacheTopLevelTypeOverload(type, namesByNamespace);
        }
    }

    private static void CacheNestedTypeOverload(Type type)
    {
        if (type.IsGenericType) return;

        var hasGenericSibling = type.DeclaringType!.GetNestedTypes()
            .Any(sibling => sibling.IsGenericType && sibling.NonGenericName() == type.Name);

        if (hasGenericSibling) OverloadedNonGenericTypes.Add(type);
    }

    private static void CacheTopLevelTypeOverload(Type type, Dictionary<string, Dictionary<string, List<Type>>> namesByNamespace)
    {
        var ns = type.Namespace ?? "";
        var baseName = type.NonGenericName();

        if (!namesByNamespace.TryGetValue(ns, out var typesByName))
            namesByNamespace[ns] = typesByName = new Dictionary<string, List<Type>>();

        if (!typesByName.TryGetValue(baseName, out var typesWithName))
            typesByName[baseName] = typesWithName = [];

        typesWithName.Add(type);

        if (typesWithName.Count <= 1) return;
        foreach (var t in typesWithName)
        {
            if (!t.IsGenericType) OverloadedNonGenericTypes.Add(t);
        }
    }

    public static bool IsOverloadedNonGenericType(this Type type) => OverloadedNonGenericTypes.Contains(type);


    public static bool CurrentUsedGenericArray { get; private set; }
    public static bool CurrentUsedBaseArray { get; private set; }

    public static void AddDependency(Type t)
    {
        var isNewAdd = AllExportedTypes.Add(t);
        if (isNewAdd) DirtyNamespaces.Add(t.Namespace);
        if (t != typeof(Nullable<>)) CurrentTypes.Add(t);
    }

    private static void AddArrayDependency(bool isGeneric)
    {
        AddDependency(typeof(Array));

        if (isGeneric) CurrentUsedGenericArray = true;
        else CurrentUsedBaseArray = true;
    }

    private static void AddNamespaceDependency(string? typeNamespace)
    {
        if (!string.IsNullOrEmpty(typeNamespace))
            CurrentNamespaces.Add(typeNamespace);
    }

    public static List<Type> GetCurrentTypeDependencies() =>
    [
        ..CurrentTypes
    ];
    public static List<string> GetCurrentNamespaceDependencies() =>
    [
        ..CurrentNamespaces.Where(it => !string.IsNullOrEmpty(it)).Select(it => it!)
    ];

    public static void ClearCurrent()
    {
        CurrentTypes.Clear();
        CurrentNamespaces.Clear();

        CurrentUsedGenericArray = false;
        CurrentUsedBaseArray = false;
    }

    public static (string? nameSpace, List<Type> types) RemoveDirtyNamespace()
    {
        var key = DirtyNamespaces.FirstOrDefault();
        DirtyNamespaces.Remove(key);
        if (key == null) return (null, []);
        var results = AllExportedTypes.Where(it => it.Namespace == key).ToList();
        return (key, results);
    }


    private static readonly Dictionary<string, string> ReservedNameMap = new()
    {
        ["from"] = "from_",
        ["del"] = "del_",
        ["None"] = "None_",
    };

    internal static string? SafePythonName(string? s)
        => s != null && ReservedNameMap.TryGetValue(s, out var safe) ? safe : s;

    public static string NonGenericName(this Type t) =>
        t.Name.Split('`')[0];

    public static string NonGenericName(this MethodBase t) =>
        t.Name.Split('`')[0];


    public static string CleanName(this Type t)
    {
        var name = t.NonGenericName();
        if (t.IsGenericType) name = $"{name}_{t.GetGenericArguments().Length}";
        return name;
    }

    public static string CleanName(this MethodBase t)
    {
        var name = t.NonGenericName();
        if (t.IsGenericMethod) name = $"{name}_{t.GetGenericArguments().Length}";
        return name;
    }


    /// <summary>
    /// Maps .NET primitive types to their Python equivalents.
    /// Only includes types that Python.NET actually converts at runtime.
    /// </summary>
    private static readonly Dictionary<Type, string> PrimitiveTypeMap = new()
    {
        // These are the ONLY types that Python.NET actually converts to Python primitives
        [typeof(void)] = "None",
        [typeof(object)] = "typing.Any",
        [typeof(string)] = "str",
        [typeof(char)] = "str",
        [typeof(double)] = "float",
        [typeof(float)] = "float",
        [typeof(bool)] = "bool",
        [typeof(long)] = "int",
        [typeof(int)] = "int",
        [typeof(byte)] = "int",
        [typeof(sbyte)] = "int",
        [typeof(short)] = "int",
        [typeof(uint)] = "int",
        [typeof(ushort)] = "int",
        [typeof(ulong)] = "int",
        [typeof(IntPtr)] = "int",
        [typeof(Type)] = "typing.Type[typing.Any]",
    };

    /// <summary>
    /// Set of delegate types that should be converted to typing.Callable.
    /// This is the main improvement for Python developer experience - 
    /// allows passing Python functions directly where .NET expects delegates.
    /// </summary>
    private static readonly HashSet<Type> CallableDelegateTypes =
    [
        typeof(Action),
        typeof(Action<>),
        typeof(Action<,>),
        typeof(Action<,,>),
        typeof(Action<,,,>),
        typeof(Action<,,,,>),
        typeof(Action<,,,,,>),
        typeof(Action<,,,,,,>),
        typeof(Action<,,,,,,,>),
        typeof(Func<>),
        typeof(Func<,>),
        typeof(Func<,,>),
        typeof(Func<,,,>),
        typeof(Func<,,,,>),
        typeof(Func<,,,,,>),
        typeof(Func<,,,,,,>),
        typeof(Func<,,,,,,,>),
        typeof(Func<,,,,,,,,>),
        typeof(Predicate<>),
        typeof(Comparison<>),
        typeof(Converter<,>),
        typeof(EventHandler),
        typeof(EventHandler<>),
    ];

    public static string ToPythonType(this Type? t, bool withGenericParams = true)
    {
        if (t == null) return "None";
        
        // 1. Check primitive types (types Python.NET actually converts)
        if (PrimitiveTypeMap.TryGetValue(t, out var primitive)) 
            return primitive;
        
        // 2. Handle Array base type
        if (t == typeof(Array)) 
        { 
            AddArrayDependency(false); 
            return "Array"; 
        }

        // 3. Handle by-ref and pointer types
        if (t.IsByRef || t.IsPointer)
            return ConvertByRefOrPointer(t, withGenericParams);

        // 4. Handle array types (T[])
        if (t.IsArray)
            return ConvertArrayType(t, withGenericParams);

        // 5. Handle generic parameters (T, TResult, etc.)
        if (t.IsGenericParameter)
            return GetGenericTypeParameterName(t);

        // 6. Convert delegate types to Callable (improves Python DX for callbacks)
        if (withGenericParams && TryGetCallableType(t, out var callableType))
            return callableType;

        // 7. Default: use .NET type name with scope (preserve .NET types!)
        return BuildDefaultTypeName(t, withGenericParams);
    }

    private static string ConvertByRefOrPointer(Type t, bool withGenericParams)
    {
        if (!withGenericParams) return "clr.Reference";
        return $"clr.Reference[{t.GetElementType().ToPythonType()}]";
    }

    private static string ConvertArrayType(Type t, bool withGenericParams)
    {
        AddArrayDependency(true);
        if (!withGenericParams) return "Array_1";
        return $"Array_1[{t.GetElementType().ToPythonType()}]";
    }

    private static string BuildDefaultTypeName(Type t, bool withGenericParams)
    {
        var cleanName = BuildCleanNameWithGenerics(t, withGenericParams);
        var scope = GetScope(t);
        if (string.IsNullOrEmpty(scope))
            AddDependency(t.IsGenericType ? t.GetGenericTypeDefinition() : t);
        return scope + cleanName;
    }

    /// <summary>
    /// Try to convert a delegate type to Python's typing.Callable format.
    /// This is the ONLY .NET -> Python type conversion we do (besides primitives),
    /// because it allows Python functions to be passed where .NET expects delegates.
    /// </summary>
    private static bool TryGetCallableType(Type t, out string callableType)
    {
        callableType = "";

        // Non-generic Action
        if (t == typeof(Action))
        {
            callableType = "typing.Callable[[], None]";
            return true;
        }

        // Non-generic EventHandler
        if (t == typeof(EventHandler))
        {
            callableType = "typing.Callable[[typing.Any, System.EventArgs], None]";
            return true;
        }

        if (!t.IsGenericType)
            return false;

        var genericDef = t.GetGenericTypeDefinition();
        if (!CallableDelegateTypes.Contains(genericDef))
            return false;

        var genericArgs = t.GetGenericArguments();
        var typeName = genericDef.Name.Split('`')[0];

        callableType = typeName switch
        {
            "Action" => BuildActionCallable(genericArgs),
            "Func" => BuildFuncCallable(genericArgs),
            "Predicate" => BuildPredicateCallable(genericArgs),
            "Comparison" => BuildComparisonCallable(genericArgs),
            "Converter" => BuildConverterCallable(genericArgs),
            "EventHandler" => BuildEventHandlerCallable(genericArgs),
            _ => ""
        };

        return !string.IsNullOrEmpty(callableType);
    }

    private static string BuildActionCallable(Type[] genericArgs)
    {
        var paramTypes = genericArgs.Select(arg => arg.ToPythonType());
        return $"typing.Callable[[{string.Join(", ", paramTypes)}], None]";
    }

    private static string BuildFuncCallable(Type[] genericArgs)
    {
        var paramTypes = genericArgs.Take(genericArgs.Length - 1).Select(arg => arg.ToPythonType());
        var returnType = genericArgs.Last().ToPythonType();
        return $"typing.Callable[[{string.Join(", ", paramTypes)}], {returnType}]";
    }

    private static string BuildPredicateCallable(Type[] genericArgs)
    {
        var paramType = genericArgs[0].ToPythonType();
        return $"typing.Callable[[{paramType}], bool]";
    }

    private static string BuildComparisonCallable(Type[] genericArgs)
    {
        var paramType = genericArgs[0].ToPythonType();
        return $"typing.Callable[[{paramType}, {paramType}], int]";
    }

    private static string BuildConverterCallable(Type[] genericArgs)
    {
        var inputType = genericArgs[0].ToPythonType();
        var outputType = genericArgs[1].ToPythonType();
        return $"typing.Callable[[{inputType}], {outputType}]";
    }

    private static string BuildEventHandlerCallable(Type[] genericArgs)
    {
        if (genericArgs.Length == 0)
            return "typing.Callable[[typing.Any, System.EventArgs], None]";
        
        var eventArgsType = genericArgs[0].ToPythonType();
        return $"typing.Callable[[typing.Any, {eventArgsType}], None]";
    }

    private static string BuildCleanNameWithGenerics(Type t, bool withGenericParams)
    {
        var cleanName = t.CleanName();
        if (!withGenericParams) return cleanName;

        var generics = GetGenerics(t);
        if (generics.Count == 0) return cleanName;

        var pythonTypeArgs = generics.Select(it => it.ToPythonType()).CommaJoin();
        if (t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            cleanName = "typing.Optional";

        return $"{cleanName}[{pythonTypeArgs}]";
    }

    private static string GetScope(Type type)
    {
        var s = type.DeclaringType?.ToPythonType(false);
        if (s != null) return $"{s}.";

        var cleanName = type.CleanName();
        if (!SymbolScope.Scopes.Any(it => it.HasConflict(cleanName, type.Namespace))) return "";
        AddNamespaceDependency(type.Namespace);
        return $"{type.Namespace}.";
    }


    private static List<Type> GetGenerics(Type type)
    {
        IEnumerable<Type> result = type.GetGenericArguments();
        if (type.IsGenericType) AddDependency(type.GetGenericTypeDefinition());
        return result.ToList();
    }

    private static string GetGenericTypeParameterName(Type t)
    {
        var currentScope = ClassScope.Current;

        var method = t.DeclaringMethod;
        var declType = t.DeclaringType;

        string basePrefix;
        if (method != null) basePrefix = method.CleanName();
        else if (declType != null) basePrefix = declType.CleanName();
        else throw new Exception("Where did this type come from?");

        var baseName = basePrefix + "_" + t.Name;

        var currentClassName = currentScope?.PythonClass;
        if (currentScope == null || currentClassName == basePrefix) return baseName;
        if (method == null) return currentClassName + "_" + baseName;

        return currentScope.PythonClass + "_" + baseName;
    }

    public static bool IsReservedWord(string propertyName)
        => ReservedNameMap.ContainsKey(propertyName);

    /// <summary>
    /// Gets Python type for parameter, handling nullable default values.
    /// </summary>
    public static string ToPythonType(this ParameterInfo param)
    {
        var baseType = param.ParameterType.ToPythonType();
        if (param is { HasDefaultValue: true, DefaultValue: null } && baseType != "None")
            return $"{baseType} | None";
        return baseType;
    }

    /// <summary>
    /// Gets Python representation of parameter's default value.
    /// </summary>
    public static string? ToPythonDefault(this ParameterInfo param)
    {
        if (!param.HasDefaultValue) return null;
        
        return param.DefaultValue switch
        {
            null => "None",
            true => "True",
            false => "False",
            string s => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
            char c => $"\"{c}\"",
            Enum e => $"{param.ParameterType.Name}.{e}",
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
            int or long or short or byte or uint or ulong or ushort or sbyte => param.DefaultValue.ToString()!,
            _ => "..."
        };
    }
}