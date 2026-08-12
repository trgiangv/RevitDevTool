namespace DevTools.NUnit.Host.Loading;

public class NUnitGenerationLoadException : Exception
{
    public NUnitGenerationLoadException(string message)
        : base(message)
    {
    }

    public NUnitGenerationLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class NUnitGenerationAssemblyResolutionException : NUnitGenerationLoadException
{
    public NUnitGenerationAssemblyResolutionException(string message)
        : base(message)
    {
    }

    public NUnitGenerationAssemblyResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
