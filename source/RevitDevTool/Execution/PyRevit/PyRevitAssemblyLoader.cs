using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using ZLogger;

namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Loads extension DLLs from pyRevit hierarchy lib/bin folders into the current AppDomain
/// so IronPython <c>clr.AddReference</c> can resolve them. Load is once per session, no file lock.
/// </summary>
internal static class PyRevitAssemblyLoader
{
    private static readonly object LoadLock = new();
    private static readonly HashSet<string> LoadedNames = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    internal static void EnsureLoaded(string scriptPath, ILogger? logger = null)
    {
        if (_initialized) return;

        lock (LoadLock)
        {
            if (_initialized) return;

            var scriptDir = Path.GetDirectoryName(scriptPath);
            var candidates = PyRevitExtensionPaths.EnumerateDllCandidates(scriptDir).ToList();
            if (candidates.Count == 0)
            {
                _initialized = true;
                return;
            }

            GetLoadedAssemblies();
            var resolved = Resolve(candidates);
            LoadAll(resolved, logger);
            _initialized = true;
        }
    }

    private static void GetLoadedAssemblies()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (name is not null)
                LoadedNames.Add(name);
        }
    }

    private static List<string> Resolve(List<PyRevitExtensionPaths.DllCandidate> candidates)
    {
        var revitYear = RevitContext.Application.VersionNumber;

        var grouped = candidates
            .GroupBy(c => c.SimpleName, StringComparer.OrdinalIgnoreCase);

        var result = new List<string>();

        foreach (var group in grouped)
        {
            var items = group.ToList();

            if (items.Count == 1)
            {
                if (IsValidAssembly(items[0].FilePath))
                    result.Add(items[0].FilePath);
                continue;
            }

            result.Add(ResolveConflict(items, revitYear));
        }

        return result;
    }

    private static string ResolveConflict(
        List<PyRevitExtensionPaths.DllCandidate> items, string revitYear)
    {
        var yearMatches = items
            .Where(c => PathContainsSegment(c.FilePath, revitYear))
            .ToList();

        if (yearMatches.Count > 0)
            return PickBest(yearMatches);

        var runtimeMajor = Environment.Version.Major;
        var compatible = new List<(PyRevitExtensionPaths.DllCandidate Candidate, int Major)>();
        foreach (var item in items)
        {
            if (!TryReadTfmMajor(item.FilePath, out var major))
                continue;

            if (major <= runtimeMajor)
                compatible.Add((item, major));
        }

        if (compatible.Count == 0)
            return PickBest(items);

        var bestMajor = compatible.Max(r => r.Major);
        var bestCandidates = compatible
            .Where(r => r.Major == bestMajor)
            .Select(r => r.Candidate)
            .ToList();

        return PickBest(bestCandidates);
    }

    private static string PickBest(List<PyRevitExtensionPaths.DllCandidate> items) =>
        items
            .OrderBy(c => c.IsLib ? 0 : 1)
            .ThenBy(c => c.Depth)
            .First()
            .FilePath;

    private static void LoadAll(List<string> dllPaths, ILogger? logger = null)
    {
        foreach (var dllPath in dllPaths)
        {
            var simpleName = Path.GetFileNameWithoutExtension(dllPath);
            if (LoadedNames.Contains(simpleName))
                continue;

            try
            {
                var bytes = File.ReadAllBytes(dllPath);
                Assembly.Load(bytes);
                LoadedNames.Add(simpleName);
                logger?.ZLogInformation($"[PyRevit] Loaded extension DLL: {Path.GetFileName(dllPath)}");
            }
            catch (Exception ex)
            {
                logger?.ZLogWarning($"[PyRevit] Failed to load '{Path.GetFileName(dllPath)}': {ex.Message}");
            }
        }
    }

    private static bool PathContainsSegment(string filePath, string segment)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir)) return false;

        foreach (var part in dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.Equals(part, segment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    #region PE Metadata

    /// <summary>
    /// Returns true if the file has valid .NET PE metadata (is a managed assembly).
    /// </summary>
    private static bool IsValidAssembly(string dllPath)
    {
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            return peReader.HasMetadata;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads TFM major version from PE metadata. Returns false if not a valid .NET assembly.
    /// </summary>
    private static bool TryReadTfmMajor(string dllPath, out int major)
    {
        major = 0;
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) return false;

            var reader = peReader.GetMetadataReader();
            var assemblyDef = reader.GetAssemblyDefinition();

            foreach (var attrHandle in assemblyDef.GetCustomAttributes())
            {
                var attr = reader.GetCustomAttribute(attrHandle);
                if (attr.Constructor.Kind != HandleKind.MemberReference) continue;

                var ctor = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                if (ctor.Parent.Kind != HandleKind.TypeReference) continue;

                var typeRef = reader.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                if (reader.GetString(typeRef.Name) != "TargetFrameworkAttribute") continue;

                var blob = reader.GetBlobReader(attr.Value);
                blob.ReadUInt16();
                var tfmString = blob.ReadSerializedString();
                major = ParseTfmMajor(tfmString);
                return true;
            }

            // Valid .NET assembly but no TargetFrameworkAttribute (old-style) — treat as net4
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
        if (string.IsNullOrEmpty(tfmString)) return 4;

        var vIndex = tfmString!.IndexOf("=v", StringComparison.OrdinalIgnoreCase);
        if (vIndex < 0) return 4;

        var versionSpan = tfmString.AsSpan(vIndex + 2);
        var dotIndex = versionSpan.IndexOf('.');
        var majorSpan = dotIndex > 0 ? versionSpan[..dotIndex] : versionSpan;

        return int.TryParse(majorSpan.ToString(), out var major) ? major : 4;
    }

    #endregion
}
