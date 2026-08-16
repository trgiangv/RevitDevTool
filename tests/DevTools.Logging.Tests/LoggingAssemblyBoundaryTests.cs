namespace DevTools.Logging.Tests;

public sealed class LoggingAssemblyBoundaryTests
{
    [Fact]
    public void Logging_assembly_forbids_ui_scintilla_and_wpf()
    {
        var references = typeof(LoggingExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("DevTools.UI", references);
        Assert.DoesNotContain("ZLogger.Scintilla", references);
        Assert.DoesNotContain("Scintilla5", references);
        Assert.DoesNotContain("ScintillaNET", references);
    }

    [Fact]
    public void Logging_csproj_has_no_wpf_or_scintilla()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.Logging",
            "DevTools.Logging.csproj"));

        Assert.DoesNotContain("UseWPF", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ZLogger.Scintilla", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scintilla5", csproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Logging_source_has_no_wpf_ui_or_scintilla()
    {
        var loggingDir = Path.Combine(RepositoryRoot.Find(), "source", "DevTools.Logging");
        var sources = Directory.GetFiles(loggingDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);

        string[] forbidden =
        [
            "PresentationFramework",
            "DevTools.UI",
            "ZLogger.Scintilla",
            "Scintilla5",
            "System.Windows",
        ];

        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Presentation_csproj_references_ZLogger_Scintilla()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.Presentation",
            "DevTools.Presentation.csproj"));

        Assert.Contains("ZLogger.Scintilla", csproj, StringComparison.Ordinal);
        Assert.Contains("Scintilla5.NET", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void NUnit_Host_still_project_references_Logging_not_ZLogger_package()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.NUnit.Host",
            "DevTools.NUnit.Host.csproj"));

        Assert.Contains("DevTools.Logging.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("ZLogger", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ZLogger.Scintilla", csproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Host_source_calls_AddLoggingProvider_then_AddMonitorLogging()
    {
        var root = RepositoryRoot.Find();
        string[] hosts =
        [
            Path.Combine(root, "source", "RevitDevTool", "Composition", "RevitServiceRegistration.cs"),
            Path.Combine(root, "source", "AcadDevTool", "Composition", "AcadServiceRegistration.cs"),
        ];

        foreach (var path in hosts)
        {
            var text = File.ReadAllText(path);
            var providerIndex = text.IndexOf("AddLoggingProvider()", StringComparison.Ordinal);
            var monitorIndex = text.IndexOf("AddMonitorLogging(", StringComparison.Ordinal);
            Assert.True(providerIndex >= 0, $"{path} must call AddLoggingProvider().");
            Assert.True(monitorIndex >= 0, $"{path} must call AddMonitorLogging.");
            Assert.True(providerIndex < monitorIndex, $"{path} must call AddLoggingProvider before AddMonitorLogging.");
        }
    }
}
