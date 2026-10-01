using System.Reflection;

namespace DevTools.Testing.Host.Loading;

/// <summary>
/// Runtime closure merged into a generation plan. The add-in resolves it from
/// the folder deployed beside itself; a policy checks the files and copies them.
/// </summary>
public sealed record RuntimeSource(
    string AssemblyPath,
    string? SymbolPath,
    IReadOnlyList<string> DependencyPaths)
{
    public static RuntimeSource ResolveBeside(
        Assembly addinAssembly,
        string runtimeFolderName,
        string runtimeAssemblyFileName,
        string? runtimeSymbolFileName = null)
    {
        ArgumentNullException.ThrowIfNull(addinAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeFolderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeAssemblyFileName);

        var addinDirectory = Path.GetDirectoryName(addinAssembly.Location) ?? AppContext.BaseDirectory;
        var runtimeDirectory = Path.Combine(addinDirectory, runtimeFolderName);
        var assemblyPath = Path.Combine(runtimeDirectory, runtimeAssemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidOperationException(
                $"Runtime assembly not found beside the add-in at '{assemblyPath}'. " +
                $"Deploy {runtimeAssemblyFileName} under {runtimeFolderName}\\ with the add-in.");
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

        return new RuntimeSource(assemblyPath, symbolPath, dependencies);
    }

    public RuntimeSource RequirePresent(Func<string, Exception> throwMissing)
    {
        ArgumentNullException.ThrowIfNull(throwMissing);

        if (string.IsNullOrWhiteSpace(AssemblyPath))
            throw throwMissing("Runtime assembly path provider returned an empty path.");

        var assemblyPath = Path.GetFullPath(AssemblyPath);
        if (!File.Exists(assemblyPath))
            throw throwMissing($"Runtime assembly not found: {assemblyPath}");

        string? symbolPath = null;
        if (!string.IsNullOrWhiteSpace(SymbolPath))
        {
            symbolPath = Path.GetFullPath(SymbolPath!);
            if (!File.Exists(symbolPath))
                throw throwMissing($"Runtime symbol file not found: {symbolPath}");
        }

        var dependencies = DependencyPaths.Select(Path.GetFullPath).ToList();
        foreach (var dependency in dependencies)
        {
            if (!File.Exists(dependency))
                throw throwMissing($"Runtime dependency not found: {dependency}");
        }

        return new RuntimeSource(assemblyPath, symbolPath, dependencies);
    }
}
