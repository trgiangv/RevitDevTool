using System.Collections;
using System.Reflection;
using System.Text;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace PythonNetStubGenerator;

public static class StubWriter
{
    /// <summary>
    /// The documentation provider for the current generation run.
    /// Set before calling GetStub. Null means no docstrings will be written.
    /// </summary>
    public static XmlDocProvider? DocProvider { get; set; }

    public static string GetStub(string nameSpace, IEnumerable<Type> stubTypes)
    {
        var types = stubTypes as Type[] ?? stubTypes.ToArray();
        PythonTypes.CacheOverloadedNonGenericTypes(types);
        var typeGroups = types
            .Where(it => it.IsVisible) // Avoid internal classes
            .Where(it => it.DeclaringType == null) // Avoid Nested classes, they're handled later
            .GroupBy(it => it.NonGenericName())
            .OrderBy(it => it.Key).ToList();

        var sb = new StringBuilder();

        var reservedSymbols = typeGroups.Select(it => it.Key);

        using (new SymbolScope(reservedSymbols, nameSpace))
        {
            foreach (var typeGroup in typeGroups)
                WriteTypeGroup(sb, typeGroup.Key, typeGroup);

        }
        var body = sb.ToString();

        //Prepend dependencies
        var deps = GetDependencies(nameSpace, body);
        return deps + body;
    }


    private static void WriteTypeGroup(StringBuilder sb, string typeName, IEnumerable<Type> typeList)
    {
        var types = typeList.ToList();

        if (types.Contains(typeof(Array)))
        {
            WriteArrayType(sb, types.First());
            return;
        }

        if (types.Count == 1 && !types.Any(it => it.IsGenericTypeDefinition))
        {
            WriteType(sb, types.First());
            return;
        }

        var genericMetaclass = $"{typeName}_GenericClasses";
        var currentGenerics = ClassScope.AccessibleGenerics.Select(it => it.ToPythonType()).CommaJoin();
        if (!string.IsNullOrEmpty(currentGenerics)) currentGenerics = $"[{currentGenerics}]";


        var genericTypes = types.Where(it => it.IsGenericTypeDefinition).ToList();
        if (genericTypes.Any()) WriteTypeOverload(sb, genericMetaclass, genericTypes);

        var nonGeneric = types.FirstOrDefault(it => !it.IsGenericTypeDefinition);

        if (nonGeneric != null)
        {
            types.Remove(nonGeneric);
            var nonGenericName = $"{typeName}_0";
            sb.Indent().AppendLine($"class {typeName}({nonGenericName}, metaclass ={genericMetaclass}{currentGenerics}): ...");
            WriteType(sb, nonGeneric, nonGenericName);
        }
        else
        {
            sb.Indent().AppendLine($"{typeName} : {genericMetaclass}{currentGenerics}");
        }

        foreach (var type in types)
            WriteType(sb, type);
    }

    private static void WriteArrayType(StringBuilder sb, Type arrayType)
    {
        const string genericMetaclass = "Array_GenericClasses";

        sb.AppendLine();
        sb.Indent().AppendLine($"class {genericMetaclass}(abc.ABCMeta):");
        using (new IndentScope())
        {
            sb.Indent().AppendLine("Generic_Array_1_T = typing.TypeVar('Generic_Array_1_T')");
            sb.Indent().AppendLine("def __getitem__(self, types : typing.Type[Generic_Array_1_T]) -> typing.Type[Array_1[Generic_Array_1_T]]: ...");
        }

        sb.AppendLine();

        var nonGenericName = "Array_0";
        sb.Indent().AppendLine($"class Array({nonGenericName}, metaclass ={genericMetaclass}): ...");

        sb.AppendLine();
        sb.Indent().AppendLine("Array_1_T = typing.TypeVar('Array_1_T', covariant=True)");
        sb.Indent().AppendLine("class Array_1(Array_0, typing.Generic[Array_1_T]):...");
        sb.AppendLine();

        WriteType(sb, arrayType, nonGenericName);
    }

    private static void WriteClassHeader(
        ClassScope classScope,
        StringBuilder sb,
        string className,
        List<string>? classArguments = null,
        Dictionary<Type, string>? genericAliases = null)
    {
        classArguments ??= [];
        var generics = ClassScope.AccessibleGenerics.ToList();

        EmitTypeVariableDeclarations(sb, generics, genericAliases);
        PrependGenericDefinition(classArguments, generics, genericAliases);

        var argumentsString = classArguments.CommaJoin();
        if (!string.IsNullOrEmpty(argumentsString))
            argumentsString = $"({argumentsString})";

        sb.Indent().AppendLine($"class {className}{argumentsString}:");
        classScope.EnterIndent();

        if (!string.IsNullOrEmpty(classScope.OutsideAccessor))
            EmitGenericShadows(sb, generics, genericAliases, classScope.OutsideAccessor);
    }

