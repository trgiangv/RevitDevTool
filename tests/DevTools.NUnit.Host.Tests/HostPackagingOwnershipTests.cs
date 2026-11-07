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

    private static string ReadProject(string relativeProjectPath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar)));

    [TestMethod]
    [DataRow("source/RevitDevTool/RevitDevTool.csproj")]
    [DataRow("source/AcadDevTool/AcadDevTool.csproj")]
    public void Hosts_name_no_test_framework(string relativeProjectPath)
    {
        var projectText = ReadProject(relativeProjectPath);

        Assert.DoesNotContain("NUnit", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnit", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("MSTest", projectText, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("source/RevitDevTool/RevitDevTool.csproj")]
    [DataRow("source/AcadDevTool/AcadDevTool.csproj")]
    public void Hosts_keep_ilrepack_defaults(string relativeProjectPath)
    {
        var projectText = ReadProject(relativeProjectPath);

        Assert.Contains("<ILRepackable>true</ILRepackable>", projectText, StringComparison.Ordinal);
        Assert.Contains("RepackBinariesExcludes", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackUnion", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackInternalize", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackILLink", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackParallel", projectText, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Layout_checks_run_after_merge()
    {
        var layout = File.ReadAllText(Path.Combine(FindRepoRoot(), "props", "ILRepack.targets"));

        Assert.Contains("<Target Name=\"AssertHostLayout\"", layout, StringComparison.Ordinal);
        Assert.Contains("AfterTargets=\"RepackAddinFiles\"", layout, StringComparison.Ordinal);
        Assert.Contains("('$(UseRevit)' == 'true' OR '$(UseAutoCad)' == 'true')", layout, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions.dll must stay loose", layout, StringComparison.Ordinal);
        Assert.Contains("must stay merged into the add-in", layout, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Runtime_merges_own_dependencies_only()
    {
        var project = File.ReadAllText(Path.Combine(FindRepoRoot(), "source", "DevTools.NUnit.Runtime", "DevTools.NUnit.Runtime.csproj"));

        Assert.Contains("<ILRepackInternalize>true</ILRepackInternalize>", project, StringComparison.Ordinal);
        Assert.Contains("<RepackBinariesKeep>nunit.framework.dll;nunit.framework.legacy.dll;", project, StringComparison.Ordinal);
        Assert.Contains("Include=\"System.Reflection.Metadata\" Condition=\"'$(TargetFramework)' == 'net48'\"", project, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Packed_host_output_has_no_framework_runtime()
    {
        var outputDir = FindPackedHostOutputDir();
        if (outputDir is null)
        {
            Assert.Inconclusive(
                "Packed (ILRepack) host output not found. This fact needs a merged host. Build: dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAddin=false");
        }

        Assert.IsTrue(File.Exists(Path.Combine(outputDir!, "RevitDevTool.dll")));
        Assert.IsTrue(File.Exists(Path.Combine(outputDir, "DevTools.Testing.Abstractions.dll")));

        foreach (var absent in new[]
                 {
                     "DevTools.NUnit.Runtime.dll",
                     "DevTools.TUnit.Runtime.dll",
                     "DevTools.MSTest.Runtime.dll",
                     "nunit.framework.dll",
                     "TUnit.Core.dll",
                     "MSTest.TestFramework.dll",
                     "DevTools.Testing.Transport.dll",
                     "DevTools.AssemblyIsolation.dll",
                 })
        {
            Assert.IsFalse(File.Exists(Path.Combine(outputDir, absent)), absent);
        }
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
