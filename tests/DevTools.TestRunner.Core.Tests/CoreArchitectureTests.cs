namespace DevTools.TestRunner.Core.Tests;

public sealed class CoreArchitectureTests
{
    [Fact]
    public void Core_has_no_provider_symbols_or_protocols()
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "source", "DevTools.TestRunner.Core"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText);

        Assert.DoesNotContain(files, text => text.Contains("NUnit", StringComparison.Ordinal)
            || text.Contains("nunit/", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nunit.framework", StringComparison.OrdinalIgnoreCase)
            || text.Contains("IRunnerCommandModule", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
