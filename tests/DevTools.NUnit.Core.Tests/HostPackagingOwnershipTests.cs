namespace DevTools.NUnit.Core.Tests;

public sealed class HostPackagingOwnershipTests
{
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate RevitDevTool.slnx from the test base directory.");
    }

    [Theory]
    [InlineData("source/RevitDevTool/RevitDevTool.csproj")]
    [InlineData("source/ACadDevTool/ACadDevTool.csproj")]
    public void Host_projects_import_shared_nunit_packaging_targets(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(projectPath), $"Missing project: {projectPath}");

        var projectText = File.ReadAllText(projectPath);
        Assert.DoesNotContain("NUnitCoreSatelliteName", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreNUnitCoreSatellites", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("nunit-core-satellites", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.Json.dll", projectText, StringComparison.Ordinal);
        Assert.Contains("NUnitHostPackaging.targets", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_packaging_targets_own_copy_and_assert_flow()
    {
        var root = FindRepoRoot();
        var packagingTargets = Path.Combine(root, "source", "DevTools.NUnit.Host", "build", "NUnitHostPackaging.targets");
        var mergedProps = Path.Combine(root, "source", "DevTools.NUnit.Host", "build", "NUnitHostMergedAssemblies.props");
        var payloadTargets = Path.Combine(root, "source", "DevTools.NUnit.Runtime", "build", "NUnitRuntimePayload.targets");

        Assert.True(File.Exists(packagingTargets));
        Assert.True(File.Exists(mergedProps));
        Assert.True(File.Exists(payloadTargets));

        var packagingText = File.ReadAllText(packagingTargets);
        Assert.Contains("CopyNUnitRuntimeBootstrap", packagingText, StringComparison.Ordinal);
        Assert.Contains("AssertNUnitHostDependencyOwnership", packagingText, StringComparison.Ordinal);
        Assert.Contains("GetNUnitRuntimePayload", packagingText, StringComparison.Ordinal);
        Assert.Contains("$(TargetDir)NUnitRuntime", packagingText, StringComparison.Ordinal);
        Assert.DoesNotContain("$(OutputPath)NUnitRuntime", packagingText, StringComparison.Ordinal);

        var payloadText = File.ReadAllText(payloadTargets);
        Assert.Contains("PrepareNUnitRuntimePayload", payloadText, StringComparison.Ordinal);
        Assert.Contains("NUnitHostOwnedAssembly", payloadText, StringComparison.Ordinal);
        Assert.Contains("'$(TargetDir)' != ''", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFullPath('$(TargetDir)NUnitRuntimePayload", payloadText, StringComparison.Ordinal);
    }
}

