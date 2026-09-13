namespace DevTools.NUnit.Host.Tests;

[TestClass]
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

    [TestMethod]
    [DataRow("source/RevitDevTool/RevitDevTool.csproj")]
    [DataRow("source/AcadDevTool/AcadDevTool.csproj")]
    public void Host_projects_import_shared_nunit_packaging_targets(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(projectPath), $"Missing project: {projectPath}");

        var projectText = File.ReadAllText(projectPath);
        Assert.DoesNotContain("NUnitCoreSatelliteName", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreNUnitCoreSatellites", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("nunit-core-satellites", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.Json.dll", projectText, StringComparison.Ordinal);
        Assert.Contains("NUnitHostPackaging.targets", projectText, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("source/RevitDevTool/RevitDevTool.csproj")]
    [DataRow("source/AcadDevTool/AcadDevTool.csproj")]
    public void Host_projects_do_not_restate_ilrepack_driver_defaults(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(projectPath), $"Missing project: {projectPath}");

        var projectText = File.ReadAllText(projectPath);
        Assert.Contains("<ILRepackable>true</ILRepackable>", projectText, StringComparison.Ordinal);
        Assert.Contains("RepackBinariesExcludes", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackUnion", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackInternalize", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackILLink", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackParallel", projectText, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Shared_packaging_targets_own_copy_and_assert_flow()
    {
        var root = FindRepoRoot();
        var packagingTargets = Path.Combine(root, "source", "DevTools.NUnit.Runtime", "build", "NUnitHostPackaging.targets");
        var payloadTargets = Path.Combine(root, "source", "DevTools.NUnit.Runtime", "build", "NUnitRuntimePayload.targets");

        Assert.IsTrue(File.Exists(packagingTargets));
        Assert.IsTrue(File.Exists(payloadTargets));
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.NUnit.Runtime", "build", "NUnitHostMergedAssemblies.props")));

        var packagingText = File.ReadAllText(packagingTargets);
        Assert.Contains("CopyNUnitRuntimeBootstrap", packagingText, StringComparison.Ordinal);
        Assert.Contains("AssertNUnitRuntimeLayout", packagingText, StringComparison.Ordinal);
        Assert.Contains("GetNUnitRuntimePayload", packagingText, StringComparison.Ordinal);
        Assert.Contains("$(TargetDir)NUnitRuntime", packagingText, StringComparison.Ordinal);
        Assert.DoesNotContain("$(OutputPath)NUnitRuntime", packagingText, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions", packagingText, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Core", packagingText, StringComparison.Ordinal);
        Assert.Contains("DevTools.NUnit.Runner.exe", packagingText, StringComparison.Ordinal);

        var payloadText = File.ReadAllText(payloadTargets);
        Assert.Contains("PrepareNUnitRuntimePayload", payloadText, StringComparison.Ordinal);
        Assert.Contains("DevTools.AssemblyIsolation.dll", payloadText, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions.dll", payloadText, StringComparison.Ordinal);
        Assert.Contains("'$(TargetDir)' != ''", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFullPath('$(TargetDir)NUnitRuntimePayload", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Core", payloadText, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("source/RevitDevTool/RevitDevTool.csproj")]
    [DataRow("source/AcadDevTool/AcadDevTool.csproj")]
    public void Host_projects_keep_testing_abstractions_loose(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        var projectText = File.ReadAllText(projectPath);
        Assert.Contains("DevTools.Testing.Abstractions.dll", projectText, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Packed_host_output_has_one_abstractions_dll_and_private_nunit_runtime()
    {
        var outputDir = FindPackedHostOutputDir();
        if (outputDir is null)
        {
            Assert.Inconclusive(
                "Packed (ILRepack) host output not found. Unpackaged Debug still copies NUnitRuntime; this fact needs a merged host. Build: dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false");
        }

        var hostDll = Path.Combine(outputDir!, "RevitDevTool.dll");
        var abstractions = Path.Combine(outputDir, "DevTools.Testing.Abstractions.dll");
        var nunitProvider = Path.Combine(outputDir, "DevTools.Testing.Discovery.dll");
        var nunitCore = Path.Combine(outputDir, "DevTools.NUnit.Core.dll");
        var runtime = Path.Combine(outputDir, "NUnitRuntime", "DevTools.NUnit.Runtime.dll");
        var framework = Path.Combine(outputDir, "NUnitRuntime", "nunit.framework.dll");

        Assert.IsTrue(File.Exists(hostDll), hostDll);
        Assert.IsTrue(File.Exists(abstractions), abstractions);
        Assert.IsTrue(File.Exists(runtime), runtime);
        Assert.IsTrue(File.Exists(framework), framework);
        Assert.IsFalse(File.Exists(nunitCore), nunitCore);
        Assert.IsFalse(File.Exists(nunitProvider), nunitProvider);
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.NUnit.Discovery.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "NUnitRuntime", "DevTools.NUnit.Core.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "NUnitRuntime", "DevTools.NUnit.Discovery.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "NUnitRuntime", "DevTools.Testing.Discovery.dll")));

        var abstractionsCopies = Directory.GetFiles(
                outputDir,
                "DevTools.Testing.Abstractions.dll",
                SearchOption.TopDirectoryOnly);
        Assert.IsTrue(
            abstractionsCopies.Length == 1,
            "Duplicate Testing.Abstractions copies:" + Environment.NewLine + string.Join(Environment.NewLine, abstractionsCopies));

        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.NUnit.Runtime.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "nunit.framework.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.NUnit.Runner.exe")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.Testing.Host.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.Testing.Transport.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.AssemblyIsolation.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "NUnitRuntime", "DevTools.AssemblyIsolation.dll")));
    }

    private static string? FindPackedHostOutputDir()
    {
        var explicitOutput = Environment.GetEnvironmentVariable("DEVTOOLS_PACKED_HOST_OUTPUT");
        if (!string.IsNullOrWhiteSpace(explicitOutput))
        {
            var normalizedOutput = Path.GetFullPath(explicitOutput);
            return LooksPacked(normalizedOutput) ? normalizedOutput : null;
        }

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

    private static bool LooksPacked(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "RevitDevTool.dll")))
            return false;
        if (!File.Exists(Path.Combine(directory, "NUnitRuntime", "DevTools.NUnit.Runtime.dll")))
            return false;

        // Unpackaged Debug also copies NUnitRuntime. Packed layout internalizes
        // Testing.Host / Transport / Isolation at the host output root.
        if (File.Exists(Path.Combine(directory, "DevTools.Testing.Host.dll")))
            return false;
        if (File.Exists(Path.Combine(directory, "DevTools.NUnit.Runtime.dll")))
            return false;

        return true;
    }
}
