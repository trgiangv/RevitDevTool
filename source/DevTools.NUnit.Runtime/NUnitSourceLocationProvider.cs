using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime;

internal sealed class NUnitSourceLocationProvider
{
    private readonly string _assemblyPath;
    private readonly string? _probeDirectory;
    private readonly Dictionary<string, (string File, int Line)?> _cache = new(StringComparer.Ordinal);

    public NUnitSourceLocationProvider(string assemblyPath)
    {
        _assemblyPath = Path.GetFullPath(assemblyPath);
        _probeDirectory = Path.GetDirectoryName(_assemblyPath);
    }

    public bool TryGetSourceLocation(ITest test, out string? filePath, out int lineNumber)
    {
        filePath = null;
        lineNumber = 0;

        if (string.IsNullOrWhiteSpace(test.FullName))
            return false;

        if (_cache.TryGetValue(test.FullName, out var cached))
        {
            if (cached is null)
                return false;

            filePath = cached.Value.File;
            lineNumber = cached.Value.Line;
            return true;
        }

        try
        {
            if (test.Method?.MethodInfo is { } methodInfo
                && TryReadSequencePoint(methodInfo, out filePath, out lineNumber))
            {
                _cache[test.FullName] = (filePath!, lineNumber);
                return true;
            }

            if (TryGetSourceLocation(test.FullName, out filePath, out lineNumber))
                return true;

            _cache[test.FullName] = null;
            return false;
        }
        catch
        {
            _cache[test.FullName] = null;
            return false;
        }
    }

    public bool TryGetSourceLocation(string fullTestName, out string? filePath, out int lineNumber)
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
            NUnitTestNameParser.Split(fullTestName, out var className, out var methodName);
            if (!TryReadSequencePoint(NUnitTestNameParser.ToMetadataTypeName(className), methodName, out filePath, out lineNumber))
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

    private bool TryReadSequencePoint(MethodInfo methodInfo, out string? filePath, out int lineNumber)
    {
        filePath = null;
        lineNumber = 0;

        var pdbPath = Path.ChangeExtension(_assemblyPath, ".pdb");
        if (!File.Exists(_assemblyPath) || !File.Exists(pdbPath))
            return false;

        using var peStream = File.OpenRead(_assemblyPath);
        using var pdbStream = File.OpenRead(pdbPath);
        using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);

        var methodHandle = (MethodDefinitionHandle)MetadataTokens.Handle(methodInfo.MetadataToken);
        var pdbMetadata = pdbProvider.GetMetadataReader();
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

internal static class NUnitTestNameParser
{
    public static void Split(string fullTestName, out string className, out string methodName)
    {
        var openParen = fullTestName.IndexOf('(');
        var dottedName = openParen >= 0 ? fullTestName[..openParen] : fullTestName;
        var lastDot = dottedName.LastIndexOf('.');
        if (lastDot < 0)
        {
            className = dottedName;
            methodName = dottedName;
            return;
        }

        className = dottedName[..lastDot];
        methodName = dottedName[(lastDot + 1)..];
    }

    public static string ToMetadataTypeName(string displayTypeName)
    {
        var lastDot = displayTypeName.LastIndexOf('.');
        if (lastDot < 0)
            return NormalizeGenericSegment(displayTypeName);

        return displayTypeName[..(lastDot + 1)] + NormalizeGenericSegment(displayTypeName[(lastDot + 1)..]);
    }

    private static string NormalizeGenericSegment(string segment)
    {
        var genericStart = segment.IndexOf('<');
        if (genericStart < 0)
            return segment;

        var baseName = segment[..genericStart];
        var depth = 0;
        var typeArgumentCount = 0;
        for (var index = genericStart; index < segment.Length; index++)
        {
            switch (segment[index])
            {
                case '<':
                    depth++;
                    if (depth == 1)
                        typeArgumentCount++;
                    break;
                case ',' when depth == 1:
                    typeArgumentCount++;
                    break;
                case '>':
                    depth--;
                    break;
            }
        }

        return typeArgumentCount == 0 ? baseName : baseName + "`" + typeArgumentCount;
    }
}