    /// <summary>Write TypeVar declarations for all accessible generics.</summary>
    private static void EmitTypeVariableDeclarations(
        StringBuilder sb, List<Type> generics, Dictionary<Type, string>? genericAliases)
    {
        var written = new HashSet<string>();
        foreach (var generic in generics)
        {
            if (genericAliases != null && genericAliases.TryGetValue(generic, out var alias))
            {
                if (written.Add(alias))
                    WriteTypeVariable(sb, generic, alias);
            }
            else
            {
                WriteTypeVariable(sb, generic);
            }
        }
    }

    /// <summary>Insert typing.Generic[...] as first class argument if generics exist.</summary>
    private static void PrependGenericDefinition(
        List<string> classArguments, List<Type> generics, Dictionary<Type, string>? genericAliases)
    {
        if (generics.Count == 0) return;

        var genericArgs = generics
            .Select(it => ResolveGenericAlias(it, genericAliases))
            .Distinct()
            .CommaJoin();
        classArguments.Insert(0, $"typing.Generic[{genericArgs}]");
    }

    /// <summary>Write inner-scope aliases for generics inherited from outer scope.</summary>
    private static void EmitGenericShadows(
        StringBuilder sb, List<Type> generics, Dictionary<Type, string>? genericAliases, string outsideAccessor)
    {
        var written = new HashSet<string>();
        foreach (var generic in generics)
        {
            var name = generic.ToPythonType();
            var outerName = genericAliases?.TryGetValue(generic, out var alias) == true ? alias : name;
            var aliasDef = $"{name} = {outsideAccessor}{outerName}";
            if (written.Add(aliasDef))
                sb.Indent().AppendLine(aliasDef);
        }
    }

    private static string ResolveGenericAlias(Type generic, Dictionary<Type, string>? aliases)
        => aliases?.TryGetValue(generic, out var alias) == true ? alias : generic.ToPythonType();

    private static void WriteTypeOverload(StringBuilder sb, string overloadClassName, List<Type> types)
    {
        sb.AppendLine();
        
        var externalGenerics = ClassScope.AccessibleGenerics.ToList();
        var newGenerics = Enumerable.Empty<Type>();
        using var classScope = new ClassScope(overloadClassName, newGenerics, false);
        WriteClassHeader(classScope, sb, overloadClassName, classArguments:
        [
            "abc.ABCMeta"
        ]);

        foreach (var type in types)
        {
            var args = type.GetGenericArguments().Skip(externalGenerics.Count).ToArray();

            var targetType = type.ToPythonType(false);

            const string prefix = "Generic_";

            var typeVarList = args.Select(arg => $"{prefix}{arg.ToPythonType()}").ToList();
            var typeVarString = externalGenerics.Select(it => it.ToPythonType()).Concat(typeVarList).CommaJoin();

            var typeArgsList = typeVarList.Select(typeVar => $"typing.Type[{typeVar}]");
            var typeArgString = typeArgsList.CommaJoin();

            switch (args.Length)
            {
                case 0:
                    sb.Indent().AppendLine($"def __call__(self) -> {targetType}[{typeVarString}]: ...");
                    continue;
                case > 1:
                    typeArgString = $"typing.Tuple[{typeArgString}]";
                    break;
            }

            foreach (var arg in args)
            {
                WriteTypeVariable(sb, arg, $"{prefix}{arg.ToPythonType()}", writeVariance: false);
            }


            if (types.Count > 1) sb.Indent().AppendLine("@typing.overload");
            sb.Indent().AppendLine($"def __getitem__(self, types : {typeArgString}) -> typing.Type[{targetType}[{typeVarString}]]: ...");
        }

        sb.AppendLine();
    }

    private static void WriteType(StringBuilder sb, Type type, string? classNameOverride = null)
    {
        sb.AppendLine();
        if (type.IsEnum)
        {
            WriteEnum(sb, type);
            sb.AppendLine();
            return;
        }

        var className = classNameOverride ?? type.CleanName();
        
        var typeArguments = new List<Type>();

        if (type.IsGenericTypeDefinition)
        {
            typeArguments.AddRange(type.GetGenericArguments());
        }
        
        using (var classScope = new ClassScope(className, typeArguments, typeArguments.Any()))
        {
            var args = GetClassArguments(type);
            WriteClassHeader(classScope, sb, className, args);

            // Write class docstring
            var classDoc = DocProvider?.GetDoc(type);
            var wroteDocstring = WriteDocstring(sb, classDoc);
            var wroteMember = wroteDocstring;

            wroteMember |= WriteConstructors(type, sb);
            wroteMember |= WriteFields(type, sb);
            wroteMember |= WriteProperties(type, sb);
            wroteMember |= WriteMethods(type, sb);
            wroteMember |= WriteNestedTypes(sb, type);

            if (!wroteMember) sb.Indent().AppendLine("pass");
        }

        sb.AppendLine();
    }

