using System.Reflection;

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


    private static readonly Dictionary<Type, string> PrimitiveTypeMap = new()
    {
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

    public static string ToPythonType(this Type? t, bool withGenericParams = true)
    {
        if (t == null) return "None";
        if (PrimitiveTypeMap.TryGetValue(t, out var primitive)) return primitive;
        if (t == typeof(Array)) { AddArrayDependency(false); return "Array"; }

        if (t.IsByRef || t.IsPointer)
            return !withGenericParams ? "clr.Reference" : $"clr.Reference[{t.GetElementType().ToPythonType()}]";

        if (t.IsArray)
        {
            AddArrayDependency(true);
            return !withGenericParams ? "Array_1" : $"Array_1[{t.GetElementType().ToPythonType()}]";
        }

        if (t.IsGenericParameter)
            return GetGenericTypeParameterName(t);

        var cleanName = BuildCleanNameWithGenerics(t, withGenericParams);
        var scope = GetScope(t);
        if (string.IsNullOrEmpty(scope))
            AddDependency(t.IsGenericType ? t.GetGenericTypeDefinition() : t);

        return scope + cleanName;
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
}