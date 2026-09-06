using System.Reflection;

namespace DevTools.Testing.Host.Loading;

public static class HostRuntimeSources
{
    public static HostRuntimeSource ResolveBesideHost(
        Assembly hostAssembly,
        string runtimeFolderName,
        string runtimeAssemblyFileName,
        string? runtimeSymbolFileName = null)
    {
        ArgumentNullException.ThrowIfNull(hostAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeFolderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeAssemblyFileName);

        var hostDirectory = Path.GetDirectoryName(hostAssembly.Location) ?? AppContext.BaseDirectory;
        var runtimeDirectory = Path.Combine(hostDirectory, runtimeFolderName);
        var assemblyPath = Path.Combine(runtimeDirectory, runtimeAssemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidOperationException(
                $"Runtime assembly not found beside the host at '{assemblyPath}'. " +
                $"Deploy {runtimeAssemblyFileName} under {runtimeFolderName}\\ with the host add-in.");
        }

        string? symbolPath = null;
        if (runtimeSymbolFileName is not null)
        {
            var candidate = Path.Combine(runtimeDirectory, runtimeSymbolFileName);
            if (File.Exists(candidate))
                symbolPath = candidate;
        }

        var dependencies = Directory.Exists(runtimeDirectory)
            ? Directory.EnumerateFiles(runtimeDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path =>
                    !string.Equals(path, assemblyPath, StringComparison.OrdinalIgnoreCase)
                    && (symbolPath is null || !string.Equals(path, symbolPath, StringComparison.OrdinalIgnoreCase)))
                .ToList()
            : [];

        return new HostRuntimeSource(assemblyPath, symbolPath, dependencies);
    }

    public static HostRuntimeSource Normalize(
        HostRuntimeSource source,
        Func<string, Exception> throwMissing)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(throwMissing);

        if (string.IsNullOrWhiteSpace(source.AssemblyPath))
            throw throwMissing("Runtime assembly path provider returned an empty path.");

        var assemblyPath = Path.GetFullPath(source.AssemblyPath);
        if (!File.Exists(assemblyPath))
            throw throwMissing($"Runtime assembly not found: {assemblyPath}");

        string? symbolPath = null;
        if (!string.IsNullOrWhiteSpace(source.SymbolPath))
        {
            symbolPath = Path.GetFullPath(source.SymbolPath);
            if (!File.Exists(symbolPath))
                throw throwMissing($"Runtime symbol file not found: {symbolPath}");
        }

        var dependencies = source.DependencyPaths.Select(Path.GetFullPath).ToList();
        foreach (var dependency in dependencies)
        {
            if (!File.Exists(dependency))
                throw throwMissing($"Runtime dependency not found: {dependency}");
        }

        return new HostRuntimeSource(assemblyPath, symbolPath, dependencies);
    }
}
