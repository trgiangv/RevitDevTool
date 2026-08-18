using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.TestAdapter;

/// <summary>
/// Host-free PE metadata discovery. Callers supply the test-attribute type
/// names; this type does not know a test framework or MTP.
/// </summary>
internal static class MetadataTestDiscoverer
{
    public static IReadOnlyList<TestingDiscoveredTest> Discover(
        string assemblyPath,
        IReadOnlyList<string> attributeTypeNames)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(assemblyPath) || attributeTypeNames.Count == 0)
            return [];

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        return !peReader.HasMetadata
            ? []
            : DiscoverFromMetadata(peReader.GetMetadataReader(), attributeTypeNames);
    }

    public static IReadOnlyList<TestingDiscoveredTest> Filter(
        IReadOnlyList<TestingDiscoveredTest> tests,
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? testIds)
    {
        if ((names is null || names.Count == 0) && (testIds is null || testIds.Count == 0))
            return tests;

        var nameSet = new HashSet<string>(names ?? [], StringComparer.Ordinal);
        var idSet = new HashSet<string>(testIds ?? [], StringComparer.Ordinal);
        return tests
            .Where(test =>
                idSet.Contains(test.TestId)
                || (test.FullName is { Length: > 0 } fullName && idSet.Contains(fullName))
                || nameSet.Contains(test.DisplayName))
            .ToList();
    }

    private static List<TestingDiscoveredTest> DiscoverFromMetadata(
        MetadataReader metadata,
        IReadOnlyList<string> attributeTypeNames)
    {
        var tests = new List<TestingDiscoveredTest>();
        foreach (var typeHandle in metadata.TypeDefinitions)
            AddTypeTests(metadata, typeHandle, attributeTypeNames, tests);

        return tests;
    }

    private static void AddTypeTests(
        MetadataReader metadata,
        TypeDefinitionHandle typeHandle,
        IReadOnlyList<string> attributeTypeNames,
        List<TestingDiscoveredTest> tests)
    {
        var typeDefinition = metadata.GetTypeDefinition(typeHandle);
        if (!TryGetDiscoverableTypeName(metadata, typeDefinition, out var typeFullName))
            return;

        foreach (var methodHandle in typeDefinition.GetMethods())
        {
            var methodDefinition = metadata.GetMethodDefinition(methodHandle);
            if (!IsDiscoverableTestMethod(metadata, methodDefinition, attributeTypeNames))
                continue;

            var methodName = metadata.GetString(methodDefinition.Name);
            var fullName = typeFullName + "." + methodName;
            tests.Add(new TestingDiscoveredTest(fullName, methodName, fullName));
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

    private static bool IsDiscoverableTestMethod(
        MetadataReader reader,
        MethodDefinition methodDefinition,
        IReadOnlyList<string> attributeTypeNames)
    {
        if ((methodDefinition.Attributes & MethodAttributes.SpecialName) != 0)
            return false;

        if ((methodDefinition.Attributes & MethodAttributes.Abstract) != 0)
            return false;

        var methodName = reader.GetString(methodDefinition.Name);
        if (methodName.Length == 0 || methodName[0] == '<')
            return false;

        return HasTestAttribute(reader, methodDefinition, attributeTypeNames);
    }

    private static bool HasTestAttribute(
        MetadataReader reader,
        MethodDefinition methodDefinition,
        IReadOnlyList<string> attributeTypeNames)
    {
        foreach (var attributeHandle in methodDefinition.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var attributeTypeName = GetAttributeTypeName(reader, attribute.Constructor);
            if (attributeTypeName is null)
                continue;

            if (IsTestAttributeName(attributeTypeName, attributeTypeNames))
                return true;
        }

        return false;
    }

    private static bool IsTestAttributeName(string attributeTypeName, IReadOnlyList<string> attributeTypeNames)
    {
        foreach (var name in attributeTypeNames)
        {
            if (attributeTypeName.EndsWith(name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

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
