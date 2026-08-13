using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DevTools.NUnit.TestAdapter;

/// <summary>
/// Resolves test method source locations from portable PDBs by reading PE + PDB metadata
/// without loading the test assembly (safe for Revit-referencing test DLLs).
/// </summary>
internal sealed class PortablePdbNavigationProvider
{
    private readonly string _assemblyPath;
    private readonly string? _probeDirectory;
    private readonly Dictionary<string, (string File, int Line)?> _cache = new(StringComparer.Ordinal);

    public PortablePdbNavigationProvider(string assemblyPath)
    {
        _assemblyPath = Path.GetFullPath(assemblyPath);
        _probeDirectory = Path.GetDirectoryName(_assemblyPath);
    }

    public bool TryGetNavigationData(string fullTestName, out string? filePath, out int lineNumber)
    {
        filePath = null;
        lineNumber = 0;

        if (string.IsNullOrWhiteSpace(fullTestName))
            return false;

        if (_cache.TryGetValue(fullTestName, out var cached))
        {
            if (cached is null)
                return false;

            filePath = cached.Value.File;
            lineNumber = cached.Value.Line;
            return true;
        }

        try
        {
            TestNameParser.Split(fullTestName, out var className, out var methodName);
            if (!TryReadSequencePoint(className, methodName, out filePath, out lineNumber))
            {
                _cache[fullTestName] = null;
                return false;
            }

            _cache[fullTestName] = (filePath!, lineNumber);
            return true;
        }
        catch
        {
            _cache[fullTestName] = null;
            return false;
        }
    }

    private bool TryReadSequencePoint(string className, string methodName, out string? filePath, out int lineNumber)
    {
        filePath = null;
        lineNumber = 0;

        var pdbPath = Path.ChangeExtension(_assemblyPath, ".pdb");
        if (!File.Exists(_assemblyPath) || !File.Exists(pdbPath))
            return false;

        using var peStream = File.OpenRead(_assemblyPath);
        using var pdbStream = File.OpenRead(pdbPath);
        using var peReader = new PEReader(peStream);
        using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);

        var peMetadata = peReader.GetMetadataReader();
        var pdbMetadata = pdbProvider.GetMetadataReader();

        if (!TryFindMethodHandle(peMetadata, className, methodName, out var methodHandle))
            return false;

        var debugInfo = pdbMetadata.GetMethodDebugInformation(methodHandle);
        foreach (var point in debugInfo.GetSequencePoints())
        {
            if (point.StartLine == SequencePoint.HiddenLine)
                continue;

            var document = pdbMetadata.GetDocument(point.Document);
            filePath = NormalizeSourcePath(pdbMetadata.GetString(document.Name));
            lineNumber = point.StartLine;
            return !string.IsNullOrWhiteSpace(filePath);
        }

        return false;
    }

    private static bool TryFindMethodHandle(
        MetadataReader reader,
        string className,
        string methodName,
        out MethodDefinitionHandle methodHandle)
    {
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var typeDefinition = reader.GetTypeDefinition(typeHandle);
            if (!string.Equals(GetTypeFullName(reader, typeDefinition), className, StringComparison.Ordinal))
                continue;

            foreach (var candidate in typeDefinition.GetMethods())
            {
                var methodDefinition = reader.GetMethodDefinition(candidate);
                if (string.Equals(reader.GetString(methodDefinition.Name), methodName, StringComparison.Ordinal))
                {
                    methodHandle = candidate;
                    return true;
                }
            }
        }

        methodHandle = default;
        return false;
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

    private string? NormalizeSourcePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(path))
            return path;

        if (_probeDirectory is null)
            return path;

        var candidate = Path.GetFullPath(Path.Combine(_probeDirectory, path));
        return File.Exists(candidate) ? candidate : path;
    }
}

internal static class PortablePdbNavigationCache
{
    private static readonly ConcurrentDictionary<string, PortablePdbNavigationProvider> Providers = new(StringComparer.OrdinalIgnoreCase);

    public static PortablePdbNavigationProvider GetOrAdd(string assemblyPath) =>
        Providers.GetOrAdd(assemblyPath, static path => new PortablePdbNavigationProvider(path));
}
