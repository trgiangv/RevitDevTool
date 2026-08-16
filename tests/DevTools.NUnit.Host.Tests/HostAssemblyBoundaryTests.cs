using DevTools.NUnit.Host;

namespace DevTools.NUnit.Host.Tests;

public sealed class HostAssemblyBoundaryTests
{
    [Fact]
    public void Host_keeps_logging_project_reference_and_forbids_ui_directly()
    {
        // NUnit.Host must keep a DevTools.Logging ProjectReference (headless ZLogger after H).
        // Host code currently consumes IHostAppInfo from Hosting and ILogger<T> from MEL, so
        // GetReferencedAssemblies omits unused Logging. Direct Presentation/UI/Scintilla refs
        // are forbidden now. Transitive WPF via Utilities/Logging is expected until E+H —
        // this is not a full UI-free claim.
        var csproj = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.NUnit.Host",
            "DevTools.NUnit.Host.csproj"));
        Assert.Contains("DevTools.Logging.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("DevTools.Hosting.csproj", csproj, StringComparison.Ordinal);

        var references = typeof(NUnitRequestHandler).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("DevTools.Hosting", references);
        Assert.DoesNotContain("DevTools.Presentation", references);
        Assert.DoesNotContain("DevTools.UI", references);
        Assert.DoesNotContain("ZLogger.Scintilla", references);
        Assert.DoesNotContain("PresentationFramework", references);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output.");
    }
}
