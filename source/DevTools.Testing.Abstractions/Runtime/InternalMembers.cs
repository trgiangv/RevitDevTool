using System.Collections.Concurrent;
using System.Reflection;

namespace DevTools.Testing.Abstractions.Runtime;

/// <summary>
/// The one place that reaches into a pinned framework's non-public members (MTP, TUnit
/// engine, NUnit). Every lookup is cached, so a run pays for reflection once per
/// process, and every miss names the member and the assembly version, so a framework
/// bump fails with one actionable message instead of a null reference deep in a run.
/// </summary>
public static class InternalMembers
{
    private const BindingFlags Instance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const BindingFlags Static =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly ConcurrentDictionary<(Type Owner, string Key), MemberInfo?> Cache = new();

    /// <summary>Finds a type by full name in <paramref name="assembly"/>.</summary>
    public static Type Type(Assembly assembly, string fullName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        return assembly.GetType(fullName, throwOnError: false)
            ?? throw Missing(fullName, assembly);
    }

    public static FieldInfo Field(Type owner, string name, bool isStatic = false) =>
        TryField(owner, name, isStatic) ?? throw Missing(owner, name);

    public static FieldInfo? TryField(Type owner, string name, bool isStatic = false)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return (FieldInfo?)Cache.GetOrAdd(
            (owner, "F:" + (isStatic ? "s:" : "i:") + name),
            _ => owner.GetField(name, isStatic ? Static : Instance));
    }

    public static PropertyInfo Property(Type owner, string name) =>
        TryProperty(owner, name) ?? throw Missing(owner, name);

    public static PropertyInfo? TryProperty(Type owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return (PropertyInfo?)Cache.GetOrAdd((owner, "P:" + name), _ => owner.GetProperty(name, Instance));
    }

    /// <summary>Finds an instance method by name and exact parameter types (none means parameterless).</summary>
    public static MethodInfo Method(Type owner, string name, params Type[] parameterTypes)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var key = "M:" + name + "(" + string.Join(",", parameterTypes.Select(type => type.FullName)) + ")";
        return (MethodInfo?)Cache.GetOrAdd(
                (owner, key),
                _ => owner.GetMethod(name, Instance, binder: null, parameterTypes, modifiers: null))
            ?? throw Missing(owner, name);
    }

    /// <summary>Finds an instance constructor, public or not, by exact parameter types.</summary>
    public static ConstructorInfo Constructor(Type owner, params Type[] parameterTypes)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var key = "C:(" + string.Join(",", parameterTypes.Select(type => type.FullName)) + ")";
        return (ConstructorInfo?)Cache.GetOrAdd(
                (owner, key),
                _ => owner.GetConstructor(Instance, binder: null, parameterTypes, modifiers: null))
            ?? throw Missing(owner, ".ctor");
    }

    private static MissingMemberException Missing(Type owner, string member) =>
        new(
            $"'{owner.FullName}.{member}' was not found in {Describe(owner.Assembly)}. "
            + "The framework version changed; update the matching runtime seam.");

    private static MissingMemberException Missing(string typeName, Assembly assembly) =>
        new(
            $"Type '{typeName}' was not found in {Describe(assembly)}. "
            + "The framework version changed; update the matching runtime seam.");

    private static string Describe(Assembly assembly)
    {
        var name = assembly.GetName();
        return name.Name + " " + name.Version;
    }
}
