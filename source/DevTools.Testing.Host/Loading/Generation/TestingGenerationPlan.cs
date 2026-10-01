using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Host.Loading;

public sealed record TestingGenerationPlan(
    TestFrameworkId FrameworkId,
    string SourceAssemblyPath,
    IReadOnlyList<(string SourcePath, string RelativePath)> Files,
    string RuntimeAssemblyRelativePath)
{
    public void ValidateShape()
    {
        if (!Enum.IsDefined(FrameworkId))
            throw new TestingGenerationBuildException("Generation framework ID is required.");
        if (Files is null || Files.Count == 0)
            throw new TestingGenerationBuildException("Generation plan must contain files.");

        var relativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Files)
        {
            if (string.IsNullOrWhiteSpace(file.RelativePath) || Path.IsPathRooted(file.RelativePath)
                || file.RelativePath.Split('/', '\\').Any(segment => segment == ".."))
            {
                throw new TestingGenerationBuildException($"Generation file path must be a relative path: {file.RelativePath}");
            }

            if (!relativePaths.Add(file.RelativePath))
                throw new TestingGenerationBuildException($"Generation plan contains duplicate path: {file.RelativePath}");
        }

        if (!relativePaths.Contains(RuntimeAssemblyRelativePath))
            throw new TestingGenerationBuildException("Generation plan runtime assembly path is not included in its files.");
    }

    public TestingGenerationManifest ToManifest(string generationId, string shadowDirectory)
    {
        var sourceAssemblyPath = Path.GetFullPath(SourceAssemblyPath);
        var sourceFile = Files.FirstOrDefault(file =>
            string.Equals(Path.GetFullPath(file.SourcePath), sourceAssemblyPath, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(sourceFile.SourcePath))
            throw new TestingGenerationBuildException("Generation plan does not include its source assembly.");

        return new TestingGenerationManifest(
            generationId,
            FrameworkId,
            Path.GetFullPath(SourceAssemblyPath),
            shadowDirectory,
            Resolve(sourceFile.RelativePath),
            Resolve(RuntimeAssemblyRelativePath),
            Files.Where(file => TestingGenerationFiles.IsManagedIdentity(file.SourcePath))
                .Select(file => Resolve(file.RelativePath))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList());

        string Resolve(string relative) =>
            Path.Combine(shadowDirectory, relative);
    }
}
