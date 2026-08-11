using System.Reflection;
using DevTools.Utilities.AssemblyLoading;

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
/// Validates test assembly paths and installs a scoped resolve from the test output directory.
/// </summary>
public sealed class NUnitAssemblyLoader
{
    public NUnitAssemblyPreflightResult Preflight(string assemblyPath)
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
            _ = AssemblyName.GetAssemblyName(fullPath);
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

    public void EnsureLoadable(string assemblyPath)
    {
        var result = Preflight(assemblyPath);
        if (!result.Success)
            throw new NUnitAssemblyLoadException(result);
    }

    public string ResolveAssemblyPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));

        return Path.GetFullPath(assemblyPath);
    }

    internal T ExecuteWithHostResolve<T>(Func<AssemblyResolveScope, T> action, string? testAssemblyPath = null)
    {
        var probeDirectory = GetProbeDirectory(testAssemblyPath);
        using var scope = BeginResolveScope(probeDirectory);
        var previous = Environment.CurrentDirectory;
        try
        {
            if (probeDirectory is not null)
                Environment.CurrentDirectory = probeDirectory;

            return action(scope);
        }
        finally
        {
            try
            {
                Environment.CurrentDirectory = previous;
            }
            catch
            {
                // Host processes may deny directory restore; ignore.
            }
        }
    }

    private static string? GetProbeDirectory(string? testAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(testAssemblyPath))
            return null;

        return Path.GetDirectoryName(Path.GetFullPath(testAssemblyPath));
    }

    private static AssemblyResolveScope BeginResolveScope(string? probeDirectory)
    {
        return new AssemblyResolveScope(Resolve);

        Assembly? Resolve(AssemblyName name)
        {
            if (probeDirectory is null)
                return null;

            return DirectoryAssemblyLoad.TryLoad(probeDirectory, name);
        }
    }
}
