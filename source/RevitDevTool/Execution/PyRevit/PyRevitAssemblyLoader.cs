using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Loads one managed DLL per simple name from the extension hierarchy,
/// chosen by <c>TargetFrameworkAttribute</c> against the host runtime.
/// Once per <c>*.extension</c>; scripts outside an extension are skipped.
/// </summary>
internal static class PyRevitAssemblyLoader
{
    private static readonly Lock LoadLock = new();
    private static readonly HashSet<string> LoadedRoots = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IReadOnlyList<string>> SelectedByRoot = new(StringComparer.OrdinalIgnoreCase);

    internal static void EnsureLoaded(string scriptPath, ILogger? logger = null)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath);
        var extensionRoot = PyRevitExtensionPaths.FindExtensionRoot(scriptDir);
        if (extensionRoot is null)
            return;

        lock (LoadLock)
        {
            if (!LoadedRoots.Add(extensionRoot))
                return;

            LoadAll(SelectAssemblies(scriptDir), logger);
        }
    }

    /// <summary>
    /// Directories of the TFM-selected DLLs, added to IronPython search paths
    /// so <c>clr.AddReference("Name")</c> can find <c>Name.dll</c>.
    /// </summary>
    internal static IReadOnlyList<string> SelectedAssemblyDirectories(string? scriptDirectory)
    {
        var directories = new List<string>();
        foreach (var dllPath in SelectAssemblies(scriptDirectory))
        {
            var directory = Path.GetDirectoryName(dllPath);
            if (string.IsNullOrEmpty(directory))
                continue;
            if (directories.Any(p => string.Equals(p, directory, StringComparison.OrdinalIgnoreCase)))
                continue;
            directories.Add(directory);
        }

        return directories;
    }

    internal static IReadOnlyList<string> SelectAssemblies(string? scriptDirectory)
    {
        var extensionRoot = PyRevitExtensionPaths.FindExtensionRoot(scriptDirectory);
        if (extensionRoot is not null)
        {
            lock (LoadLock)
            {
                if (SelectedByRoot.TryGetValue(extensionRoot, out var cached))
                    return cached;
            }
        }

        var candidates = PyRevitExtensionPaths.EnumerateDllCandidates(scriptDirectory).ToList();
        var selected = candidates.Count == 0
            ? (IReadOnlyList<string>)[]
            : SelectByTfm(candidates);

        if (extensionRoot is null)
            return selected;

        lock (LoadLock)
            SelectedByRoot[extensionRoot] = selected;

        return selected;
    }

    private static List<string> SelectByTfm(List<string> dllPaths)
    {
        var runtimeMajor = Environment.Version.Major;
        var result = new List<string>();
        foreach (var group in dllPaths.GroupBy(static p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase))
        {
            if (HighestCompatible(group, runtimeMajor) is { } path)
                result.Add(path);
        }

        return result;
    }

    private static string? HighestCompatible(IEnumerable<string> dllPaths, int runtimeMajor)
    {
        string? chosen = null;
        var bestMajor = int.MinValue;
        foreach (var dllPath in dllPaths)
        {
            if (!TryReadTfmMajor(dllPath, out var major))
                continue;
            if (major > runtimeMajor)
                continue;
            if (major <= bestMajor)
                continue;

            bestMajor = major;
            chosen = dllPath;
        }

        return chosen;
    }

    private static void LoadAll(IReadOnlyList<string> dllPaths, ILogger? logger)
    {
        foreach (var dllPath in dllPaths)
        {
            var simpleName = Path.GetFileNameWithoutExtension(dllPath);
            if (IsLoaded(simpleName))
                continue;

            try
            {
                Assembly.LoadFrom(dllPath);
                logger?.ZLogInformation($"[PyRevit] Loaded extension DLL: {Path.GetFileName(dllPath)}");
            }
            catch (Exception ex)
            {
                logger?.ZLogWarning($"[PyRevit] Failed to load '{Path.GetFileName(dllPath)}': {ex.Message}");
            }
        }
    }

    private static bool IsLoaded(string simpleName) =>
        AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads TFM major from PE <c>TargetFrameworkAttribute</c>.
    /// Managed assemblies without the attribute (old-style) are treated as net4.
    /// Native / invalid files return false.
    /// </summary>
    private static bool TryReadTfmMajor(string dllPath, out int major)
    {
        major = 0;
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return false;

            var reader = peReader.GetMetadataReader();
            var assemblyDef = reader.GetAssemblyDefinition();

            foreach (var attrHandle in assemblyDef.GetCustomAttributes())
            {
                var attr = reader.GetCustomAttribute(attrHandle);
                if (attr.Constructor.Kind != HandleKind.MemberReference) continue;

                var ctor = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                if (ctor.Parent.Kind != HandleKind.TypeReference) continue;

                var typeRef = reader.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                if (reader.GetString(typeRef.Name) != nameof(TargetFrameworkAttribute)) continue;

                var blob = reader.GetBlobReader(attr.Value);
                blob.ReadUInt16();
                major = ParseTfmMajor(blob.ReadSerializedString());
                return true;
            }

            major = 4;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int ParseTfmMajor(string? tfmString)
    {
        if (tfmString is not { Length: > 0 }) return 4;

        var vIndex = tfmString.IndexOf("=v", StringComparison.OrdinalIgnoreCase);
        if (vIndex < 0) return 4;

        var versionSpan = tfmString.AsSpan(vIndex + 2);
        var dotIndex = versionSpan.IndexOf('.');
        var majorSpan = dotIndex > 0 ? versionSpan[..dotIndex] : versionSpan;

        return int.TryParse(majorSpan.ToString(), out var parsed) ? parsed : 4;
    }
}
