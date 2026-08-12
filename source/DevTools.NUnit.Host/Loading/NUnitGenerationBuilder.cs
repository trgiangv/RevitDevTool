using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace DevTools.NUnit.Host.Loading;

public class NUnitGenerationBuildException : Exception
{
    public NUnitGenerationBuildException(string message)
        : base(message)
    {
    }

    public NUnitGenerationBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class NUnitGenerationCorruptionException : NUnitGenerationBuildException
{
    public NUnitGenerationCorruptionException(
        string shadowDirectory,
        string expectedGenerationId,
        string actualGenerationId)
        : base(
            $"Published generation at '{shadowDirectory}' is corrupted: expected generation ID '{expectedGenerationId}', actual content hash '{actualGenerationId}'.")
    {
        ShadowDirectory = shadowDirectory;
        ExpectedGenerationId = expectedGenerationId;
        ActualGenerationId = actualGenerationId;
    }

    public string ShadowDirectory { get; }

    public string ExpectedGenerationId { get; }

    public string ActualGenerationId { get; }
}

public sealed record NUnitRuntimeSource(
    string AssemblyPath,
    string? SymbolPath,
    IReadOnlyList<string> DependencyPaths);

public delegate NUnitRuntimeSource NUnitRuntimeSourcePathProvider();

internal enum SnapshotCopyPhase
{
    BeforeCopy,
    AfterCopy,
}

internal static class NUnitGenerationContentHash
{
    internal const byte FormatVersion = 1;

    internal static string ComputeGenerationId(IEnumerable<(string RelativePath, string AbsolutePath)> entries)
    {
        var orderedEntries = entries
            .Select(entry => (
                CanonicalPath: NUnitGenerationBuilder.CanonicalizeHashRelativePath(entry.RelativePath),
                entry.AbsolutePath))
            .OrderBy(static entry => entry.CanonicalPath, StringComparer.Ordinal)
            .ToList();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(new[] { FormatVersion });

        foreach (var entry in orderedEntries)
        {
            var pathBytes = Encoding.UTF8.GetBytes(entry.CanonicalPath);
            NUnitGenerationBuilder.AppendUInt32LittleEndian(hash, checked((uint)pathBytes.Length));
            hash.AppendData(pathBytes);

            using var stream = File.OpenRead(entry.AbsolutePath);
            NUnitGenerationBuilder.AppendInt64LittleEndian(hash, stream.Length);

            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static void AppendUInt32LittleEndian(IncrementalHash hash, uint value)
    {
        hash.AppendData(new[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
        });
    }

    internal static void AppendInt64LittleEndian(IncrementalHash hash, long value)
    {
        hash.AppendData(new[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
            (byte)(value >> 32),
            (byte)(value >> 40),
            (byte)(value >> 48),
            (byte)(value >> 56),
        });
    }
}

public sealed class NUnitGenerationBuilder : INUnitGenerationBuilder
{
    public const string RuntimeAssemblyFileName = "DevTools.NUnit.Runtime.dll";
    public const string RuntimeSymbolFileName = "DevTools.NUnit.Runtime.pdb";
    public const string FrameworkAssemblyFileName = "nunit.framework.dll";
    public const string GenerationCompleteMarkerFileName = ".generation-complete";
    public const string ExpectedNUnitFileVersion = "4.6.1.0";
    public const string ExpectedNUnitPackageVersion = "4.6.1";

    private const int MaxSnapshotAttempts = 3;

    private static readonly ConcurrentDictionary<string, object> GenerationLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly NUnitRuntimeSourcePathProvider _runtimeSourcePathProvider;
    private readonly string _generationsRootDirectory;

    internal Action? AfterSnapshotBeforePublishHook { get; set; }

    internal Action<string, SnapshotCopyPhase>? SnapshotCopyProgressHook { get; set; }

    public NUnitGenerationBuilder(
        NUnitRuntimeSourcePathProvider runtimeSourcePathProvider,
        string? generationsRootDirectory = null)
    {
        if (runtimeSourcePathProvider is null)
            throw new ArgumentNullException(nameof(runtimeSourcePathProvider));
        _runtimeSourcePathProvider = runtimeSourcePathProvider;
        _generationsRootDirectory = generationsRootDirectory
            ?? Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", "Generations");
    }

    public NUnitGenerationManifest Build(string testAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(testAssemblyPath))
            throw new ArgumentException("Test assembly path is required.", nameof(testAssemblyPath));

        var sourceAssemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(sourceAssemblyPath))
        {
            throw new NUnitGenerationBuildException(
                $"Test assembly not found: {sourceAssemblyPath}");
        }

        var sourceOutputDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new NUnitGenerationBuildException(
                $"Test assembly path has no directory: {sourceAssemblyPath}");

        var runtimeSource = ResolveRuntimeSource();

        for (var attempt = 0; attempt < MaxSnapshotAttempts; attempt++)
        {
            var stagingDirectory = CreateUniqueStagingDirectory();
            try
            {
                var copyPlan = CreateCopyPlan(sourceAssemblyPath, sourceOutputDirectory, runtimeSource);
                if (!TrySnapshotCopyPlan(stagingDirectory, copyPlan))
                    continue;

                if (!ValidateStagedAssemblies(stagingDirectory, copyPlan))
                    continue;

                var generationId = ComputeGenerationIdFromSnapshot(stagingDirectory, copyPlan.ContentRelativePaths);
                if (!VerifySnapshotGenerationId(stagingDirectory, copyPlan.ContentRelativePaths, generationId))
                    continue;

                AfterSnapshotBeforePublishHook?.Invoke();

                if (!VerifySnapshotGenerationId(stagingDirectory, copyPlan.ContentRelativePaths, generationId))
                    continue;

                var shadowDirectory = Path.Combine(_generationsRootDirectory, generationId);
                var generationLock = GenerationLocks.GetOrAdd(generationId, static _ => new object());
                lock (generationLock)
                {
                    if (Directory.Exists(shadowDirectory) && IsPublishedGeneration(shadowDirectory))
                    {
                        EnsurePublishedGenerationIsValid(shadowDirectory, generationId);
                        return CreateManifestFromPublishedSnapshot(
                            generationId,
                            sourceAssemblyPath,
                            shadowDirectory);
                    }

                    if (!VerifySnapshotGenerationId(stagingDirectory, copyPlan.ContentRelativePaths, generationId))
                        continue;

                    PublishSnapshot(stagingDirectory, shadowDirectory, generationId);
                    EnsurePublishedGenerationIsValid(shadowDirectory, generationId);
                    return CreateManifestFromPublishedSnapshot(
                        generationId,
                        sourceAssemblyPath,
                        shadowDirectory);
                }
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, recursive: true);
            }
        }

