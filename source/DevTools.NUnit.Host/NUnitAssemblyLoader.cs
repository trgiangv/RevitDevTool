using System.Reflection;

namespace DevTools.NUnit.Host;

public sealed record NUnitAssemblyPreflightResult(
    bool Success,
    string AssemblyPath,
    string? Message,
    string? Details)
{
    public static NUnitAssemblyPreflightResult Succeeded(string assemblyPath) =>
        new(true, assemblyPath, null, null);

    public static NUnitAssemblyPreflightResult Failed(
        string assemblyPath,
        string message,
        string? details = null) =>
        new(false, assemblyPath, message, details);
}

public sealed class NUnitAssemblyLoadException : Exception
{
    public NUnitAssemblyLoadException(NUnitAssemblyPreflightResult result)
        : base(result.Message ?? "Failed to load test assembly.")
    {
        Result = result;
    }

    public NUnitAssemblyPreflightResult Result { get; }
}

/// <summary>
/// Validates test assembly paths before generation load.
/// </summary>
public sealed class NUnitAssemblyLoader
{
    public static NUnitAssemblyPreflightResult Preflight(string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return NUnitAssemblyPreflightResult.Failed(
                assemblyPath ?? string.Empty,
                "Assembly path is required.");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            return NUnitAssemblyPreflightResult.Failed(
                fullPath,
                $"Assembly not found: {fullPath}");
        }

        try
        {
            AssemblyName.GetAssemblyName(fullPath);
            return NUnitAssemblyPreflightResult.Succeeded(fullPath);
        }
        catch (Exception ex)
        {
            return NUnitAssemblyPreflightResult.Failed(
                fullPath,
                $"Failed to read assembly metadata: {ex.Message}",
                ex.ToString());
        }
    }

    public static void EnsureLoadable(string assemblyPath)
    {
        var result = Preflight(assemblyPath);
        if (!result.Success)
            throw new NUnitAssemblyLoadException(result);
    }

    public static string ResolveAssemblyPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));

        return Path.GetFullPath(assemblyPath);
    }
}
