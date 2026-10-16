using System.Collections;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using HarmonyLib;
// ReSharper disable InconsistentNaming

namespace DevTools.Presentation.Services;

/// <summary>
/// ILPack throws while rebuilding metadata for assemblies it was not written against.
/// These patches keep the original identity and skip only the rows the metadata tables cannot store.
/// </summary>
internal static class AssemblyPatcher
{
    private static readonly Lock Gate = new();
    private static bool _applied;
    private static FieldInfo _reverseMap = null!;
    private static FieldInfo _handles = null!;
    private static FieldInfo _metadata = null!;
    private static FieldInfo _methodTokens = null!;
    private static FieldInfo _fieldTokens = null!;
    private static FieldInfo _ctorDefs = null!;
    private static MethodInfo _addReference = null!;
    private static MethodInfo _getTypeHandle = null!;
    private static MethodInfo _getMethodHandle = null!;
    private static MethodInfo _getFieldHandle = null!;
    private static MethodInfo _getCtorHandle = null!;
    private static MethodInfo _addInterface = null!;
    private static MethodInfo _addMethodImpl = null!;
    private static PropertyInfo _builder = null!;

    public static void Apply()
    {
        lock (Gate)
        {
            if (_applied)
                return;

            Bind();
            _applied = true;
        }
    }

    private static void Bind()
    {
        var metadata = typeof(Lokad.ILPack.AssemblyGenerator).Assembly.GetType("Lokad.ILPack.Metadata.AssemblyMetadata", true)!;
        _reverseMap = Field(metadata, "_reverseForwardingMap");
        _handles = Field(metadata, "_assemblyRefHandles");
        _methodTokens = Field(metadata, "_unconstructedMethodDefs");
        _fieldTokens = Field(metadata, "_unconstructedFieldDefs");
        _ctorDefs = Field(metadata, "_ctorDefHandles");
        _builder = metadata.GetProperty("Builder") ?? throw new MissingMemberException(metadata.FullName, "Builder");
        _addReference = Method(metadata, "AddReferencedAssembly", 2);
        _getTypeHandle = Method(metadata, "GetTypeHandle", 2);
        _getMethodHandle = Method(metadata, "GetMethodHandle", 2);
        _getFieldHandle = Method(metadata, "GetFieldHandle", 2);
        _getCtorHandle = Method(metadata, "GetConstructorHandle", 2);
        _metadata = Field(typeof(Lokad.ILPack.AssemblyGenerator), "_metadata");
        _addInterface = Method(typeof(MetadataBuilder), "AddInterfaceImplementation", 2);
        _addMethodImpl = Method(typeof(MetadataBuilder), "AddMethodImplementation", 3);

        var extensions = typeof(Lokad.ILPack.AssemblyGenerator).Assembly.GetType("Lokad.ILPack.Extensions", true)!;
        var harmony = new Harmony("DevTools.Presentation.AssemblyReference");
        harmony.Patch(StaticMethod(extensions, "FromSystemType", typeof(SignatureTypeEncoder)), prefix: Hook(nameof(ByRefPrefix)));
        harmony.Patch(Method(metadata, "GetReferencedAssemblyForType", 1), prefix: Hook(nameof(ReferencePrefix)));
        harmony.Patch(Method(typeof(Lokad.ILPack.AssemblyGenerator), "DeclareInterfacesAndCreateInterfaceMap", 2), prefix: Hook(nameof(InterfacePrefix)));
        harmony.Patch(_getMethodHandle, finalizer: Hook(nameof(FinalizeMethod)));
        harmony.Patch(_getFieldHandle, finalizer: Hook(nameof(FinalizeField)));
        harmony.Patch(_getCtorHandle, finalizer: Hook(nameof(FinalizeCtor)));
    }

    private static HarmonyMethod Hook(string name) => new(typeof(AssemblyPatcher), name);

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(type.FullName, name);

