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
    public void Host_projects_import_the_layout_assertions_and_no_nunit_packaging(string relativeProjectPath)
    {
        var projectPath = Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(projectPath), $"Missing project: {projectPath}");

        var projectText = File.ReadAllText(projectPath);
        Assert.DoesNotContain("NUnitCoreSatelliteName", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreNUnitCoreSatellites", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("nunit-core-satellites", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Text.Json.dll", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitHostPackaging.targets", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Runtime", projectText, StringComparison.Ordinal);
        Assert.Contains("HostLayout.targets", projectText, StringComparison.Ordinal);
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
    public void Layout_assertions_run_after_the_merge()
    {
        var root = FindRepoRoot();
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.NUnit.Runtime", "build", "NUnitHostPackaging.targets")));
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.NUnit.Runtime", "build", "NUnitRuntimePayload.targets")));

        var layout = File.ReadAllText(Path.Combine(root, "props", "HostLayout.targets"));
        Assert.Contains("AfterTargets=\"RepackAddinFiles\"", layout, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions.dll must stay loose", layout, StringComparison.Ordinal);
        Assert.Contains("must stay merged into the add-in", layout, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Runtime_merges_its_own_dependencies_on_net48_only_and_keeps_the_framework_out()
    {
        var project = File.ReadAllText(Path.Combine(FindRepoRoot(), "source", "DevTools.NUnit.Runtime", "DevTools.NUnit.Runtime.csproj"));

        Assert.Contains("<ILRepackInternalize>true</ILRepackInternalize>", project, StringComparison.Ordinal);
        Assert.Contains("<RepackBinariesKeep>nunit.framework.dll;nunit.framework.legacy.dll;", project, StringComparison.Ordinal);
        Assert.Contains("Include=\"System.Reflection.Metadata\" Condition=\"'$(TargetFramework)' == 'net48'\"", project, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("source/RevitDevTool/RevitDevTool.csproj")]
    [DataRow("source/AcadDevTool/AcadDevTool.csproj")]
    public void Host_projects_do_not_package_a_tunit_runtime(string relativeProjectPath)
    {
        var root = FindRepoRoot();
        var projectText = File.ReadAllText(Path.Combine(root, relativeProjectPath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("TUnit", projectText, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.TUnit.Runtime", "build", "TUnitHostPackaging.targets")));
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.TUnit.Runtime", "build", "TUnitRuntimePayload.targets")));
    }

    [TestMethod]
    [DataRow("source/RevitDevTool/RevitDevTool.csproj")]
    [DataRow("source/AcadDevTool/AcadDevTool.csproj")]
    public void Host_projects_do_not_package_an_mstest_runtime(string relativeProjectPath)
    {
        var root = FindRepoRoot();
        var projectText = File.ReadAllText(Path.Combine(root, relativeProjectPath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("MSTest", projectText, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.MSTest.Runtime", "build", "MSTestHostPackaging.targets")));
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.MSTest.Runtime", "build", "MSTestRuntimePayload.targets")));
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
    public void Packed_host_output_has_one_abstractions_dll_and_no_framework_runtime()
    {
        var outputDir = FindPackedHostOutputDir();
        if (outputDir is null)
        {
            Assert.Inconclusive(
                "Packed (ILRepack) host output not found. This fact needs a merged host. Build: dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAddin=false");
        }

        var hostDll = Path.Combine(outputDir!, "RevitDevTool.dll");
        var abstractions = Path.Combine(outputDir, "DevTools.Testing.Abstractions.dll");
        var nunitProvider = Path.Combine(outputDir, "DevTools.Testing.Discovery.dll");
        var nunitCore = Path.Combine(outputDir, "DevTools.NUnit.Core.dll");

        Assert.IsTrue(File.Exists(hostDll), hostDll);
        Assert.IsTrue(File.Exists(abstractions), abstractions);
        Assert.IsFalse(File.Exists(nunitCore), nunitCore);
        Assert.IsFalse(File.Exists(nunitProvider), nunitProvider);
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.NUnit.Discovery.dll")));

        var abstractionsCopies = Directory.GetFiles(
                outputDir,
                "DevTools.Testing.Abstractions.dll",
                SearchOption.TopDirectoryOnly);
        Assert.IsTrue(
            abstractionsCopies.Length == 1,
            "Duplicate Testing.Abstractions copies:" + Environment.NewLine + string.Join(Environment.NewLine, abstractionsCopies));

        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "MSTest.TestFramework.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.NUnit.Runtime.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "nunit.framework.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.Testing.Host.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.Testing.Transport.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(outputDir, "DevTools.AssemblyIsolation.dll")));
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
        return !File.Exists(Path.Combine(directory, "DevTools.Testing.Host.dll"));
    }
}
