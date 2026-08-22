namespace DevTools.Testing.Host.Loading;

public enum TestingGenerationFileKind
{
    Managed,
    Native,
    Symbols,
    Other,
}

public sealed record TestingGenerationFile(
    string SourcePath,
    string RelativePath,
    TestingGenerationFileKind Kind);

public sealed record TestingGenerationPlan(
    string FrameworkId,
    string SourceAssemblyPath,
    IReadOnlyList<TestingGenerationFile> Files,
    string RuntimeAssemblyRelativePath)
{
    public void ValidateShape()
    {
        if (string.IsNullOrWhiteSpace(FrameworkId))
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

            if (!relativePaths.Add(TestingGenerationPaths.NormalizeRelativePath(file.RelativePath)))
                throw new TestingGenerationBuildException($"Generation plan contains duplicate path: {file.RelativePath}");
        }

        if (!relativePaths.Contains(TestingGenerationPaths.NormalizeRelativePath(RuntimeAssemblyRelativePath)))
            throw new TestingGenerationBuildException("Generation plan runtime assembly path is not included in its files.");
    }
}

public interface ITestingGenerationPolicy
{
    TestingGenerationPlan CreatePlan(string testAssemblyPath);

    void ValidatePublished(TestingGenerationManifest manifest);
}

public class TestingGenerationBuildException : Exception
{
    public TestingGenerationBuildException(string message)
        : base(message)
    {
    }

    public TestingGenerationBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class TestingGenerationCorruptionException : TestingGenerationBuildException
{
    public TestingGenerationCorruptionException(
        string shadowDirectory,
        string expectedGenerationId,
        string actualGenerationId)
        : base($"Published generation at '{shadowDirectory}' is corrupted: expected generation ID '{expectedGenerationId}', actual content hash '{actualGenerationId}'.")
    {
        ShadowDirectory = shadowDirectory;
        ExpectedGenerationId = expectedGenerationId;
        ActualGenerationId = actualGenerationId;
    }

    public string ShadowDirectory { get; }

    public string ExpectedGenerationId { get; }

    public string ActualGenerationId { get; }
}
