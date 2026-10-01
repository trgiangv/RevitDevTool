using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DevTools.Testing.Host.Loading;

public sealed class TestingGenerationStore(string? generationsRootDirectory = null)
{
    private const int MaxPublishAttempts = 3;
    private static readonly ConcurrentDictionary<string, Lock> GenerationLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _generationsRootDirectory = generationsRootDirectory
                                                        ?? Path.Combine(Path.GetTempPath(), "DevTools.Testing");

    // Deterministic test/diagnostic seam; callers must not mutate source or staging content.
    public Action<string>? AfterFileCopied { get; set; }

    public TestingGenerationManifest Build(ITestingGenerationPolicy policy, string testAssemblyPath)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var plan = policy.CreatePlan(testAssemblyPath) ?? throw new TestingGenerationBuildException("Generation policy returned no plan.");
        ValidateSources(plan, testAssemblyPath);
        string? lastFailure = null;

        for (var attempt = 0; attempt < MaxPublishAttempts; attempt++)
        {
            if (TryBuild(policy, plan, out var manifest, out lastFailure))
                return manifest!;
        }

        throw new TestingGenerationBuildException(
            $"Failed to publish a coherent generation after {MaxPublishAttempts} attempts. Last failure: {lastFailure ?? "unknown"}.");
    }

    private bool TryBuild(
        ITestingGenerationPolicy policy,
        TestingGenerationPlan plan,
        out TestingGenerationManifest? manifest,
        out string? failure)
    {
        manifest = null;
        failure = null;
        var staging = Path.Combine(_generationsRootDirectory, ".staging." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var metadata = plan.Files.Select(file => (File: file, Metadata: SourceMetadata.Capture(file.SourcePath))).ToList();
            foreach (var item in metadata)
            {
                TestingGenerationPublish.CopyFile(item.File.SourcePath, Path.Combine(staging, item.File.RelativePath));
                AfterFileCopied?.Invoke(item.File.SourcePath);
            }

            if (metadata.Any(item => !item.Metadata.Matches(item.File.SourcePath)))
            {
                failure = "source files changed during copy";
                return false;
            }

            var contentPaths = plan.Files.Select(file => file.RelativePath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            var generationId = TestingGenerationPublish.ComputeGenerationId(staging, contentPaths);
            if (!string.Equals(generationId, TestingGenerationPublish.ComputeGenerationId(staging, contentPaths), StringComparison.Ordinal))
            {
                failure = "staged generation changed before publication";
                return false;
            }

            var shadowDirectory = Path.Combine(_generationsRootDirectory, generationId);
            var generationLock = GenerationLocks.GetOrAdd(generationId, static _ => new Lock());
            lock (generationLock)
            {
                try
                {
                    if (Directory.Exists(shadowDirectory))
                    {
                        TestingGenerationPublish.EnsurePublishedIsValid(shadowDirectory, generationId);
                    }
                    else
                    {
                        TestingGenerationPublish.Publish(staging, shadowDirectory, generationId);
                        TestingGenerationPublish.EnsurePublishedIsValid(shadowDirectory, generationId);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failure = ex.Message;
                    return false;
                }
            }

            manifest = plan.ToManifest(generationId, shadowDirectory);
            policy.ValidatePublished(manifest);
            return true;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static void ValidateSources(TestingGenerationPlan plan, string testAssemblyPath)
    {
        plan.ValidateShape();

        var sourceAssemblyPath = Path.GetFullPath(plan.SourceAssemblyPath);
        if (!File.Exists(sourceAssemblyPath))
            throw new TestingGenerationBuildException($"Test assembly not found: {sourceAssemblyPath}");
        if (!string.Equals(sourceAssemblyPath, Path.GetFullPath(testAssemblyPath), StringComparison.OrdinalIgnoreCase))
            throw new TestingGenerationBuildException("Generation plan source assembly does not match the requested test assembly.");

        foreach (var file in plan.Files)
        {
            if (string.IsNullOrWhiteSpace(file.SourcePath) || !File.Exists(file.SourcePath))
                throw new TestingGenerationBuildException($"Generation file not found: {file.SourcePath}");
        }
    }

    private sealed record SourceMetadata(long Length, DateTime LastWriteUtc, string ContentHash)
    {
        internal static SourceMetadata Capture(string path)
        {
            var info = new FileInfo(path);
            return new SourceMetadata(info.Length, info.LastWriteTimeUtc, ComputeContentHash(path));
        }

        internal bool Matches(string path)
        {
            var info = new FileInfo(path);
            return info.Exists
                && info.Length == Length
                && info.LastWriteTimeUtc == LastWriteUtc
                && string.Equals(ComputeContentHash(path), ContentHash, StringComparison.Ordinal);
        }

        private static string ComputeContentHash(string path)
        {
            using var hash = SHA256.Create();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToBase64String(hash.ComputeHash(stream));
        }
    }
}
