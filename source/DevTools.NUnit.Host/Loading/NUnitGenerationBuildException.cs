namespace DevTools.NUnit.Host.Loading;

public sealed class NUnitGenerationBuildException : Exception
{
    public NUnitGenerationBuildException(string message) : base(message) { }
    public NUnitGenerationBuildException(string message, Exception innerException) : base(message, innerException) { }
}
