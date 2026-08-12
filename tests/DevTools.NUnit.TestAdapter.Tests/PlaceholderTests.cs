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
        Assert.Contains("System.Text.Json", adapterProject);
        Assert.DoesNotContain("Newtonsoft.Json", adapterProject);
        Assert.DoesNotContain("VersionOverride", adapterProject);
        Assert.True(File.Exists(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.TestAdapter",
            "ILRepack.targets")));
        Assert.DoesNotContain("TargetsForTfmSpecificContentInPackage", adapterProject);
    }

    [Fact]
    public void Source_sample_uses_msbuild_props_and_adapter_reference()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sampleProject = File.ReadAllText(Path.Combine(repositoryRoot, "samples", "DevTools.NUnit.SampleTests", "DevTools.NUnit.SampleTests.csproj"));
        var targets = File.ReadAllText(Path.Combine(repositoryRoot, "source", "DevTools.NUnit.TestAdapter", "build", "DevTools.NUnit.TestAdapter.targets"));
        var props = File.ReadAllText(Path.Combine(repositoryRoot, "source", "DevTools.NUnit.TestAdapter", "build", "DevTools.NUnit.TestAdapter.props"));

        Assert.Contains("DevTools.NUnit.TestAdapter.targets", sampleProject);
        Assert.Contains("<HostName>Revit</HostName>", sampleProject);
        Assert.Contains("<HostVersion>$(RevitVersion)</HostVersion>", sampleProject);
        Assert.Contains("<HostLaunch>false</HostLaunch>", sampleProject);
        Assert.Contains("<HostTimeout>60</HostTimeout>", sampleProject);
        Assert.Contains("<HostLaunchTimeout>360</HostLaunchTimeout>", sampleProject);
        Assert.Contains("GenerateDevToolsNUnitRunSettings", targets);
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
            Path.Combine(repositoryRoot, "samples", "DevTools.NUnit.SampleTests")).Select(Path.GetFileName));
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
        Assert.DoesNotContain("client.Discover", discoverer);
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
