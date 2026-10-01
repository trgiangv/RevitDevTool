namespace DevTools.Testing.Host.Loading;

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