        throw new NUnitGenerationBuildException(
            $"Failed to create a coherent generation snapshot after {MaxSnapshotAttempts} attempts.");
    }

    internal static string ComputeGenerationIdForTesting(
        IReadOnlyList<(string RelativePath, string AbsolutePath)> entries) =>
        NUnitGenerationContentHash.ComputeGenerationId(entries);

    internal static string CanonicalizeHashRelativePath(string relativePath) =>
        NormalizeRelativePath(relativePath).ToLowerInvariant();

    internal static void AppendUInt32LittleEndian(IncrementalHash hash, uint value) =>
        NUnitGenerationContentHash.AppendUInt32LittleEndian(hash, value);

    internal static void AppendInt64LittleEndian(IncrementalHash hash, long value) =>
        NUnitGenerationContentHash.AppendInt64LittleEndian(hash, value);

    private NUnitRuntimeSource ResolveRuntimeSource()
    {
        var source = _runtimeSourcePathProvider();
        var assemblyPath = source.AssemblyPath;
        var symbolPath = source.SymbolPath;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new NUnitGenerationBuildException(
                "Runtime assembly path provider returned an empty path.");
        }

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullAssemblyPath))
        {
            throw new NUnitGenerationBuildException(
                $"Runtime assembly not found: {fullAssemblyPath}");
        }

        string? fullSymbolPath = null;
        if (!string.IsNullOrWhiteSpace(symbolPath))
        {
            fullSymbolPath = Path.GetFullPath(symbolPath);
            if (!File.Exists(fullSymbolPath))
            {
                throw new NUnitGenerationBuildException(
                    $"Runtime symbol file not found: {fullSymbolPath}");
            }
        }

        var dependencyPaths = source.DependencyPaths
            .Select(Path.GetFullPath)
            .ToList();
        foreach (var dependencyPath in dependencyPaths)
        {
            if (!File.Exists(dependencyPath))
            {
                throw new NUnitGenerationBuildException(
                    $"Runtime dependency not found: {dependencyPath}");
            }
        }

        return new NUnitRuntimeSource(fullAssemblyPath, fullSymbolPath, dependencyPaths);
    }

    private static GenerationCopyPlan CreateCopyPlan(
        string sourceAssemblyPath,
        string sourceOutputDirectory,
        NUnitRuntimeSource runtimeSource)
    {
        var sourceAssemblyRelativePath = NormalizeRelativePath(
            GetRelativePath(sourceOutputDirectory, sourceAssemblyPath));

        var outputFiles = Directory.EnumerateFiles(sourceOutputDirectory, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ValidateNUnitFramework(outputFiles, sourceOutputDirectory);

        var copyEntries = new List<GenerationCopyEntry>();
        var contentRelativePaths = new List<string>();
        var managedAssemblyRelativePaths = new List<string>();
        string? frameworkRelativePath = null;
        string? testSymbolRelativePath = null;

        foreach (var sourceFile in outputFiles)
        {
            var relativePath = NormalizeRelativePath(
                GetRelativePath(sourceOutputDirectory, sourceFile));

            if (NUnitSharedAssemblyPolicy.ShouldExcludeFromGenerationCopy(sourceFile))
                continue;

            if (TryGetManagedAssemblySimpleName(sourceFile, out var simpleName))
            {
                managedAssemblyRelativePaths.Add(relativePath);

                if (string.Equals(
                        simpleName,
                        Path.GetFileNameWithoutExtension(FrameworkAssemblyFileName),
                        StringComparison.OrdinalIgnoreCase))
                {
                    frameworkRelativePath = relativePath;
                }
            }

            copyEntries.Add(new GenerationCopyEntry(sourceFile, relativePath));
            contentRelativePaths.Add(relativePath);
        }

        if (frameworkRelativePath is null)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {FrameworkAssemblyFileName} is required in the test output directory.");
        }

        var testPdbPath = Path.ChangeExtension(sourceAssemblyPath, ".pdb");
        if (File.Exists(testPdbPath))
        {
            testSymbolRelativePath = NormalizeRelativePath(
                GetRelativePath(sourceOutputDirectory, testPdbPath));
        }

        copyEntries.Add(new GenerationCopyEntry(runtimeSource.AssemblyPath, RuntimeAssemblyFileName));
        contentRelativePaths.Add(RuntimeAssemblyFileName);
        managedAssemblyRelativePaths.Add(RuntimeAssemblyFileName);

        if (!string.IsNullOrWhiteSpace(runtimeSource.SymbolPath))
        {
            var symbolPath = runtimeSource.SymbolPath!;
            copyEntries.Add(new GenerationCopyEntry(symbolPath, RuntimeSymbolFileName));
            contentRelativePaths.Add(RuntimeSymbolFileName);
        }

        foreach (var dependencyPath in runtimeSource.DependencyPaths)
        {
            if (NUnitSharedAssemblyPolicy.ShouldExcludeFromGenerationCopy(dependencyPath))
                continue;

            var relativePath = Path.GetFileName(dependencyPath);
            if (string.Equals(relativePath, RuntimeAssemblyFileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(relativePath, RuntimeSymbolFileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(relativePath, FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existingIndex = copyEntries.FindIndex(entry =>
                string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                // Runtime owns its private dependency closure. When the test output
                // already copied a different build of the same simple name (common for
                // net48 polyfills), keep the Runtime copy so Reflection.Metadata and
                // nunit.framework bind coherently with DevTools.NUnit.Runtime.
                if (!FilesHaveEqualContent(copyEntries[existingIndex].SourcePath, dependencyPath))
                    copyEntries[existingIndex] = new GenerationCopyEntry(dependencyPath, relativePath);

                continue;
            }

            copyEntries.Add(new GenerationCopyEntry(dependencyPath, relativePath));
            contentRelativePaths.Add(relativePath);
            if (TryGetManagedAssemblySimpleName(dependencyPath, out _))
                managedAssemblyRelativePaths.Add(relativePath);
        }

        contentRelativePaths.Sort(StringComparer.OrdinalIgnoreCase);
        managedAssemblyRelativePaths.Sort(StringComparer.OrdinalIgnoreCase);

        return new GenerationCopyPlan(
            sourceAssemblyRelativePath,
            testSymbolRelativePath,
            frameworkRelativePath,
            copyEntries,
            contentRelativePaths,
            managedAssemblyRelativePaths);
    }

    private static bool FilesHaveEqualContent(string firstPath, string secondPath)
    {
        var first = new FileInfo(firstPath);
        var second = new FileInfo(secondPath);
        if (first.Length != second.Length)
            return false;

        using var firstStream = File.OpenRead(firstPath);
        using var secondStream = File.OpenRead(secondPath);
        var firstBuffer = new byte[81920];
        var secondBuffer = new byte[81920];
        int firstRead;
        while ((firstRead = firstStream.Read(firstBuffer, 0, firstBuffer.Length)) > 0)
        {
            var secondRead = secondStream.Read(secondBuffer, 0, secondBuffer.Length);
            if (secondRead != firstRead
                || !firstBuffer.AsSpan(0, firstRead).SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
            {
                return false;
            }
        }

        return secondStream.ReadByte() == -1;
    }

    private bool TrySnapshotCopyPlan(string stagingDirectory, GenerationCopyPlan plan)
    {
        Directory.CreateDirectory(stagingDirectory);

        var sourceMetadata = plan.CopyEntries
            .Select(entry => (Entry: entry, Metadata: CaptureSourceMetadata(entry.SourcePath)))
            .ToList();

        foreach (var item in sourceMetadata)
        {
            SnapshotCopyProgressHook?.Invoke(item.Entry.SourcePath, SnapshotCopyPhase.BeforeCopy);

            var destinationPath = Path.Combine(stagingDirectory, item.Entry.RelativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            CopyWithoutLockingSource(item.Entry.SourcePath, destinationPath);

            SnapshotCopyProgressHook?.Invoke(item.Entry.SourcePath, SnapshotCopyPhase.AfterCopy);
        }

        foreach (var item in sourceMetadata)
        {
            if (!SourceMetadataMatches(item.Entry.SourcePath, item.Metadata))
                return false;
        }

        return true;
    }

    private static bool ValidateStagedAssemblies(string stagingDirectory, GenerationCopyPlan plan)
    {
        var stagedTestAssembly = Path.Combine(stagingDirectory, plan.SourceAssemblyRelativePath);
        if (!TryGetManagedAssemblySimpleName(stagedTestAssembly, out _))
        {
            return false;
        }

        var stagedFramework = Path.Combine(stagingDirectory, plan.FrameworkRelativePath);
        try
        {
            ValidateNUnitFrameworkVersion(stagedFramework);
        }
        catch (NUnitGenerationBuildException)
        {
            return false;
        }

        foreach (var relativePath in plan.ManagedAssemblyRelativePaths)
        {
            var stagedPath = Path.Combine(stagingDirectory, relativePath);
            if (!TryGetManagedAssemblySimpleName(stagedPath, out _))
                return false;
        }

        return true;
    }

    private static SourceFileMetadata CaptureSourceMetadata(string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        return new SourceFileMetadata(info.Length, info.LastWriteTimeUtc);
    }

    private static bool SourceMetadataMatches(string sourcePath, SourceFileMetadata metadata)
    {
        var info = new FileInfo(sourcePath);
        return info.Length == metadata.Length
            && info.LastWriteTimeUtc == metadata.LastWriteUtc;
    }

    private static string ComputeGenerationIdFromSnapshot(
        string snapshotDirectory,
        IReadOnlyList<string> contentRelativePaths)
    {
        var entries = contentRelativePaths
            .Select(relativePath => (RelativePath: relativePath, AbsolutePath: Path.Combine(snapshotDirectory, relativePath)))
            .ToList();

        return NUnitGenerationContentHash.ComputeGenerationId(entries);
    }

    private static bool VerifySnapshotGenerationId(
        string snapshotDirectory,
        IReadOnlyList<string> contentRelativePaths,
        string expectedGenerationId) =>
        string.Equals(
            ComputeGenerationIdFromSnapshot(snapshotDirectory, contentRelativePaths),
            expectedGenerationId,
            StringComparison.Ordinal);

    private void PublishSnapshot(string stagingDirectory, string shadowDirectory, string generationId)
    {
        File.WriteAllText(
            Path.Combine(stagingDirectory, GenerationCompleteMarkerFileName),
            string.Empty);

        if (!VerifySnapshotGenerationId(
                stagingDirectory,
                ReadSnapshotContentRelativePaths(stagingDirectory),
                generationId))
        {
            throw new NUnitGenerationBuildException(
                "Refusing to publish a generation whose snapshot no longer matches its generation ID.");
        }

        if (Directory.Exists(shadowDirectory))
            return;

        try
        {
            Directory.Move(stagingDirectory, shadowDirectory);
        }
        catch (IOException) when (Directory.Exists(shadowDirectory))
        {
            // Another process published the same generation first.
        }
    }

    private static IReadOnlyList<string> ReadSnapshotContentRelativePaths(string snapshotDirectory) =>
        Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(GetRelativePath(snapshotDirectory, path)))
            .Where(relativePath => !string.Equals(
                relativePath,
                GenerationCompleteMarkerFileName,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void EnsurePublishedGenerationIsValid(string shadowDirectory, string expectedGenerationId)
    {
        if (!IsPublishedGeneration(shadowDirectory))
        {
            throw new NUnitGenerationBuildException(
                $"Expected published generation at '{shadowDirectory}' but the completion marker is missing.");
        }

        var actualGenerationId = ComputeGenerationIdFromSnapshot(
            shadowDirectory,
            ReadSnapshotContentRelativePaths(shadowDirectory));

        if (!string.Equals(actualGenerationId, expectedGenerationId, StringComparison.Ordinal))
        {
            throw new NUnitGenerationCorruptionException(
                shadowDirectory,
                expectedGenerationId,
                actualGenerationId);
        }
    }

    private static void ValidateNUnitFramework(IReadOnlyList<string> outputFiles, string sourceOutputDirectory)
    {
        var frameworkMatches = outputFiles
            .Where(path => string.Equals(
                Path.GetFileName(path),
                FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (frameworkMatches.Count == 0)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {FrameworkAssemblyFileName} {ExpectedNUnitPackageVersion} is required; none was found.");
        }

        if (frameworkMatches.Count > 1)
        {
            throw new NUnitGenerationBuildException(
                $"Exactly one {FrameworkAssemblyFileName} {ExpectedNUnitPackageVersion} is required; found {frameworkMatches.Count}.");
        }

        ValidateNUnitFrameworkVersion(frameworkMatches[0], sourceOutputDirectory);
    }

    internal static void ValidateNUnitFrameworkVersion(string frameworkPath, string? sourceOutputDirectory = null)
    {
        var fileVersion = FileVersionInfo.GetVersionInfo(frameworkPath).FileVersion;
        if (!string.Equals(fileVersion, ExpectedNUnitFileVersion, StringComparison.Ordinal))
        {
            var location = sourceOutputDirectory is null
                ? frameworkPath
                : NormalizeRelativePath(GetRelativePath(sourceOutputDirectory, frameworkPath));

            throw new NUnitGenerationBuildException(
                $"Expected {FrameworkAssemblyFileName} file version {ExpectedNUnitFileVersion} (NUnit package {ExpectedNUnitPackageVersion}); found {fileVersion ?? "<missing>"} at {location}.");
        }

        try
        {
            _ = AssemblyName.GetAssemblyName(frameworkPath);
        }
        catch (Exception ex)
        {
            throw new NUnitGenerationBuildException(
                $"{FrameworkAssemblyFileName} is not a valid managed assembly: {frameworkPath}",
                ex);
        }
    }

    private static bool TryGetManagedAssemblySimpleName(string filePath, out string? simpleName) =>
        NUnitSharedAssemblyPolicy.TryGetManagedAssemblyIdentity(filePath, out simpleName);

    private static bool IsNativeAsset(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".dll", StringComparison.OrdinalIgnoreCase)
        && !TryGetManagedAssemblySimpleName(filePath, out _);

    private static void CopyWithoutLockingSource(string sourcePath, string destinationPath)
    {
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);

        source.CopyTo(destination);
    }

    private static NUnitGenerationManifest CreateManifestFromPublishedSnapshot(
        string generationId,
        string sourceAssemblyPath,
        string shadowDirectory)
    {
        var sourceOutputDirectory = Path.GetDirectoryName(sourceAssemblyPath)
            ?? throw new NUnitGenerationBuildException(
                $"Test assembly path has no directory: {sourceAssemblyPath}");

        var sourceAssemblyRelativePath = NormalizeRelativePath(
            GetRelativePath(sourceOutputDirectory, sourceAssemblyPath));

        var managedAssemblies = new List<string>();
        var nativeAssets = new List<string>();
        string? frameworkRelativePath = null;
        string? testSymbolRelativePath = null;

        foreach (var absolutePath in Directory.EnumerateFiles(shadowDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeRelativePath(GetRelativePath(shadowDirectory, absolutePath));
            if (string.Equals(relativePath, GenerationCompleteMarkerFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryGetManagedAssemblySimpleName(absolutePath, out var simpleName))
            {
                managedAssemblies.Add(relativePath);

                if (string.Equals(
                        simpleName,
                        Path.GetFileNameWithoutExtension(FrameworkAssemblyFileName),
                        StringComparison.OrdinalIgnoreCase))
                {
                    frameworkRelativePath = relativePath;
                }
            }
            else if (IsNativeAsset(absolutePath))
            {
                nativeAssets.Add(relativePath);
            }
        }

        if (frameworkRelativePath is null)
        {
            throw new NUnitGenerationBuildException(
                $"Published generation {generationId} is missing {FrameworkAssemblyFileName}.");
        }

        var testSymbolCandidate = Path.ChangeExtension(sourceAssemblyRelativePath, ".pdb");
        if (File.Exists(Path.Combine(shadowDirectory, testSymbolCandidate)))
            testSymbolRelativePath = testSymbolCandidate;

        managedAssemblies.Sort(StringComparer.OrdinalIgnoreCase);
        nativeAssets.Sort(StringComparer.OrdinalIgnoreCase);

        return CreateManifest(
            generationId,
            sourceAssemblyPath,
            shadowDirectory,
            sourceAssemblyRelativePath,
            testSymbolRelativePath,
            frameworkRelativePath,
            managedAssemblies,
            nativeAssets);
    }

    private static bool IsPublishedGeneration(string shadowDirectory) =>
        Directory.Exists(shadowDirectory)
        && File.Exists(Path.Combine(shadowDirectory, GenerationCompleteMarkerFileName));

    private static NUnitGenerationManifest CreateManifest(
        string generationId,
        string sourceAssemblyPath,
        string shadowDirectory,
        string sourceAssemblyRelativePath,
        string? testSymbolRelativePath,
        string frameworkRelativePath,
        IReadOnlyList<string> managedAssemblies,
        IReadOnlyList<string> nativeAssets)
    {
        var shadowAssemblyPath = Path.Combine(shadowDirectory, sourceAssemblyRelativePath);
        var runtimeAssemblyPath = Path.Combine(shadowDirectory, RuntimeAssemblyFileName);
        var frameworkAssemblyPath = Path.Combine(shadowDirectory, frameworkRelativePath);
        string? symbolPath = null;

        if (!string.IsNullOrWhiteSpace(testSymbolRelativePath))
        {
            var candidate = Path.Combine(shadowDirectory, testSymbolRelativePath);
            if (File.Exists(candidate))
                symbolPath = candidate;
        }

        return new NUnitGenerationManifest(
            generationId,
            sourceAssemblyPath,
            shadowDirectory,
            shadowAssemblyPath,
            runtimeAssemblyPath,
            frameworkAssemblyPath,
            managedAssemblies
                .Select(relative => Path.Combine(shadowDirectory, relative))
                .ToList(),
            nativeAssets
                .Select(relative => Path.Combine(shadowDirectory, relative))
                .ToList(),
            symbolPath);
    }

    private string CreateUniqueStagingDirectory()
    {
        var directory = Path.Combine(
            _generationsRootDirectory,
            ".staging." + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        return directory;
    }

    internal static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('/', '\\');

    private static string GetRelativePath(string relativeTo, string path)
    {
        var relativeToUri = new Uri(AppendDirectorySeparator(relativeTo));
        var pathUri = new Uri(path);
        var relativeUri = relativeToUri.MakeRelativeUri(pathUri);
        return Uri.UnescapeDataString(
            relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            && !path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }

    private sealed record SourceFileMetadata(long Length, DateTime LastWriteUtc);

    private sealed record GenerationCopyEntry(string SourcePath, string RelativePath);

    private sealed record GenerationCopyPlan(
        string SourceAssemblyRelativePath,
        string? TestSymbolRelativePath,
        string FrameworkRelativePath,
        IReadOnlyList<GenerationCopyEntry> CopyEntries,
        IReadOnlyList<string> ContentRelativePaths,
        IReadOnlyList<string> ManagedAssemblyRelativePaths);
}