    private static MethodInfo StaticMethod(Type type, string name, Type firstParameter)
    {
        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var parameters = method.GetParameters();
            if (method.Name == name && parameters.Length > 0 && parameters[0].ParameterType == firstParameter)
                return method;
        }

        throw new MissingMethodException(type.FullName, name);
    }

    private static void ByRefPrefix(SignatureTypeEncoder typeEncoder, ref Type type)
    {
        while (type.IsByRef)
        {
            var element = type.GetElementType();
            if (element == null)
                return;

            typeEncoder.Builder.WriteByte((byte)SignatureTypeCode.ByReference);
            type = element;
        }
    }

    private static MethodInfo Method(Type type, string name, int parameters)
    {
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.Name == name && method.GetParameters().Length == parameters)
                return method;
        }

        throw new MissingMethodException(type.FullName, name);
    }

    private static bool ReferencePrefix(object __instance, Type type, ref AssemblyReferenceHandle __result)
    {
        if (!__result.IsNil)
            return true;

        __result = ResolveReference(__instance, type);
        return false;
    }

    private static AssemblyReferenceHandle ResolveReference(object metadata, Type type)
    {
        while (type.DeclaringType != null)
            type = type.DeclaringType;

        var map = Map<AssemblyName>(metadata, _reverseMap);
        if (!map.TryGetValue($"{type.Namespace}.{type.Name}", out var name))
            name = type.Assembly.GetName();

        var handles = Map<AssemblyReferenceHandle>(metadata, _handles);
        var key = name.ToString();
        if (handles.TryGetValue(key, out var existing))
            return existing;

        _addReference.Invoke(metadata, [name.Name, name]);
        return handles[key];
    }

    private static bool InterfacePrefix(object __instance, Type type, TypeDefinitionHandle handle)
    {
        WriteInterfaces(Metadata(__instance), type, handle);
        return false;
    }

    private static void WriteInterfaces(object metadata, Type type, TypeDefinitionHandle handle)
    {
        var declared = DeclaredInterfaces(metadata, type);
        foreach (var implemented in declared)
            AddInterface(metadata, handle, TypeHandle(metadata, implemented));

        if (type.IsInterface)
            return;

        foreach (var mapping in type.GetInterfaces())
            WriteMapping(metadata, type, handle, declared, type.GetInterfaceMap(mapping));
    }

    private static HashSet<Type> DeclaredInterfaces(object metadata, Type type)
    {
        var interfaces = type.BaseType == null
            ? type.GetInterfaces()
            : type.GetInterfaces().Except(type.BaseType.GetInterfaces());
        return new HashSet<Type>(interfaces.OrderBy(implemented => CodedIndex.TypeDefOrRefOrSpec(TypeHandle(metadata, implemented))));
    }

    private static void WriteMapping(
        object metadata,
        Type type,
        TypeDefinitionHandle handle,
        HashSet<Type> declared,
        InterfaceMapping mapping)
    {
        var implementedByType = declared.Contains(mapping.InterfaceType);
        for (var i = 0; i < mapping.InterfaceMethods.Length; i++)
            WriteOverride(metadata, type, handle, implementedByType, mapping.TargetMethods[i], mapping.InterfaceMethods[i]);
    }

    private static void WriteOverride(
        object metadata,
        Type type,
        TypeDefinitionHandle handle,
        bool implementedByType,
        MethodInfo? target,
        MethodInfo? iface)
    {
        if (target?.DeclaringType == null || iface == null || (!implementedByType && target.DeclaringType != type))
            return;

        var body = Member(metadata, _getMethodHandle, target);
        var declaration = Member(metadata, _getMethodHandle, iface);
        if (body == declaration)
            return;

        // Class must be the type being written. MethodImpl rows are emitted in TypeDef order,
        // so attaching one to an earlier base type makes the table unsorted.
        _addMethodImpl.Invoke(Builder(metadata), [handle, body, declaration]);
    }

    private static Exception? FinalizeMethod(
        object __instance,
        MethodInfo method,
        bool inMethodBodyWritingContext,
        ref EntityHandle __result,
        Exception? __exception) =>
        Recover(__exception, "Method cannot be found", __instance, FindToken(_methodTokens, __instance, method), inMethodBodyWritingContext, _getMethodHandle, ref __result);

    private static Exception? FinalizeField(
        object __instance,
        FieldInfo field,
        bool inMethodBodyWritingContext,
        ref EntityHandle __result,
        Exception? __exception) =>
        Recover(__exception, "Field cannot be found", __instance, FindToken(_fieldTokens, __instance, field), inMethodBodyWritingContext, _getFieldHandle, ref __result);

    private static Exception? FinalizeCtor(
        object __instance,
        ConstructorInfo ctor,
        bool inMethodBodyWritingContext,
        ref EntityHandle __result,
        Exception? __exception) =>
        Recover(__exception, "Constructor cannot be found", __instance, FindCtor(__instance, ctor), inMethodBodyWritingContext, _getCtorHandle, ref __result);

    private static Exception? Recover(
        Exception? error,
        string marker,
        object metadata,
        MemberInfo? reserved,
        bool inBody,
        MethodInfo lookup,
        ref EntityHandle result)
    {
        if (error is not ArgumentException || reserved == null || error.Message.StartsWith(marker, StringComparison.Ordinal) == false)
            return error;

        result = Member(metadata, lookup, reserved, inBody);
        return null;
    }

    private static MemberInfo? FindToken(FieldInfo tokens, object metadata, MemberInfo member)
    {
        if (tokens.GetValue(metadata) is not IDictionary map)
            return null;
        if (map.Contains(member.MetadataToken))
        {
            var reserved = map[member.MetadataToken] as MemberInfo;
            if (reserved != null && !ReferenceEquals(reserved, member))
                return reserved;
        }

        foreach (var value in map.Values)
        {
            if (value is MemberInfo candidate && !ReferenceEquals(candidate, member) && SameMember(candidate, member))
                return candidate;
        }

        return null;
    }

    private static ConstructorInfo? FindCtor(object metadata, ConstructorInfo ctor)
    {
        if (_ctorDefs.GetValue(metadata) is not IDictionary map)
            return null;

        foreach (var key in map.Keys)
        {
            if (key is ConstructorInfo candidate && !ReferenceEquals(candidate, ctor) && candidate.MetadataToken == ctor.MetadataToken)
                return candidate;
        }

        return null;
    }

    private static bool SameMember(MemberInfo candidate, MemberInfo member)
    {
        if (candidate.Name != member.Name || candidate.DeclaringType?.MetadataToken != member.DeclaringType?.MetadataToken)
            return false;
        if (candidate is MethodBase left && member is MethodBase right)
            return SameParameters(left.GetParameters(), right.GetParameters());

        return candidate is FieldInfo leftField && member is FieldInfo rightField && leftField.FieldType == rightField.FieldType;
    }

    private static bool SameParameters(ParameterInfo[] left, ParameterInfo[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (left[i].ParameterType != right[i].ParameterType)
                return false;
        }

        return true;
    }

    private static void AddInterface(object metadata, TypeDefinitionHandle type, EntityHandle implemented) =>
        _addInterface.Invoke(Builder(metadata), [type, implemented]);

    private static EntityHandle Member(object metadata, MethodInfo lookup, MemberInfo member, bool inBody = false) =>
        (EntityHandle)lookup.Invoke(metadata, [member, inBody])!;

    private static EntityHandle TypeHandle(object metadata, Type type) =>
        (EntityHandle)_getTypeHandle.Invoke(metadata, [type, false])!;

    private static object Metadata(object generator) => _metadata.GetValue(generator)!;

    private static object Builder(object metadata) => _builder.GetValue(metadata)!;

    private static Dictionary<string, TValue> Map<TValue>(object metadata, FieldInfo field) =>
        field.GetValue(metadata) as Dictionary<string, TValue>
        ?? throw new InvalidOperationException($"ILPack field {field.Name} is not the expected map.");
}
