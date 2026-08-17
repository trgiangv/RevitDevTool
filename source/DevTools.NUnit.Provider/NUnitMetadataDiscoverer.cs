using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using DevTools.NUnit.Transport.Contracts;

namespace DevTools.NUnit.Provider;

/// <summary>
/// Discovers NUnit tests from PE metadata without loading the assembly or
/// contacting a host process. Used by VSTest, MTP Test Explorer refresh, and
/// <c>Runner discover</c>.
/// </summary>
public static class NUnitMetadataDiscoverer
{
    public static IReadOnlyList<NUnitDiscoveredTest> Discover(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(assemblyPath))
            return [];

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        return !peReader.HasMetadata ? [] : DiscoverFromMetadata(peReader.GetMetadataReader());
    }

    public static IReadOnlyList<NUnitDiscoveredTest> Filter(
        IReadOnlyList<NUnitDiscoveredTest> tests,
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? fullNames)
    {
        if ((names is null || names.Count == 0) && (fullNames is null || fullNames.Count == 0))
            return tests;

        var nameSet = new HashSet<string>(names ?? [], StringComparer.Ordinal);
        var fullNameSet = new HashSet<string>(fullNames ?? [], StringComparer.Ordinal);
        return tests
            .Where(test => fullNameSet.Contains(test.FullName) || nameSet.Contains(test.Name))
            .ToList();
    }

    private static List<NUnitDiscoveredTest> DiscoverFromMetadata(MetadataReader metadata)
    {
        var tests = new List<NUnitDiscoveredTest>();
        foreach (var typeHandle in metadata.TypeDefinitions)
            AddTypeTests(metadata, typeHandle, tests);

        return tests;
    }

    private static void AddTypeTests(
        MetadataReader metadata,
        TypeDefinitionHandle typeHandle,
        List<NUnitDiscoveredTest> tests)
    {
        var typeDefinition = metadata.GetTypeDefinition(typeHandle);
        if (!TryGetDiscoverableTypeName(metadata, typeDefinition, out var typeFullName))
            return;

        foreach (var methodHandle in typeDefinition.GetMethods())
        {
            var methodDefinition = metadata.GetMethodDefinition(methodHandle);
            if (!IsDiscoverableTestMethod(metadata, methodDefinition))
                continue;

            var methodName = metadata.GetString(methodDefinition.Name);
            var fullName = typeFullName + "." + methodName;
            tests.Add(new NUnitDiscoveredTest(
                Id: "local:" + fullName,
                Name: methodName,
                FullName: fullName));
        }
    }

    private static bool TryGetDiscoverableTypeName(
        MetadataReader metadata,
        TypeDefinition typeDefinition,
        out string typeFullName)
    {
        typeFullName = string.Empty;
        if (typeDefinition.GetDeclaringType() != default)
            return false;

        typeFullName = GetTypeFullName(metadata, typeDefinition);
        return !string.IsNullOrWhiteSpace(typeFullName) && typeFullName[0] != '<';
    }

    private static bool IsDiscoverableTestMethod(MetadataReader reader, MethodDefinition methodDefinition)
    {
        if ((methodDefinition.Attributes & MethodAttributes.SpecialName) != 0)
            return false;

        if ((methodDefinition.Attributes & MethodAttributes.Abstract) != 0)
            return false;

        var methodName = reader.GetString(methodDefinition.Name);
        if (methodName.Length == 0 || methodName[0] == '<')
            return false;

        return HasNUnitTestAttribute(reader, methodDefinition);
    }

    private static bool HasNUnitTestAttribute(MetadataReader reader, MethodDefinition methodDefinition)
    {
        foreach (var attributeHandle in methodDefinition.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var attributeTypeName = GetAttributeTypeName(reader, attribute.Constructor);
            if (attributeTypeName is null)
                continue;

            if (IsNUnitTestAttributeName(attributeTypeName))
                return true;
        }

        return false;
    }

    private static bool IsNUnitTestAttributeName(string attributeTypeName) =>
        attributeTypeName.EndsWith("TestAttribute", StringComparison.Ordinal)
        || attributeTypeName.EndsWith("TestCaseAttribute", StringComparison.Ordinal)
        || attributeTypeName.EndsWith("TestCaseSourceAttribute", StringComparison.Ordinal)
        || attributeTypeName.EndsWith("TheoryAttribute", StringComparison.Ordinal);

    private static string? GetAttributeTypeName(MetadataReader reader, EntityHandle constructorHandle) =>
        constructorHandle.Kind switch
        {
            HandleKind.MemberReference => GetTypeName(reader, reader.GetMemberReference((MemberReferenceHandle)constructorHandle).Parent),
            HandleKind.MethodDefinition => GetTypeName(reader, reader.GetMethodDefinition((MethodDefinitionHandle)constructorHandle).GetDeclaringType()),
            _ => null,
        };

    private static string? GetTypeName(MetadataReader reader, EntityHandle handle) =>
        handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeFullName(reader, reader.GetTypeDefinition((TypeDefinitionHandle)handle)),
            HandleKind.TypeReference => GetTypeReferenceFullName(reader, reader.GetTypeReference((TypeReferenceHandle)handle)),
            _ => null,
        };

    private static string GetTypeReferenceFullName(MetadataReader reader, TypeReference typeReference)
    {
        var name = reader.GetString(typeReference.Name);
        if (typeReference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var declaringType = reader.GetTypeReference((TypeReferenceHandle)typeReference.ResolutionScope);
            return GetTypeReferenceFullName(reader, declaringType) + "+" + name;
        }

        var namespaceName = reader.GetString(typeReference.Namespace);
        return string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "." + name;
    }

    private static string GetTypeFullName(MetadataReader reader, TypeDefinition typeDefinition)
    {
        var name = reader.GetString(typeDefinition.Name);
        if (typeDefinition.IsNested)
        {
            var declaringType = reader.GetTypeDefinition(typeDefinition.GetDeclaringType());
            return GetTypeFullName(reader, declaringType) + "+" + name;
        }

        var namespaceName = reader.GetString(typeDefinition.Namespace);
        return string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "." + name;
    }
}
