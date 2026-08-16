using System.Text.Json;
using DevTools.Hosting;
using DevTools.Mcp.Server.Contracts;

namespace DevTools.Mcp.Tests;

public sealed class HostLaunchCompositionBoundaryTests
{
    [Fact]
    public void Mcp_Server_forbids_FileMetadata_Revit_and_Hosting_Revit()
    {
        var csproj = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "source", "DevTools.Mcp.Server", "DevTools.Mcp.Server.csproj"));
        Assert.DoesNotContain("FileMetadata.Revit", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Hosting.Revit", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Hosting.Acad", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void Daemon_Hosting_folder_has_no_Revit_type_files()
    {
        var hostingDir = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Daemon", "Hosting");
        var revitFiles = Directory.GetFiles(hostingDir, "Revit*", SearchOption.AllDirectories);
        Assert.Empty(revitFiles);
    }

    [Fact]
    public void Add_ins_do_not_call_launch_extensions()
    {
        var root = FindRepositoryRoot();
        foreach (var project in new[] { "RevitDevTool", "AcadDevTool" })
        {
            var sources = Directory.GetFiles(
                Path.Combine(root, "source", project), "*.cs", SearchOption.AllDirectories);
            foreach (var path in sources)
            {
                var text = File.ReadAllText(path);
                Assert.DoesNotContain("AddRevitLaunch", text, StringComparison.Ordinal);
                Assert.DoesNotContain("AddAutocadFamilyLaunch", text, StringComparison.Ordinal);
                Assert.DoesNotContain("AddHostLaunchCore", text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void LaunchHostTool_uses_HostLaunchWait_and_culture_language()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "source", "DevTools.Mcp.Server", "Tools", "LaunchHostTool.cs"));
        Assert.Contains("HostLaunchWait.UntilAsync", source, StringComparison.Ordinal);
        Assert.Contains("en-US", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Revit ENU", source, StringComparison.Ordinal);
        Assert.DoesNotContain("default 'ENU'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WaitOutcome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task<bool> WaitForInstanceConnectionAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LaunchHostResult_echoes_culture_not_ENU()
    {
        var payload = new LaunchHostResult(
            HostApp.Revit,
            1,
            "2025",
            @"C:\Revit.exe",
            "/language ENU",
            "en-US",
            true);
        var json = JsonSerializer.Serialize(payload);
        Assert.Contains("\"languageCode\":\"en-US\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"languageCode\":\"ENU\"", json, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
