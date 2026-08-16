using DevTools.Telemetry;

namespace DevTools.Telemetry.Tests;

public sealed class TelemetryAssemblyBoundaryTests
{
    [Fact]
    public void Telemetry_assembly_forbids_settings_ui_logging_and_utilities()
    {
        var references = typeof(ITelemetry).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("DevTools.Settings", references);
        Assert.DoesNotContain("DevTools.UI", references);
        Assert.DoesNotContain("DevTools.Logging", references);
        Assert.DoesNotContain("DevTools.Utilities", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.Contains("DevTools.Hosting", references);
        Assert.Contains("Sentry", references);
    }

    [Fact]
    public void Telemetry_csproj_references_hosting_and_sentry_only()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.Telemetry",
            "DevTools.Telemetry.csproj"));

        Assert.Contains("DevTools.Hosting.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("Sentry", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Settings", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DevTools.Logging", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DevTools.Utilities", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DevTools.UI", csproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Telemetry_source_has_no_settings_ui_logging_or_utilities()
    {
        var telemetryDir = Path.Combine(RepositoryRoot.Find(), "source", "DevTools.Telemetry");
        var sources = Directory.GetFiles(telemetryDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);

        string[] forbidden =
        [
            "ISettingsService",
            "DevTools.Settings",
            "DevTools.UI",
            "DevTools.Logging",
            "DevTools.Utilities",
        ];

        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                Assert.DoesNotContain(token, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Host_source_supplies_enable_and_dsn()
    {
        var root = RepositoryRoot.Find();
        string[] hosts =
        [
            Path.Combine(root, "source", "RevitDevTool", "Host.cs"),
            Path.Combine(root, "source", "AcadDevTool", "Host.cs"),
        ];

        foreach (var path in hosts)
        {
            var text = File.ReadAllText(path);
            Assert.Contains("AddDevToolsTelemetry(", text, StringComparison.Ordinal);
            Assert.Contains("EnableTelemetry", text, StringComparison.Ordinal);
            Assert.Contains("BuiltInSentryDsn", text, StringComparison.Ordinal);
            Assert.DoesNotContain("AddDevToolsTelemetry()", text, StringComparison.Ordinal);
        }
    }
}