    private static bool WriteNestedTypes(StringBuilder sb, Type stubType)
    {
        var nestedTypeGroups = stubType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(it => it.Name)
            .GroupBy(it => it.NonGenericName());

        var wroteGroup = false;
        foreach (var typeGroup in nestedTypeGroups)
        {
            WriteTypeGroup(sb, typeGroup.Key, typeGroup);
            wroteGroup = true;
        }

        return wroteGroup;
    }

    private static string GetDependencies(string nameSpace, string body)
    {
        var sb = new StringBuilder();

        AppendUtilityImports(sb, body);
        AppendNamespaceImports(sb);
        AppendTypeDependencyImports(sb, nameSpace);

        return sb.ToString();
    }

    private static void AppendUtilityImports(StringBuilder sb, string body)
    {
        var deps = new List<string>(3);
        if (body.Contains("typing.")) deps.Add("typing");
        if (body.Contains("clr.")) deps.Add("clr");
        if (body.Contains("abc.")) deps.Add("abc");
        if (deps.Count > 0)
            sb.Indent().AppendLine("import " + deps.CommaJoin());
    }

    private static void AppendNamespaceImports(StringBuilder sb)
    {
        foreach (var ns in PythonTypes.GetCurrentNamespaceDependencies())
            sb.AppendLine($"import {ns}");
    }

    private static void AppendTypeDependencyImports(StringBuilder sb, string currentNameSpace)
    {
        var depsByNamespace = PythonTypes.GetCurrentTypeDependencies().GroupBy(it => it.Namespace);

        foreach (var group in depsByNamespace)
        {
            if (group.Key == currentNameSpace) continue;
            var types = group.Select(it => it.GetRootType().ToPythonType(false)).Distinct().ToList();
            ResolveArrayTypeImport(types, group.Key);
            sb.AppendLine($"from {group.Key} import {types.CommaJoin()}");
        }
    }

    /// <summary>Replace the generic Array type import with concrete Array/Array_1 names.</summary>
    private static void ResolveArrayTypeImport(List<string> types, string? groupNamespace)
    {
        var arrayTypeStr = typeof(Array).ToPythonType(false);
        if (groupNamespace != typeof(Array).Namespace) return;

        var index = types.IndexOf(arrayTypeStr);
        if (index < 0) return;

        var arrayNames = new List<string>(2);
        if (PythonTypes.CurrentUsedBaseArray) arrayNames.Add("Array");
        if (PythonTypes.CurrentUsedGenericArray) arrayNames.Add("Array_1");
        types[index] = arrayNames.CommaJoin();
    }


    private static Type GetRootType(this Type type)
    {
        while (true)
        {
            if (type.DeclaringType == null) return type;
            type = type.DeclaringType;
        }
    }

    private static List<string> GetClassArguments(Type type)
    {
        var args = new List<string>();

        AddBaseTypeArgument(args, type.BaseType);
        AddDirectInterfaceArguments(args, type);
        AddAbstractMarker(args, type);

        return args;
    }

    private static void AddBaseTypeArgument(List<string> args, Type? baseType)
    {
        if (baseType == null || baseType == typeof(object) || baseType == typeof(ValueType))
            return;

        args.Add(FormatTypeArgument(baseType));
    }

    private static void AddDirectInterfaceArguments(List<string> args, Type type)
    {
        var interfaces = type.GetInterfaces().OrderByDescending(GetInterfaceDepth).ToList();
        var inherited = CollectInheritedInterfaces(type.BaseType, interfaces);

        foreach (var iface in interfaces)
        {
            if (iface.IsVisible && !inherited.Contains(iface))
                args.Add(FormatTypeArgument(iface));
        }
    }

    /// <summary>Collect interfaces that are already inherited (from base type or from other interfaces).</summary>
    private static HashSet<Type> CollectInheritedInterfaces(Type? baseType, List<Type> interfaces)
    {
        var inherited = new HashSet<Type>();

        if (baseType != null)
            foreach (var i in baseType.GetInterfaces())
                inherited.Add(i);

        foreach (var iface in interfaces)
            foreach (var parent in iface.GetInterfaces())
                if (parent != iface) inherited.Add(parent);

        return inherited;
    }

    private static void AddAbstractMarker(List<string> args, Type type)
    {
        if (type.IsInterface)
            args.Add("typing.Protocol");
        else if (type.IsAbstract && type.BaseType?.IsAbstract != true)
            args.Add("abc.ABC");
    }

