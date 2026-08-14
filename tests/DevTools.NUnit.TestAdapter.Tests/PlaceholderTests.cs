namespace DevTools.NUnit.TestAdapter.Tests;

public class TestAdapterLayoutTests
{
    [Fact]
    public void TestAdapter_is_the_only_public_nunit_package_project()
    {
        var repositoryRoot = FindRepositoryRoot();
        var adapterProject = File.ReadAllText(Path.Combine(repositoryRoot, "source", "DevTools.NUnit.TestAdapter", "DevTools.NUnit.TestAdapter.csproj"));

        Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "source", "DevTools.NUnit.Client")));
        Assert.Contains("<PackageId>DevTools.NUnit.TestAdapter</PackageId>", adapterProject);
        Assert.Contains("<IsPackable>true</IsPackable>", adapterProject);
        Assert.Contains("DevTools.NUnit.TestAdapter.targets", adapterProject);
        Assert.Contains("ILRepack", adapterProject);
        Assert.Contains("RepackBinariesExcludes", adapterProject);
        Assert.Contains("ILRepackInternalize", adapterProject);
        Assert.Contains("System.Text.Json", adapterProject);
        Assert.DoesNotContain("Newtonsoft.Json", adapterProject);
        Assert.DoesNotContain("VersionOverride", adapterProject);
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "props", "ILRepack.targets")));
        Assert.False(File.Exists(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "ILRepack.targets")));
        var sharedRepack = File.ReadAllText(Path.Combine(repositoryRoot, "props", "ILRepack.targets"));
        var revitTargets = File.ReadAllText(Path.Combine(repositoryRoot, "props", "Revit.targets"));
        var acadTargets = File.ReadAllText(Path.Combine(repositoryRoot, "props", "AutoCad.targets"));
        Assert.Contains("Target Name=\"RepackAddinFiles\"", sharedRepack, StringComparison.Ordinal);
        Assert.Contains("JetBrains.Annotations.dll", sharedRepack, StringComparison.Ordinal);
        Assert.DoesNotContain("RepackAddinFiles", revitTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("RepackAddinFiles", acadTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepack.exe", revitTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepack.exe", acadTargets, StringComparison.Ordinal);
        var extraRepackTargets = Directory
            .EnumerateFiles(repositoryRoot, "ILRepack.targets", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Where(path => !path.Equals("props/ILRepack.targets", StringComparison.Ordinal)
                           && path.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) < 0
                           && path.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();
        Assert.Empty(extraRepackTargets);
        Assert.DoesNotContain("TargetsForTfmSpecificContentInPackage", adapterProject);
    }

    [Fact]
    public void Source_sample_uses_msbuild_props_and_adapter_reference()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sampleProject = File.ReadAllText(Path.Combine(repositoryRoot, "samples", "DevTools.NUnit.VSTest.SampleTests", "DevTools.NUnit.VSTest.SampleTests.csproj"));
        var targets = File.ReadAllText(Path.Combine(repositoryRoot, "source", "DevTools.NUnit.TestAdapter", "build", "DevTools.NUnit.TestAdapter.targets"));
        var props = File.ReadAllText(Path.Combine(repositoryRoot, "source", "DevTools.NUnit.TestAdapter", "build", "DevTools.NUnit.TestAdapter.props"));

        Assert.Contains("DevTools.NUnit.TestAdapter.targets", sampleProject);
        Assert.Contains("<HostName>Revit</HostName>", sampleProject);
        Assert.Contains("<HostVersion>$(RevitVersion)</HostVersion>", sampleProject);
        Assert.Contains("<HostLaunch>false</HostLaunch>", sampleProject);
        Assert.Contains("<HostTimeout>60</HostTimeout>", sampleProject);
        Assert.Contains("<HostLaunchTimeout>360</HostLaunchTimeout>", sampleProject);
        Assert.Contains("GenerateDevToolsNUnitRunSettings", targets);
        Assert.Contains("$(TargetDir)DevTools.NUnit.runsettings", targets);
        Assert.Contains("TryApplyFromAssembly", File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "DevToolsNUnitDiscoverer.cs")));
        Assert.DoesNotContain("GenerateDevToolsNUnitAdapterBindingRedirects", targets);
        Assert.Contains("$(DevToolsNUnitGeneratedRunSettingsPath)", targets);
        Assert.Contains("obj\\$(_DevToolsNUnitConfig)\\DevTools.NUnit.generated.runsettings", props);
        Assert.Contains("&lt;DevToolsNUnit&gt;", targets);
        Assert.Contains("must declare &lt;HostVersion&gt;", targets);

        Assert.Contains("<RunSettingsFilePath", props);
        Assert.DoesNotContain("$(RevitVersion)", props);
        Assert.DoesNotContain("TestAdapterLoadingStrategy", targets);
        Assert.DoesNotContain("TestAdaptersPaths", targets);
        Assert.DoesNotContain("DevTools.NUnit.Client", sampleProject);
        Assert.DoesNotContain("DevTools.NUnit.runsettings", sampleProject);
        Assert.DoesNotContain("AssemblyInfo.cs", Directory.GetFiles(
            Path.Combine(repositoryRoot, "samples", "DevTools.NUnit.VSTest.SampleTests")).Select(Path.GetFileName));
    }

    [Fact]
    public void Discoverer_collects_tests_locally_without_host_runner()
    {
        var repositoryRoot = FindRepositoryRoot();
        var discoverer = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "DevToolsNUnitDiscoverer.cs"));

        Assert.Contains("LocalNUnitTestDiscoverer.Discover", discoverer);
        Assert.Contains("TryApplyFromAssembly", discoverer);
        Assert.DoesNotContain("client.Discover", discoverer);
        Assert.DoesNotContain("ProcessRunnerClient", discoverer);
        Assert.True(File.Exists(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.Core",
            "Discovery",
            "NUnitMetadataDiscoverer.cs")));
        Assert.True(File.Exists(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "LocalNUnitTestDiscoverer.cs")));
    }

    [Fact]
    public void Vstest_case_uses_nunit_full_name_as_fqn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mapper = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "VSTestCaseMapper.cs"));

        Assert.Contains("new TestCase(test.FullName, ExecutorUri, test.Source)", mapper);
        Assert.DoesNotContain("VsTestCaseNaming", mapper);
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
