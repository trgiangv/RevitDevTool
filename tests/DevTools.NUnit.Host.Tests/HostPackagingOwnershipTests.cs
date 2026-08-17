namespace DevTools.NUnit.Host.Tests;

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
    [InlineData("source/AcadDevTool/AcadDevTool.csproj")]
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

    [Theory]
    [InlineData("source/RevitDevTool/RevitDevTool.csproj")]
    [InlineData("source/AcadDevTool/AcadDevTool.csproj")]
    public void Host_projects_do_not_restate_ilrepack_driver_defaults(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(projectPath), $"Missing project: {projectPath}");

        var projectText = File.ReadAllText(projectPath);
        Assert.Contains("<ILRepackable>true</ILRepackable>", projectText, StringComparison.Ordinal);
        Assert.Contains("RepackBinariesExcludes", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackUnion", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackInternalize", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackILLink", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackParallel", projectText, StringComparison.Ordinal);
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
        Assert.Contains("TestingSharedAssembly", packagingText, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions", packagingText, StringComparison.Ordinal);
        Assert.Contains("DevTools.NUnit.Transport", packagingText, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Core", packagingText, StringComparison.Ordinal);
        Assert.Contains("DevTools.NUnit.Runner.exe", packagingText, StringComparison.Ordinal);

        var mergedText = File.ReadAllText(mergedProps);
        Assert.Contains("TestingSharedAssembly Include=\"DevTools.Testing.Abstractions\"", mergedText, StringComparison.Ordinal);
        Assert.Contains("NUnitHostOwnedAssembly Include=\"DevTools.NUnit.Transport\"", mergedText, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Core", mergedText, StringComparison.Ordinal);

        var payloadText = File.ReadAllText(payloadTargets);
        Assert.Contains("PrepareNUnitRuntimePayload", payloadText, StringComparison.Ordinal);
        Assert.Contains("NUnitHostOwnedAssembly", payloadText, StringComparison.Ordinal);
        Assert.Contains("TestingSharedAssembly", payloadText, StringComparison.Ordinal);
        Assert.Contains("'$(TargetDir)' != ''", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFullPath('$(TargetDir)NUnitRuntimePayload", payloadText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("source/RevitDevTool/RevitDevTool.csproj")]
    [InlineData("source/AcadDevTool/AcadDevTool.csproj")]
    public void Host_projects_keep_testing_abstractions_loose(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        var projectText = File.ReadAllText(projectPath);
        Assert.Contains("DevTools.Testing.Abstractions.dll", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void Packed_host_output_has_one_abstractions_dll_and_private_nunit_runtime()
    {
        var outputDir = FindPackedHostOutputDir();
        Assert.True(
            outputDir is not null,
            "Build the host with ILRepack first: dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false");

        var hostDll = Path.Combine(outputDir!, "RevitDevTool.dll");
        var abstractions = Path.Combine(outputDir, "DevTools.Testing.Abstractions.dll");
        var nunitTransport = Path.Combine(outputDir, "DevTools.NUnit.Transport.dll");
        var nunitProvider = Path.Combine(outputDir, "DevTools.NUnit.Provider.dll");
        var nunitCore = Path.Combine(outputDir, "DevTools.NUnit.Core.dll");
        var runtime = Path.Combine(outputDir, "NUnitRuntime", "DevTools.NUnit.Runtime.dll");
        var framework = Path.Combine(outputDir, "NUnitRuntime", "nunit.framework.dll");

        Assert.True(File.Exists(hostDll), hostDll);
        Assert.True(File.Exists(abstractions), abstractions);
        Assert.True(File.Exists(nunitTransport), nunitTransport);
        Assert.True(File.Exists(runtime), runtime);
        Assert.True(File.Exists(framework), framework);
        Assert.False(File.Exists(nunitCore), nunitCore);
        Assert.False(File.Exists(nunitProvider), nunitProvider);
        Assert.False(File.Exists(Path.Combine(outputDir, "NUnitRuntime", "DevTools.NUnit.Core.dll")));
        Assert.False(File.Exists(Path.Combine(outputDir, "NUnitRuntime", "DevTools.NUnit.Provider.dll")));

        var abstractionsCopies = Directory.GetFiles(
                outputDir,
                "DevTools.Testing.Abstractions.dll",
                SearchOption.TopDirectoryOnly);
        Assert.True(
            abstractionsCopies.Length == 1,
            "Duplicate Testing.Abstractions copies:" + Environment.NewLine + string.Join(Environment.NewLine, abstractionsCopies));

        Assert.False(File.Exists(Path.Combine(outputDir, "DevTools.NUnit.Runtime.dll")));
        Assert.False(File.Exists(Path.Combine(outputDir, "nunit.framework.dll")));
        Assert.False(File.Exists(Path.Combine(outputDir, "DevTools.NUnit.Runner.exe")));
        Assert.False(File.Exists(Path.Combine(outputDir, "DevTools.Testing.Host.dll")));
        Assert.False(File.Exists(Path.Combine(outputDir, "DevTools.Testing.Transport.dll")));
    }

    private static string? FindPackedHostOutputDir()
    {
        var root = FindRepoRoot();
        var preferred = Path.Combine(root, "source", "RevitDevTool", "bin", "Debug.Autodesk.2025");
        if (LooksPacked(preferred))
            return preferred;

        var bin = Path.Combine(root, "source", "RevitDevTool", "bin");
        if (!Directory.Exists(bin))
            return null;

        return Directory.GetDirectories(bin, "*", SearchOption.AllDirectories)
            .Where(LooksPacked)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool LooksPacked(string directory) =>
        File.Exists(Path.Combine(directory, "RevitDevTool.dll"))
        && File.Exists(Path.Combine(directory, "NUnitRuntime", "DevTools.NUnit.Runtime.dll"));
}
