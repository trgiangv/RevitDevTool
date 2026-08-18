using System.Reflection;

namespace DevTools.Testing.Host;

public sealed record TestingAssemblyPreflightResult(
    bool Success,
    string AssemblyPath,
    string? Message,
    string? Details);

public sealed class TestingAssemblyPreflightException : Exception
{
    public TestingAssemblyPreflightException(TestingAssemblyPreflightResult result)
        : base(result.Message ?? "Failed to validate test assembly.") => Result = result;

    public TestingAssemblyPreflightResult Result { get; }
}

/// <summary>Validates a managed test assembly without loading it.</summary>
public static class TestingAssemblyPreflight
{
    public static TestingAssemblyPreflightResult Check(string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            return Failed(assemblyPath ?? string.Empty, "Assembly path is required.");

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
            return Failed(fullPath, $"Assembly not found: {fullPath}");

        try
        {
            AssemblyName.GetAssemblyName(fullPath);
            return new TestingAssemblyPreflightResult(true, fullPath, null, null);
        }
        catch (Exception ex)
        {
            return Failed(fullPath, $"Failed to read assembly metadata: {ex.Message}", ex.ToString());
        }
    }

    public static string ResolveAndEnsureLoadable(string assemblyPath)
    {
        var result = Check(assemblyPath);
        if (!result.Success)
            throw new TestingAssemblyPreflightException(result);
        return result.AssemblyPath;
    }

    private static TestingAssemblyPreflightResult Failed(string path, string message, string? details = null) =>
        new(false, path, message, details);
}