    private static string FormatTypeArgument(Type type)
    {
        var name = type.ToPythonType();
        return type.IsOverloadedNonGenericType() ? name + "_0" : name;
    }

    private static readonly Dictionary<Type, int> InterfaceDepthCache = new();
    private static int GetInterfaceDepth(Type t)
    {
        if (InterfaceDepthCache.TryGetValue(t, out var val)) return val;
        var interfaces = t.GetInterfaces();
        var depth = interfaces.Length == 0 ? 0 : interfaces.Max(GetInterfaceDepth) + 1;
        InterfaceDepthCache[t] = depth;
        return depth;
    }

    private static void WriteTypeVariable(StringBuilder sb, Type typeVariable, string? customName = null, bool writeVariance = true)
    {
        var varName = customName ?? $"{typeVariable.ToPythonType()}";
        sb.Indent().Append($"{varName} = typing.TypeVar('{varName}'");

        if (writeVariance)
        {
            var covariant = typeVariable.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Covariant);
            var contravariant = typeVariable.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Contravariant);


            if (contravariant) sb.Append(", contravariant=True");
            else if (covariant) sb.Append(", covariant=True");
        }

        var bound = GetTypeVarBound(typeVariable);

        if (!string.IsNullOrEmpty(bound)) sb.Append(", bound=" + bound);
        sb.AppendLine(")");
    }

    private static string? GetTypeVarBound(Type typeVariable)
    {
        var constraints = typeVariable.GetGenericParameterConstraints().ToList();
        constraints.RemoveAll(it => it == typeof(ValueType));

        if (constraints.Count <= 1) return null;

        if (constraints.Count == 1) return constraints[0].ToPythonType();

        var types = constraints.Select(it => it.ToPythonType()).CommaJoin();
        return $"Union[{types}]";
    }

    private static bool WriteMethods(Type stubType, StringBuilder sb)
    {
        var methodGroups = stubType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(it => it, new MethodComparer())
            .Where(it => !IsPropertyAccessor(it) && it.DeclaringType == stubType)
            .GroupBy(it => it.NonGenericName())
            .OrderBy(infos => NeedsMethodGroup(infos.ToList()))
            .ToArray();

        var didWrite = false;
        foreach (var methodGroup in methodGroups)
        {
            didWrite |= WriteMethodGroupOrSimple(sb, stubType, methodGroup);
        }

        WriteIEnumerableIterator(sb, stubType);
        return didWrite;
    }

    private static bool WriteMethodGroupOrSimple(
        StringBuilder sb, Type stubType, IGrouping<string, MethodInfo> methodGroup)
    {
        var methods = methodGroup.ToList();
        if (!NeedsMethodGroup(methods))
        {
            return methods.Aggregate(false, (current, method) => current | WriteSimpleMethod(sb, method, methods.Count > 1));
        }

        if (stubType.IsInterface && methods.Any(it => it is { IsStatic: true, IsAbstract: true }))
            return false;

        sb.Indent().AppendLine($"# Skipped {methodGroup.Key} due to it being static, abstract and generic.");
        WriteMethodGroup(sb, methodGroup, methodGroup.Key);
        return true;
    }

    private static void WriteIEnumerableIterator(StringBuilder sb, Type stubType)
    {
        if (stubType == typeof(IEnumerable))
            sb.Indent().AppendLine("def __iter__(self) -> typing.Iterator[typing.Any]: ...");
        else if (stubType == typeof(IEnumerable<>))
        {
            var elementType = stubType.GetGenericArguments()[0].ToPythonType();
            sb.Indent().AppendLine($"def __iter__(self) -> typing.Iterator[{elementType}]: ...");
        }
    }

    private static bool IsPropertyAccessor(MethodInfo it)
        => it.IsSpecialName && (it.Name.StartsWith("set_") || it.Name.StartsWith("get_")
                                || it.Name.StartsWith("add_") || it.Name.StartsWith("remove_"));

    private static bool NeedsMethodGroup(List<MethodInfo> methods)
        => (!methods.Any(IsOperator) && methods.Count > 1) || methods.Any(it => it.IsGenericMethodDefinition);

    private static bool IsOperator(MethodBase method) => method.IsSpecialName && method.Name.StartsWith("op_");

    private static bool WriteConstructors(Type stubType, StringBuilder sb)
    {
        // constructors
        // sort for consistent output
        var constructors = stubType.GetConstructors(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(it => it.Name + string.Join("_", it.GetParameters().Select(p => p.Name)))
            .ToArray();
        
        var existingSignatures = new HashSet<string>();
        var constructorsToAdd = new List<ConstructorInfo>();
        foreach (var constructor in constructors)
        {

            var signature = GetUniqueMethodSignature(constructor);

            if (!existingSignatures.Add(signature))
            {
                var dotnetParams = constructor.GetParameters().Select(it => $"{it.Name} : {it.ParameterType.Name}");
                var dotnetSignature = dotnetParams.CommaJoin();
                sb.Indent().AppendLine($"# Constructor {constructor.Name}({dotnetSignature}) was skipped since it collides with above method");
                continue;
            }

            constructorsToAdd.Add(constructor);
        }

        foreach (var constructorInfo in constructorsToAdd)
        {
            WriteSimpleMethod(sb, constructorInfo, constructorsToAdd.Count > 1);
        }

        return constructorsToAdd.Count > 0;
    }

    private static bool WriteProperties(Type stubType, StringBuilder sb)
    {
        var properties = stubType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(it => it.Name)
            .ToArray();

        foreach (var property in properties)
        {
            if (ShouldSkipProperty(property, sb)) continue;
            WriteProperty(property, sb);
        }

        return properties.Length > 0;
    }

    private static bool ShouldSkipProperty(PropertyInfo property, StringBuilder sb)
    {
        if (!PythonTypes.IsReservedWord(property.Name)) return false;
        sb.Indent().AppendLine($"# Skipped property {property.Name} since it is a reserved python word. Use reflection to access.");
        return true;
    }

    private static void WriteProperty(PropertyInfo property, StringBuilder sb)
    {
        var context = GetPropertyContext(property);
        WritePropertyGetter(property, context, sb);
        if (property.CanWrite)
            WritePropertySetter(property, context, sb);
    }

    private static (bool IsStatic, string FirstParam, string PropertyType, string GetterType) GetPropertyContext(PropertyInfo property)
    {
        var isStatic = property.GetAccessors(true)[0].IsStatic;
        var propertyType = property.PropertyType.ToPythonType();
        return (
            IsStatic: isStatic,
            FirstParam: isStatic ? "cls" : "self",
            PropertyType: propertyType,
            GetterType: property.CanRead ? propertyType : "None");
    }

    private static void WritePropertyGetter(
        PropertyInfo property,
        (bool IsStatic, string FirstParam, string PropertyType, string GetterType) context,
        StringBuilder sb)
    {
        if (context.IsStatic) sb.Indent().AppendLine("@classmethod");
        sb.Indent().AppendLine("@property");

        var doc = DocProvider?.GetDoc(property);
        if (doc is not { IsEmpty: false })
        {
            sb.Indent().AppendLine($"def {property.Name}({context.FirstParam}) -> {context.GetterType}: ...");
            return;
        }

        sb.Indent().AppendLine($"def {property.Name}({context.FirstParam}) -> {context.GetterType}:");
        using (new IndentScope())
        {
            var summary = doc.Summary ?? doc.Value;
            if (!string.IsNullOrWhiteSpace(summary))
                sb.Indent().AppendLine($"\"\"\"{EscapeDocstring(summary!)}\"\"\"");
            sb.Indent().AppendLine("...");
        }
    }

    private static void WritePropertySetter(
        PropertyInfo property,
        (bool IsStatic, string FirstParam, string PropertyType, string GetterType) context,
        StringBuilder sb)
    {
        if (context.IsStatic) sb.Indent().AppendLine("@classmethod");
        sb.Indent().AppendLine($"@{property.Name}.setter");
        sb.Indent().AppendLine($"def {property.Name}({context.FirstParam}, value: {context.PropertyType}) -> {context.GetterType}: ...");
    }

    private static bool WriteFields(Type stubType, StringBuilder sb)
    {
        var fields = stubType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(it => it.Name)
            .ToArray();

        foreach (var field in fields)
        {
            var type = field.FieldType.ToPythonType();
            sb.Indent().AppendLine($"{field.Name} : {type}");
        }

        return fields.Length > 0;
    }

    private static void WriteMethodGroup(StringBuilder sb, IEnumerable<MethodInfo> methodGroup, string methodName)
    {
        var infos = methodGroup.OrderBy(it => it, new MethodComparer()).ToList();

        sb.AppendLine();


        var className = $"{methodName}_MethodGroup";

        var currentGenerics = ClassScope.AccessibleGenerics.Select(it => it.ToPythonType()).CommaJoin();

        if (!string.IsNullOrEmpty(currentGenerics)) currentGenerics = $"[{currentGenerics}]";
        sb.Indent().AppendLine($"{methodName} : {className}{currentGenerics}");

        using (var classScope = new ClassScope(className, [], false))
        {
            WriteClassHeader(classScope, sb, className);
            // We want to merge methods with the same amount of parameters with the same bounds
            // (since the python type system can't distinguish between them)
            string GetBoundsKey(MethodInfo it) =>
                it.GetGenericArguments().Select(GetTypeVarBound)!.CommaJoin();

            var genericMethodGroups = infos
                .Where(it => it.IsGenericMethodDefinition)
                .GroupBy(GetBoundsKey)
                .ToList();

            var hasGenericOverloads = genericMethodGroups.Count > 1;

            foreach (var methods in genericMethodGroups)
            {
                WriteGenericMethodAccessors(sb, methods, hasGenericOverloads);
            }

            var callsToAdd = GetMethodCallers(infos, true);


            foreach (var call in callsToAdd)
            {
                sb.Indent().AppendLine(call);
            }


        }
        sb.AppendLine();
    }


    private static List<string> GetMethodCallers(List<MethodInfo> infos, bool skipHardGenerics)
    {
        var callsToAdd = BuildDeduplicatedCalls(infos, skipHardGenerics);
        return ApplyOverloadDecorators(callsToAdd);
    }

    private static List<string> BuildDeduplicatedCalls(List<MethodInfo> infos, bool skipHardGenerics)
    {
        var calls = new List<string>();
        var existingSignatures = new HashSet<string>();

        foreach (var method in infos)
        {
            if (skipHardGenerics && method.IsGenericMethodDefinition)
                continue;

            var signature = GetUniqueMethodSignature(method);
            if (!existingSignatures.Add(signature))
            {
                var dotnetParams = method.GetParameters().Select(it => $"{it.Name} : {it.ParameterType.Name}");
                calls.Add($"# Method {method.Name}({dotnetParams.CommaJoin()}) was skipped since it collides with above method");
                continue;
            }

            var parameters = GetParameters(method, true);
            calls.Add($"def __call__({parameters}) -> {method.ReturnType.ToPythonType()}:...");
        }

        return calls;
    }

    private static List<string> ApplyOverloadDecorators(List<string> calls)
    {
        var actualMethodCount = calls.Count(it => !it.TrimStart().StartsWith("#"));
        var result = new List<string>(calls.Count * 2);

        foreach (var call in calls)
        {
            if (actualMethodCount > 1 && !call.StartsWith("#"))
                result.Add("@typing.overload");
            result.Add(call);
        }

        return result;
    }

    private static string GetUniqueMethodSignature(MethodBase method)
        => method.GetParameters().Select(it => NormalizeNumericType(it.ParameterType.ToPythonType())).CommaJoin();

    /// <summary>Normalize float/bool to int since Python can't distinguish these overloads.</summary>
    private static string NormalizeNumericType(string typeName)
        => typeName switch
        {
            "float" or "bool" => "int",
            _ => typeName.Replace("[float]", "[int]").Replace("[bool]", "[int]")
        };

    private static void WriteGenericMethodAccessors(
        StringBuilder sb,
        IEnumerable<MethodInfo> methods,
        bool hasGenericOverloads
    )
    {
        var methodInfos = methods.ToList();

        // use first method to get info
        var templateMethod = methodInfos[0];
        var templateArguments = templateMethod.GetGenericArguments();
        var methodClassName = templateMethod.CleanName();

        var aliasDictionary = new Dictionary<Type, string>();
        var aliases = new List<string>();


        for (var i = 0; i < templateArguments.Length; i++)
        {
            var alias = $"{methodClassName}_T{i + 1}";
            var positionalParams = methodInfos.Select(method => method.GetGenericArguments()[i]).ToList();
            aliases.Add(alias);
            foreach (var param in positionalParams)
                aliasDictionary[param] = alias;
        }

        var outerGenerics = ClassScope.AccessibleGenerics;

        var indexerTypes = aliases.Select(it => $"typing.Type[{it}]");
        var typeVarsString = indexerTypes.CommaJoin();
        var indexerArgs = templateArguments.Length == 1 ? typeVarsString : $"typing.Tuple[{typeVarsString}]";

        var genericArguments = outerGenerics.Select(it => it.ToPythonType()).Concat(aliases);

        var returnTypeStr = $"{methodClassName}[{genericArguments.CommaJoin()}]";

        if (hasGenericOverloads) sb.Indent().AppendLine("@typing.overload");
        sb.Indent().AppendLine($"def __getitem__(self, t:{indexerArgs}) -> {returnTypeStr}: ...");


        sb.AppendLine();


        using (var classScope = new ClassScope(methodClassName, aliasDictionary.Keys, false))
        {

            WriteClassHeader(classScope, sb, methodClassName, genericAliases: aliasDictionary);
            var callLines = GetMethodCallers(methodInfos, false);
            foreach (var line in callLines)
                sb.Indent().AppendLine(line);
        }

        sb.AppendLine();

    }


    private static bool WriteSimpleMethod(StringBuilder sb, MethodBase method, bool isOverload = false)
    {
        var isOperator = IsOperator(method);
        var isStatic = method.IsStatic && !isOperator;


        var methodName = method.IsConstructor ? "__init__" : method.Name;
        if (methodName == "<Clone>$") return false;
        if (isOperator)
        {
            methodName = ConvertOperatorName(method.Name);
            if (methodName == null)
            {
                var signature = method.GetParameters().Select(it => it.Name + ": " + it.ParameterType.Name).CommaJoin();
                sb.Indent().AppendLine($"# Operator not supported {method.Name}({signature})");
                return false;
            }
        }

        var returnType = method is MethodInfo mi ? mi.ReturnType.ToPythonType() : "None";

        var parameters = GetParameters(method, !isStatic);


        // ReSharper disable StringLiteralTypo - python decorator
        if (isOverload) sb.Indent().AppendLine("@typing.overload");
        if (isStatic) sb.Indent().AppendLine("@staticmethod");
        if (method.IsAbstract) sb.Indent().AppendLine("@abc.abstractmethod");
        // ReSharper enable StringLiteralTypo - python decorator

        // Try to get documentation for this method/constructor
        var doc = DocProvider?.GetDoc(method);
        if (doc is { IsEmpty: false })
        {
            sb.Indent().AppendLine($"def {methodName}({parameters}) -> {returnType}:");
            using (new IndentScope())
            {
                WriteMethodDocstring(sb, doc, method.GetParameters());
                sb.Indent().AppendLine("...");
            }
        }
        else
        {
            sb.Indent().AppendLine($"def {methodName}({parameters}) -> {returnType}: ...");
        }
        return true;
    }

    private static readonly Dictionary<string, string> OperatorNameMap = new()
    {
        ["op_Equality"] = "__eq__",
        ["op_Inequality"] = "__ne__",
        ["op_GreaterThan"] = "__gt__",
        ["op_LessThan"] = "__lt__",
        ["op_GreaterThanOrEqual"] = "__ge__",
        ["op_LessThanOrEqual"] = "__le__",
        ["op_BitwiseAnd"] = "__and__",
        ["op_BitwiseOr"] = "__or__",
        ["op_Addition"] = "__add__",
        ["op_Subtraction"] = "__sub__",
        ["op_Division"] = "__truediv__",
        ["op_Modulus"] = "__mod__",
        ["op_Multiply"] = "__mul__",
        ["op_LeftShift"] = "__lshift__",
        ["op_RightShift"] = "__rshift__",
        ["op_ExclusiveOr"] = "__xor__",
        ["op_UnaryNegation"] = "__neg__",
        ["op_UnaryPlus"] = "__pos__",
        ["op_OnesComplement"] = "__invert__",
    };

    private static string? ConvertOperatorName(string methodName)
        => OperatorNameMap.TryGetValue(methodName, out var python) ? python : null;

    private static string GetParameters(MethodBase method, bool includeSelf)
    {
        var parameters = method.GetParameters();
        var pythonParams = parameters.Select(GetParameter);
        if (includeSelf) pythonParams = pythonParams.Prepend("self");
        return pythonParams.CommaJoin();

        string GetParameter(ParameterInfo it)
        {
            var name = PythonTypes.SafePythonName(it.Name);
            var type = it.ParameterType.ToPythonType();
            var defaultValue = it.HasDefaultValue ? " = ..." : "";
            return $"{name}: {type}{defaultValue}";
        }
    }

    private static void WriteEnum(StringBuilder sb, Type stubType)
    {
        var underlyingType = stubType.GetEnumUnderlyingType().ToPythonType();
        sb.Indent().AppendLine($"class {stubType.Name}(typing.SupportsInt):");
        using var _ = new IndentScope();

        // Write enum docstring
        var enumDoc = DocProvider?.GetDoc(stubType);
        WriteDocstring(sb, enumDoc);

        sb.Indent().AppendLine("@typing.overload");
        sb.Indent().AppendLine($"def __init__(self, value : {underlyingType}) -> None: ...");
        sb.Indent().AppendLine("@typing.overload");
        sb.Indent().AppendLine($"def __init__(self, value : {underlyingType}, force_if_true: {typeof(bool).ToPythonType()}) -> None: ...");
        sb.Indent().AppendLine("def __int__(self) -> int: ...");
        sb.Indent().AppendLine();
        sb.Indent().AppendLine("# Values:");
        var names = Enum.GetNames(stubType);
        var values = Enum.GetValues(stubType);

        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            name = PythonTypes.SafePythonName(name);

            var val = Convert.ChangeType(values.GetValue(i), Type.GetTypeCode(stubType));
            sb.Indent().AppendLine($"{name} : {stubType.ToPythonType()} # {val}");
        }

    }

    #region Docstring Writing

    /// <summary>
    /// Write a class/type-level docstring. Returns true if something was written.
    /// </summary>
    private static bool WriteDocstring(StringBuilder sb, DocComment? doc)
    {
        if (doc == null || doc.IsEmpty) return false;

        var summary = doc.Summary;
        if (string.IsNullOrWhiteSpace(summary)) return false;

        // Single-line docstring for short summaries without extras
        if (doc.Remarks == null && doc.Parameters.Count == 0 && doc.Exceptions.Count == 0
            && summary.Length < 80 && !summary.Contains('\n'))
        {
            sb.Indent().AppendLine($"\"\"\"{EscapeDocstring(summary)}\"\"\"");
            return true;
        }

        // Multi-line docstring
        sb.Indent().AppendLine($"\"\"\"{EscapeDocstring(summary)}");

        if (doc.Remarks != null)
        {
            sb.AppendLine();
            sb.Indent().AppendLine($"Remarks:");
            sb.Indent().AppendLine($"    {EscapeDocstring(doc.Remarks)}");
        }

        sb.Indent().AppendLine("\"\"\"");
        return true;
    }

    /// <summary>
    /// Write a method/constructor-level docstring with parameter and return documentation.
    /// Uses reStructuredText/Sphinx format: :param name:, :returns:, :raises Type:
    /// </summary>
    private static void WriteMethodDocstring(StringBuilder sb, DocComment doc, ParameterInfo[] parameters)
    {
        var summary = doc.Summary;
        var hasSummary = !string.IsNullOrWhiteSpace(summary);

        // Single-line for simple summaries
        if (IsSimpleSingleLineDocstring(summary, hasSummary, doc, parameters))
        {
            sb.Indent().AppendLine($"\"\"\"{EscapeDocstring(summary!)}\"\"\"");
            return;
        }

        WriteMethodDocstringHeader(sb, summary, hasSummary);
        WriteParamDocs(sb, doc, parameters);
        WriteReturnsDoc(sb, doc);
        WriteExceptionDocs(sb, doc);
        WriteRemarksDoc(sb, doc);

        sb.Indent().AppendLine("\"\"\"");
    }

    private static bool IsSimpleSingleLineDocstring(
        string? summary, bool hasSummary, DocComment doc, ParameterInfo[] parameters)
        => hasSummary
           && !HasMethodDocDetails(doc, parameters)
           && summary!.Length < 72
           && !summary.Contains('\n');

    private static bool HasMethodDocDetails(DocComment doc, ParameterInfo[] parameters)
        => (doc.Parameters.Count > 0 && parameters.Length > 0)
           || !string.IsNullOrWhiteSpace(doc.Returns)
           || doc.Exceptions.Count > 0
           || !string.IsNullOrWhiteSpace(doc.Remarks);

    private static void WriteMethodDocstringHeader(StringBuilder sb, string? summary, bool hasSummary)
        => sb.Indent().AppendLine(hasSummary ? $"\"\"\"{EscapeDocstring(summary!)}" : "\"\"\"");

    private static void WriteParamDocs(StringBuilder sb, DocComment doc, ParameterInfo[] parameters)
    {
        if (doc.Parameters.Count == 0) return;

        sb.AppendLine();
        foreach (var param in parameters)
        {
            var paramName = param.Name ?? $"arg{param.Position}";
            if (doc.Parameters.TryGetValue(paramName, out var paramDoc))
                sb.Indent().AppendLine($":param {paramName}: {EscapeDocstring(paramDoc)}");
        }
    }

    private static void WriteReturnsDoc(StringBuilder sb, DocComment doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.Returns))
            sb.Indent().AppendLine($":returns: {EscapeDocstring(doc.Returns!)}");
    }

    private static void WriteExceptionDocs(StringBuilder sb, DocComment doc)
    {
        foreach (var exc in doc.Exceptions)
            sb.Indent().AppendLine($":raises {exc.TypeName}: {EscapeDocstring(exc.Description)}");
    }

    private static void WriteRemarksDoc(StringBuilder sb, DocComment doc)
    {
        if (string.IsNullOrWhiteSpace(doc.Remarks)) return;
        sb.AppendLine();
        sb.Indent().AppendLine("Remarks:");
        sb.Indent().AppendLine($"    {EscapeDocstring(doc.Remarks!)}");
    }

    /// <summary>
    /// Escape content for use inside a Python triple-quoted docstring.
    /// </summary>
    private static string EscapeDocstring(string text)
    {
        // Escape triple quotes within the docstring
        return text.Replace("\"\"\"", "\\\"\\\"\\\"");
    }

    #endregion
}