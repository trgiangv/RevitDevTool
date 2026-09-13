using DevTools.NUnit.MTP;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Config;

namespace DevTools.TestAdapter.Tests;

[TestClass]
public sealed class AdapterArchitectureTests
{
    [TestMethod]
    public void TUnit_uses_the_existing_adapter_and_TestRunner_transport()
    {
        var adapterDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var props = File.ReadAllText(Path.Combine(adapterDir, "build", "RevitDevTool.TestAdapter.props"));
        var targets = File.ReadAllText(Path.Combine(adapterDir, "build", "RevitDevTool.TestAdapter.targets"));

        Assert.Contains("'$(TestingFramework)' == 'tunit'", targets, StringComparison.Ordinal);
        Assert.Contains("DevTools.TestAdapter.TestingPlatformBuilderHook", props, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"Microsoft.Testing.Platform.MSBuild\"", props, StringComparison.Ordinal);
        Assert.DoesNotContain("MtpMsBuildPackageVersion", props, StringComparison.Ordinal);
        Assert.DoesNotContain("supports only Revit 2023", props, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(HostVersion)' != '2023'", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<MTPAssembly", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<MTPEntry", props, StringComparison.Ordinal);
        Assert.Contains("DevTools.TUnit.MTP", targets, StringComparison.Ordinal);
        Assert.Contains("DevTools.TUnit.MTP.TUnitMtpBuilderHook", targets, StringComparison.Ordinal);
        Assert.Contains("TestingPlatformBuilderHook Remove=\"6ADF853A-6945-4A06-9A4B-D99BC1DC1094\"", targets, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(adapterDir, "TUnitTestingPlatformBuilderHook.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(adapterDir, "RevitTestHostLauncher.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(adapterDir, "build", "TUnitRevitExecutor.cs")));
        var tunitSample = File.ReadAllText(Path.Combine(
            RepositoryRoot, "samples", "DevTools.TUnit.SampleTests", "DevTools.TUnit.SampleTests.csproj"));
        Assert.DoesNotContain("Microsoft.Testing.Platform.MSBuild", tunitSample, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"RevitDevTool.TestAdapter\"", tunitSample, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.TestAdapter.csproj", tunitSample, StringComparison.Ordinal);
        Assert.DoesNotContain("RevitDevTool.TestAdapter.targets", tunitSample, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TUnit_runtime_is_isolated_from_the_host_and_consumer_output()
    {
        var props = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TestAdapter", "build", "RevitDevTool.TestAdapter.props"));
        var hostProject = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "RevitDevTool", "RevitDevTool.csproj"));
        var packaging = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TUnit.Runtime", "build", "TUnitHostPackaging.targets"));

        Assert.DoesNotContain("ILRepackable", props, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Host", hostProject, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.TUnit.Host", hostProject, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(RevitVersion)' == '2023' OR '$(RevitVersion)' == '2025'", hostProject, StringComparison.Ordinal);
        Assert.Contains("TUnitRuntime\\", packaging, StringComparison.Ordinal);
        Assert.Contains("TUnit.Core.dll must be deployed under TUnitRuntime", packaging, StringComparison.Ordinal);
        Assert.Contains("TUnit.Engine.dll must be deployed under TUnitRuntime", packaging, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Testing.Platform.dll must be deployed under TUnitRuntime", packaging, StringComparison.Ordinal);
        Assert.DoesNotContain("must not ship Microsoft.Testing.Platform", packaging, StringComparison.Ordinal);
        Assert.Contains("must not be copied at the add-in root", packaging, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(RevitVersion)' == '2023' OR '$(RevitVersion)' == '2025'", packaging, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Revit_and_Acad_compositions_register_tunit_host_services()
    {
        var revitComposition = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "RevitDevTool", "Composition", "RevitServiceRegistration.cs"));
        var acadComposition = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "AcadDevTool", "Composition", "AcadServiceRegistration.cs"));

        Assert.Contains("AddTestingHostServices", revitComposition, StringComparison.Ordinal);
        Assert.Contains("AddTestingHostServices", acadComposition, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Revit_TUnit_execution_reuses_the_generic_testing_run_handler()
    {
        var root = Path.Combine(RepositoryRoot, "source", "RevitDevTool");
        var composition = File.ReadAllText(Path.Combine(root, "Composition", "RevitServiceRegistration.cs"));
        var handler = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.Testing.Host", "MarshaledTestRequestHandler.cs"));
        var genericHosting = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.Testing.Host", "TestingHostingExtensions.cs"));

        Assert.Contains("AddTestingHostServices", composition, StringComparison.Ordinal);
        Assert.Contains("AddTUnitHostServices", genericHosting, StringComparison.Ordinal);
        Assert.Contains("TestingProviderRegistry", genericHosting, StringComparison.Ordinal);
        Assert.DoesNotContain("REVIT2023 || REVIT2025", composition, StringComparison.Ordinal);
        Assert.Contains("_hostContext.ExecuteAsync", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("GetResult(), ct)", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("RevitTestExecutionDispatcher", composition, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(root, "Testing", "RevitTestHostApplicationLauncher.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(root, "Testing", "RevitTestExecutionDispatcher.cs")));
    }

    [TestMethod]
    public void TUnit_in_host_uses_engine_instead_of_nested_mtp()
    {
        var runtimeDir = Path.Combine(RepositoryRoot, "source", "DevTools.TUnit.Runtime");
        var session = File.ReadAllText(Path.Combine(runtimeDir, "TUnitRuntimeSession.cs"));
        var host = File.ReadAllText(Path.Combine(runtimeDir, "TUnitEngineHost.cs"));
        var catalog = File.ReadAllText(Path.Combine(runtimeDir, "TUnitCatalog.cs"));
        var identity = File.ReadAllText(Path.Combine(runtimeDir, "TUnitTestIdentity.cs"));
        var project = File.ReadAllText(Path.Combine(runtimeDir, "DevTools.TUnit.Runtime.csproj"));
        var discoverer = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TUnit.MTP", "TUnitTestDiscoverer.cs"));
        var mtpProject = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TUnit.MTP", "DevTools.TUnit.MTP.csproj"));

        Assert.DoesNotContain("TestApplication", session, StringComparison.Ordinal);
        Assert.DoesNotContain("AddTUnit()", session, StringComparison.Ordinal);
        Assert.DoesNotContain("AddTUnit()", host, StringComparison.Ordinal);
        Assert.DoesNotContain("TestApplication", host, StringComparison.Ordinal);
        Assert.Contains("TUnitEngineHost.Run(_testAssembly", session, StringComparison.Ordinal);
        Assert.Contains("_executionGate", session, StringComparison.Ordinal);
        Assert.Contains("_runControl", session, StringComparison.Ordinal);
        Assert.Contains("ExecuteRequestAsync", host, StringComparison.Ordinal);
        Assert.Contains("TestNodeUidListFilter", host, StringComparison.Ordinal);
        Assert.Contains("SourceRegistrar.IsEnabled", host, StringComparison.Ordinal);
        Assert.Contains("TUnitSourceCatalog.Retain", host, StringComparison.Ordinal);
        Assert.Contains("TUnitSourceCatalog.Retain", catalog, StringComparison.Ordinal);
        Assert.Contains("SynchronizationContext.SetSynchronizationContext(null)", host, StringComparison.Ordinal);
        Assert.Contains("TUnit.Engine", project, StringComparison.Ordinal);
        Assert.Contains("TUnit.Core", project, StringComparison.Ordinal);
        Assert.DoesNotContain("VersionOverride=\"9.0.0\"", project, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"System.Text.Json\"", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference Include=\"Microsoft.Testing.Platform\"", project, StringComparison.Ordinal);
        Assert.Contains("Sources.TestEntries", catalog, StringComparison.Ordinal);
        Assert.Contains("GetFilterData", catalog, StringComparison.Ordinal);
        Assert.Contains("TUnitExpansion.Expand", catalog, StringComparison.Ordinal);
        Assert.Contains("TUnitCatalog.Discover", discoverer, StringComparison.Ordinal);
        Assert.Contains("TUnitSourceCatalog.cs", mtpProject, StringComparison.Ordinal);
        Assert.Contains("TUnitMetadataNames.cs", mtpProject, StringComparison.Ordinal);
        Assert.Contains("ParameterTypeFullNames", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitUidSelection", mtpProject, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitTestRunMapper", mtpProject, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(runtimeDir, "TUnitUidSelection.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(runtimeDir, "TUnitTestRunMapper.cs")));
        Assert.DoesNotContain("<PackageReference Include=\"Microsoft.Testing.Platform\"", mtpProject, StringComparison.Ordinal);
        Assert.DoesNotContain("TestingDiscoveryOptions", discoverer, StringComparison.Ordinal);
        Assert.Contains("InheritanceDepth", identity, StringComparison.Ordinal);
        Assert.Contains("_Deferred", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitAot", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitAot", session, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(runtimeDir, "TUnitExecutor.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(runtimeDir, "TUnitHooks.cs")));
        Assert.Contains("TestRunTraceScope", host, StringComparison.Ordinal);
        Assert.Contains("TUnitEngineMessageBus(traceScope)", host, StringComparison.Ordinal);
        Assert.Contains("TestRunTraceScope", File.ReadAllText(Path.Combine(runtimeDir, "TUnitEnginePlatform.cs")), StringComparison.Ordinal);
        Assert.Contains("TestRunTraceScope.Merge", File.ReadAllText(Path.Combine(runtimeDir, "TUnitEngineResults.cs")), StringComparison.Ordinal);
        Assert.Contains("TestEventKinds.Output", session, StringComparison.Ordinal);

        var expansion = File.ReadAllText(Path.Combine(runtimeDir, "TUnitExpansion.cs"));
        Assert.Contains("GetDataRowsAsync", expansion, StringComparison.Ordinal);
        Assert.Contains("RepeatTimes", expansion, StringComparison.Ordinal);
        Assert.Contains("ResolvePropertyDataSources", expansion, StringComparison.Ordinal);
        Assert.Contains("new SourceRow([], 1, 1, null)", expansion, StringComparison.Ordinal);
        Assert.Contains("return [[]];", expansion, StringComparison.Ordinal);
        Assert.Contains("[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]", expansion, StringComparison.Ordinal);
        Assert.Contains("TUnitCombinationIndices", expansion, StringComparison.Ordinal);
        Assert.Contains(
            "CombinationDisplayName(methodRow.DisplayName, classRow.DisplayName)",
            expansion,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitAot", expansion, StringComparison.Ordinal);
    }

    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [TestMethod]
    public void Root_global_json_selects_mtp_and_ricaun_overrides_to_vstest()
    {
        var root = File.ReadAllText(Path.Combine(RepositoryRoot, "global.json"));
        Assert.Contains("\"runner\": \"Microsoft.Testing.Platform\"", root, StringComparison.Ordinal);

        var ricaun = File.ReadAllText(Path.Combine(
            RepositoryRoot, "samples", "ricaun.NUnit.SampleTests", "global.json"));
        Assert.Contains("\"runner\": \"VSTest\"", ricaun, StringComparison.Ordinal);

        foreach (var sample in new[]
                 {
                     "DevTools.NUnit.SampleTests",
                     "DevTools.TUnit.SampleTests",
                     "DevTools.NUnit.Civil3D.SampleTests",
                     "DevTools.TUnit.Civil3D.SampleTests",
                 })
        {
            Assert.IsFalse(
                File.Exists(Path.Combine(RepositoryRoot, "samples", sample, "global.json")),
                $"{sample} inherits the root MTP runner; do not keep a scoped global.json.");
        }
    }

    [TestMethod]
    public void Mtp_DoesNotLocateOrLaunchAutodeskHosts()
    {
        var directory = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var forbidden = new[]
        {
            "HostLocator",
            "IHostSession",
            "ITestSession",
            "Revit.exe",
            "acad.exe",
            "Microsoft.Win32.Registry",
            "EnvDTE",
            "Microsoft.VisualStudio.Interop",
            "GetActiveObject",
            "VisualStudio.DTE",
        };

        var offenders = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, content: File.ReadAllText(path)))
            .SelectMany(file => forbidden
                .Where(pattern => file.content.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepositoryRoot, file.path)} -> {pattern}"))
            .ToList();

        Assert.IsEmpty(offenders);
    }

    [TestMethod]
    public void Mtp_discovery_does_not_invoke_host_runner()
    {
        var framework = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "TestFramework.cs"));

        Assert.DoesNotContain("MetadataTestDiscoverer", framework, StringComparison.Ordinal);
        Assert.Contains("TestingDiscovery.Current", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("session.Discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("_transport.Discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("IDebugSession", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDebugSession", framework, StringComparison.Ordinal);
        Assert.Contains("Debugger.IsAttached", framework, StringComparison.Ordinal);
        Assert.Contains("EnsureSession()", framework, StringComparison.Ordinal);
        Assert.Contains("ApplyDebugParent", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultFrameworkId", File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "TestRunSettingsLoader.cs")), StringComparison.Ordinal);

        var session = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "TestRunSession.cs"));
        Assert.DoesNotContain("Discover(", session, StringComparison.Ordinal);

        var client = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.Testing.Transport",
            "ProcessTestRunnerClient.cs"));
        Assert.DoesNotContain("NUnitRunnerCli.DiscoverCommand", client, StringComparison.Ordinal);
        Assert.DoesNotContain("IReadOnlyList<TestDiscoveredTest> Discover", client, StringComparison.Ordinal);
        Assert.Contains("TestRunnerCli.SerializeExecute", client, StringComparison.Ordinal);
        Assert.Contains("TestRunnerCli.RunCommand", client, StringComparison.Ordinal);
        Assert.DoesNotContain("TestRunnerCli.BuildRunArguments", client, StringComparison.Ordinal);
        var cancel = client[client.IndexOf("public void Cancel(", StringComparison.Ordinal)
            ..client.IndexOf("public void Dispose(", StringComparison.Ordinal)];
        Assert.Contains("TestCancelSignal.TrySignal", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("TryTerminate", cancel, StringComparison.Ordinal);

        var transport = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.Testing.Transport",
            "ITestRunnerTransport.cs"));
        Assert.DoesNotContain("Discover(", transport, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Mtp_uses_the_generic_runner_client()
    {
        var client = Path.Combine(
            RepositoryRoot, "source", "DevTools.Testing.Transport", "ProcessTestRunnerClient.cs");
        Assert.IsTrue(File.Exists(client));
        Assert.IsFalse(File.Exists(Path.Combine(
            RepositoryRoot, "source", "DevTools.TestAdapter", "ProcessRunnerClient.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(
            RepositoryRoot, "source", "DevTools.TestAdapter", "NUnitProcessTransportAdapter.cs")));
        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Client")));

        var mtp = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TestAdapter", "DevTools.TestAdapter.csproj"));
        Assert.DoesNotContain("DevTools.Testing.Discovery", mtp, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Provider", mtp, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference Include=\"..\\DevTools.NUnit.MTP", mtp, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitCollapsedSelection.cs", mtp, StringComparison.Ordinal);
        Assert.Contains("PackNUnitMTP", mtp, StringComparison.Ordinal);
        Assert.Contains("PackTUnitMTP", mtp, StringComparison.Ordinal);
        Assert.DoesNotContain("PackTUnitMTP\"\n            DependsOnTargets=\"BuildTUnitMTPForPack\"\n            Condition=", mtp, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Transport", mtp, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Testing.Mtp", mtp, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitProcessTransportAdapter.cs", mtp, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Local_discovery_is_nunit_explore_tests_not_pe_metadata()
    {
        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.Testing.Discovery")));
        Assert.IsFalse(File.Exists(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "MetadataTestDiscoverer.cs")));

        var framework = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "TestFramework.cs"));
        Assert.Contains("TestingDiscovery.Current", framework, StringComparison.Ordinal);
        Assert.Contains("bridge.RunMapper", framework, StringComparison.Ordinal);
        Assert.Contains("TestingDiscovery.Register", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("HostTestRunMappers.PassThrough", framework, StringComparison.Ordinal);
        Assert.Contains("FoldResults", framework, StringComparison.Ordinal);
        Assert.Contains("ToRunSelection", framework, StringComparison.Ordinal);
        Assert.Contains("devtools.testadapter.discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitCollapsedSelection", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("using DevTools.NUnit.Runtime", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("ToMetadataTypeName", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("LastDotAtDepthZero", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("TrySplitIdentity", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataTestDiscoverer", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("PEReader", framework, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Adapter_hook_is_framework_neutral_and_sibling_hooks_register_discovery()
    {
        var adapterDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var abstractionsDir = Path.Combine(RepositoryRoot, "source", "DevTools.Testing.Abstractions");
        var hook = File.ReadAllText(Path.Combine(adapterDir, "TestingPlatformBuilderHook.cs"));
        var nunitHook = File.ReadAllText(Path.Combine(
            adapterDir, "build", "hooks", "NUnitMtpBuilderHook.cs"));
        var tunitHook = File.ReadAllText(Path.Combine(
            adapterDir, "build", "hooks", "TUnitMtpBuilderHook.cs"));
        var abstractionsSources = Directory.EnumerateFiles(abstractionsDir, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.Contains("RegisterTestFramework", hook, StringComparison.Ordinal);
        Assert.Contains("TestingDiscovery", hook, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitMTP", hook, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitMTP", hook, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly.Load", hook, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(adapterDir, "AdapterBootstrap.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(adapterDir, "HostMtpRegistration.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(adapterDir, "AdapterTestConfig.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(adapterDir, "RuntimeAssemblyResolver.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.MTP", "NUnitMTP.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.TUnit.MTP", "TUnitMTP.cs")));
        Assert.Contains("new NUnitTestDiscoverer()", nunitHook, StringComparison.Ordinal);
        Assert.Contains("new NUnitTestRunMapper()", nunitHook, StringComparison.Ordinal);
        Assert.Contains("new TUnitTestDiscoverer()", tunitHook, StringComparison.Ordinal);
        Assert.Contains("TestingDiscovery.Register", nunitHook, StringComparison.Ordinal);
        Assert.Contains("TestingDiscovery.Register", tunitHook, StringComparison.Ordinal);
        Assert.DoesNotContain("TestingDiscovery.Provider =", nunitHook, StringComparison.Ordinal);
        Assert.DoesNotContain("TestingDiscovery.RunMapper =", tunitHook, StringComparison.Ordinal);
        Assert.Contains("ITestApplicationBuilder", nunitHook, StringComparison.Ordinal);
        Assert.Contains("ITestApplicationBuilder", tunitHook, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnit.MTP", string.Concat(abstractionsSources), StringComparison.Ordinal);
        Assert.DoesNotContain("TUnit.MTP", string.Concat(abstractionsSources), StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(
            RepositoryRoot, "source", "DevTools.NUnit.MTP", "NUnitMtpBuilderHook.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(
            RepositoryRoot, "source", "DevTools.TUnit.MTP", "TUnitMtpBuilderHook.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(abstractionsDir, "Mtp", "HostMTPRegistration.cs")));
    }

    [TestMethod]
    public void NUnit_mtp_owns_authoritative_discovery_and_loads_beside_the_adapter()
    {
        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.MTP");
        var discoverer = File.ReadAllText(Path.Combine(mtpDir, "NUnitTestDiscoverer.cs"));

        Assert.Contains("NUnitTestAssemblyRunner", discoverer, StringComparison.Ordinal);
        Assert.Contains("ExploreTests", discoverer, StringComparison.Ordinal);
        Assert.Contains("test.FullName", discoverer, StringComparison.Ordinal);
        Assert.Contains("ToSourceTypeSegment", discoverer, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(mtpDir, "NUnitTestDiscoverer.RunMapping.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(mtpDir, "NUnitTestRunMapper.cs")));
        Assert.IsTrue(typeof(ITestDiscoverer).IsAssignableFrom(typeof(NUnitTestDiscoverer)));
        Assert.IsFalse(typeof(ITestRunMapper).IsAssignableFrom(typeof(NUnitTestDiscoverer)));
        Assert.IsTrue(typeof(ITestRunMapper).IsAssignableFrom(typeof(NUnitTestRunMapper)));
        Assert.IsFalse(typeof(ITestDiscoverer).IsAssignableFrom(typeof(NUnitTestRunMapper)));
        Assert.DoesNotContain("HostLocator", discoverer, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", discoverer, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"NUnit\"", File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "DevTools.TestAdapter.csproj")), StringComparison.Ordinal);

        var sample = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "samples",
            "DevTools.NUnit.SampleTests",
            "DevTools.NUnit.SampleTests.csproj"));
        var civil = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "samples",
            "DevTools.NUnit.Civil3D.SampleTests",
            "DevTools.NUnit.Civil3D.SampleTests.csproj"));
        Assert.DoesNotContain("DevTools.NUnit.MTP.csproj", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.MTP.csproj", civil, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"NUnit\"", sample, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"RevitDevTool.TestAdapter\"", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.TestAdapter.csproj", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("RevitDevTool.TestAdapter.targets", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Testing.Platform.MSBuild", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Testing.Platform.MSBuild", civil, StringComparison.Ordinal);

        var mtpCsproj = File.ReadAllText(Path.Combine(mtpDir, "DevTools.NUnit.MTP.csproj"));
        Assert.DoesNotContain("DevTools.TestAdapter.csproj", mtpCsproj, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions.csproj", mtpCsproj, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"Microsoft.Testing.Platform\"", mtpCsproj, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Net48_consumer_props_enable_binding_redirects()
    {
        var adapterDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter", "build");
        var props = File.ReadAllText(Path.Combine(adapterDir, "RevitDevTool.TestAdapter.props"));
        var targets = File.ReadAllText(Path.Combine(adapterDir, "RevitDevTool.TestAdapter.targets"));

        Assert.Contains("GenerateBindingRedirectsOutputType", targets, StringComparison.Ordinal);
        Assert.Contains("TargetFrameworkIdentifier", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith('net4')", props, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"System.Runtime.CompilerServices.Unsafe\"", props, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"Microsoft.Bcl.AsyncInterfaces\"", props, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepack", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsNUnitRepack", props, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackable", props, StringComparison.Ordinal);
        Assert.Contains("TestingFramework", props, StringComparison.Ordinal);
        Assert.DoesNotContain("TestingDiscoveryAttributes", props, StringComparison.Ordinal);
        Assert.Contains("<ForceLaunch", props, StringComparison.Ordinal);
        Assert.Contains("<PerTestTimeout", props, StringComparison.Ordinal);
        Assert.Contains("<LaunchTimeout", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<HostLaunch>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<HostTimeout>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<HostLaunchTimeout>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<RequestTimeout", props, StringComparison.Ordinal);
        Assert.Contains("DevTools.TestAdapter.TestingPlatformBuilderHook", props, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Packed_build_files_do_not_depend_on_the_consumer_configuration()
    {
        var adapterDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var buildDir = Path.Combine(adapterDir, "build");
        var packedTargets = File.ReadAllText(Path.Combine(buildDir, "RevitDevTool.TestAdapter.targets"));
        var packed = new[]
        {
            File.ReadAllText(Path.Combine(buildDir, "RevitDevTool.TestAdapter.props")),
            packedTargets,
        };

        Assert.IsFalse(
            File.Exists(Path.Combine(buildDir, "RevitDevTool.TestAdapter.Local.targets")),
            "In-repo samples restore the packed nupkg. There is no checkout-only Local.targets path.");
        Assert.DoesNotContain(
            "Local.targets",
            File.ReadAllText(Path.Combine(adapterDir, "DevTools.TestAdapter.csproj")),
            StringComparison.Ordinal);

        foreach (var file in packed)
        {
            // Configuration names and repo layout are consumer decisions.
            // Do not set RuntimeIdentifier (restore never reads it from this
            // file). Flattening RID output is a package decision.
            Assert.DoesNotContain("$(Configuration", file, StringComparison.Ordinal);
            Assert.DoesNotContain("<RuntimeIdentifier>", file, StringComparison.Ordinal);
            Assert.DoesNotContain("$(RevitVersion)", file, StringComparison.Ordinal);
            Assert.DoesNotContain("$(AutoCadVersion)", file, StringComparison.Ordinal);
            Assert.DoesNotContain("UseRevit", file, StringComparison.Ordinal);
            Assert.DoesNotContain("<ProjectReference", file, StringComparison.Ordinal);
            Assert.DoesNotContain("DevTools.TestAdapter.csproj", file, StringComparison.Ordinal);
            Assert.DoesNotContain(@"..\..\", file, StringComparison.Ordinal);
        }

        Assert.Contains("AppendRuntimeIdentifierToOutputPath", packed[0], StringComparison.Ordinal);

        Assert.DoesNotContain("DevToolsTestAdapterLocal", packedTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("RevitDevTool.TestAdapter.Local.targets", packedTargets, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Adapter_supplies_the_netfx_module_initializer_tunit_needs()
    {
        var adapterDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var buildDir = Path.Combine(adapterDir, "build");
        var props = File.ReadAllText(Path.Combine(buildDir, "RevitDevTool.TestAdapter.props"));
        var targets = File.ReadAllText(Path.Combine(buildDir, "RevitDevTool.TestAdapter.targets"));
        var csproj = File.ReadAllText(Path.Combine(adapterDir, "DevTools.TestAdapter.csproj"));
        var shimPath = Path.Combine(buildDir, "netfx", "ModuleInitializerAttribute.cs");

        // TUnit injects Polyfill from its own .targets, which restore never reads, so the
        // consumer is left with either a missing attribute or a duplicate PackageReference.
        Assert.Contains(
            "<EnableTUnitPolyfills Condition=\"'$(EnableTUnitPolyfills)' == ''\">false</EnableTUnitPolyfills>",
            props,
            StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(shimPath), "The package must ship build/netfx/ModuleInitializerAttribute.cs.");

        var shim = File.ReadAllText(shimPath);
        Assert.Contains("namespace System.Runtime.CompilerServices", shim, StringComparison.Ordinal);
        Assert.Contains("class ModuleInitializerAttribute", shim, StringComparison.Ordinal);
        Assert.Contains("#if NETFRAMEWORK", shim, StringComparison.Ordinal);

        // Only net4x TUnit, and never when the project already declares the type.
        Assert.Contains("Name=\"NetFxModuleInitializer\"", targets, StringComparison.Ordinal);
        Assert.Contains(
            "'$(TestingFramework)' == 'tunit' And $(TargetFramework.StartsWith('net4'))",
            targets,
            StringComparison.Ordinal);
        Assert.Contains("'$(NetFxModuleInitializer)' != 'false'", targets, StringComparison.Ordinal);
        Assert.Contains(
            "Include=\"@(GlobalPackageReference)\" Condition=\"'%(Identity)' == 'Polyfill'\"",
            targets,
            StringComparison.Ordinal);
        Assert.Contains("'@(_DevToolsPolyfill)' == ''", targets, StringComparison.Ordinal);

        // The shim is consumer source, never compiled into the adapter itself.
        Assert.Contains("<Compile Remove=\"build\\**\\*.cs\"/>", csproj, StringComparison.Ordinal);
        Assert.Contains(
            "<None Include=\"build\\netfx\\ModuleInitializerAttribute.cs\" Pack=\"true\" PackagePath=\"build\\netfx\\\"/>",
            csproj,
            StringComparison.Ordinal);
        Assert.Contains(
            "<None Include=\"build\\hooks\\NUnitMtpBuilderHook.cs\" Pack=\"true\" PackagePath=\"build\\hooks\\\"/>",
            csproj,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void Adapter_writes_mtp_testconfig_devtools_section_and_skips_polyfill()
    {
        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var targets = File.ReadAllText(Path.Combine(mtpDir, "build", "RevitDevTool.TestAdapter.targets"));
        var props = File.ReadAllText(Path.Combine(mtpDir, "build", "RevitDevTool.TestAdapter.props"));
        var csproj = File.ReadAllText(Path.Combine(mtpDir, "DevTools.TestAdapter.csproj"));
        var loader = File.ReadAllText(Path.Combine(mtpDir, "TestRunSettingsLoader.cs"));

        Assert.Contains("WriteDiscoveryRefs", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyMTPSibling", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("RevitDevTool.TestAdapter.Local.targets", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsTestAdapterLocal", targets, StringComparison.Ordinal);
        Assert.Contains("<Reference Include=\"$(_MtpSiblingName)\">", targets, StringComparison.Ordinal);
        Assert.Contains("<Private>true</Private>", targets, StringComparison.Ordinal);
        Assert.Contains("<ExternallyResolved>true</ExternallyResolved>", targets, StringComparison.Ordinal);
        Assert.Contains("hooks\\NUnitMtpBuilderHook.cs", targets, StringComparison.Ordinal);
        Assert.Contains("hooks\\TUnitMtpBuilderHook.cs", targets, StringComparison.Ordinal);
        Assert.Contains("<IsTestProject>false</IsTestProject>", csproj, StringComparison.Ordinal);
        Assert.Contains("<IsTestingPlatformApplication>false</IsTestingPlatformApplication>", csproj, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"Microsoft.Testing.Platform.MSBuild\"", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>", csproj, StringComparison.Ordinal);
        var mtpMsBuildItemStart = csproj.IndexOf(
            "PackageReference Include=\"Microsoft.Testing.Platform.MSBuild\"",
            StringComparison.Ordinal);
        Assert.IsTrue(mtpMsBuildItemStart >= 0, "Expected Microsoft.Testing.Platform.MSBuild PackageReference.");
        var mtpMsBuildItemEnd = csproj.IndexOf("</PackageReference>", mtpMsBuildItemStart, StringComparison.Ordinal);
        Assert.IsTrue(mtpMsBuildItemEnd > mtpMsBuildItemStart, "Expected Microsoft.Testing.Platform.MSBuild PackageReference to close.");
        var mtpMsBuildItem = csproj[mtpMsBuildItemStart..mtpMsBuildItemEnd];
        Assert.DoesNotContain("<PrivateAssets>all</PrivateAssets>", mtpMsBuildItem, StringComparison.Ordinal);
        Assert.Contains("<PrivateAssets>none</PrivateAssets>", mtpMsBuildItem, StringComparison.Ordinal);
        // Runtime assets must flow: an NUnit-only consumer has no other Microsoft.Testing.Platform.
        Assert.DoesNotContain("<ExcludeAssets>", mtpMsBuildItem, StringComparison.Ordinal);
        Assert.Contains("<RepackBinariesExcludes", csproj, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Testing.Platform.dll", csproj, StringComparison.Ordinal);
        Assert.Contains("<PrivateAssets>all</PrivateAssets>", csproj, StringComparison.Ordinal);
        Assert.Contains("DisableTestingPlatformServerCapability", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("testhost-bcl", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("PackNet48Bcl", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("MTPAssembly", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("MTPEntry", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_MTPFileName", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_MTPFromRepo", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_UserTestConfigNormalized", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyDevToolsNUnitMtp", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(TestingFramework)' != 'tunit'", targets, StringComparison.Ordinal);
        Assert.Contains("'$(TestingFramework)' == 'nunit'", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_DevToolsMTPSkipPackageCopy", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("$(_PackageRuntimeDir)*.dll", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("$(_PackageRuntimeDir)DevTools.Ipc.dll", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyRuntimeClosure", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_AdapterOut", targets, StringComparison.Ordinal);
        Assert.Contains("$(_PackageRuntimeDir)DevTools.Testing.Abstractions.dll", targets, StringComparison.Ordinal);
        Assert.Contains("$(_PackageRuntimeDir)$(_MtpSiblingName).dll", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("MTPCopy", targets, StringComparison.Ordinal);
        Assert.Contains("_ResolveRuntimeDir", targets, StringComparison.Ordinal);
        Assert.Contains("VersionGreaterThanOrEquals", targets, StringComparison.Ordinal);
        Assert.Contains("has no runtime assets", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("PackRuntimeClosure", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("StageNet48Abstractions", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("$(TargetDir)*.dll", csproj, StringComparison.Ordinal);
        Assert.Contains("discovery-refs.txt", targets, StringComparison.Ordinal);
        Assert.Contains("%(ReferencePath.NuGetPackageId)", targets, StringComparison.Ordinal);
        Assert.Contains("%(ReferencePath.CopyLocal)", targets, StringComparison.Ordinal);
        Assert.Contains(@"\dotnet\packs\", targets, StringComparison.Ordinal);
        Assert.Contains(@"\Reference Assemblies\", targets, StringComparison.Ordinal);
        Assert.Contains("EndsWith('.ref')", targets, StringComparison.Ordinal);
        Assert.Contains("UpToDateCheckBuilt", targets, StringComparison.Ordinal);
        Assert.Contains("SkipUnchangedFiles=\"false\"", targets, StringComparison.Ordinal);
        Assert.Contains("_TestingPlatformConfigurationFileSourcePath", targets, StringComparison.Ordinal);
        Assert.Contains("$(IntermediateOutputPath)testconfig.json", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("$(OutputPath)$(TargetName).testconfig.json", targets, StringComparison.Ordinal);
        Assert.Contains("IConfiguration", loader, StringComparison.Ordinal);
        Assert.Contains("TestConfig.Keys", loader, StringComparison.Ordinal);
        Assert.Contains("TestConfig.FileName", loader, StringComparison.Ordinal);
        Assert.Contains("TestConfig.SectionName", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadKey(configuration, \"", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonDocument", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllText", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadFile", loader, StringComparison.Ordinal);
        Assert.DoesNotContain("devtools.testing.host.json", targets, StringComparison.Ordinal);
        Assert.DoesNotContain(".runsettings", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnit.Microsoft.Testing.Platform", targets, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Testing.Platform", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("GlobalPackageReference Remove=\"Polyfill\"", csproj, StringComparison.Ordinal);
        var commonProps = File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Build.props"));
        Assert.Contains("<PolyUseEmbeddedAttribute>true</PolyUseEmbeddedAttribute>", commonProps, StringComparison.Ordinal);
        Assert.Contains("<PolyArgumentExceptions>true</PolyArgumentExceptions>", commonProps, StringComparison.Ordinal);
        var packagesProps = File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Packages.props"));
        Assert.Contains("<GlobalPackageReference Include=\"Polyfill\"", packagesProps, StringComparison.Ordinal);
        Assert.DoesNotContain("Condition=\"$(TargetFramework.StartsWith('net4'))\"", packagesProps, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"System.Text.Json\"", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"System.Runtime.CompilerServices.Unsafe\"", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"Microsoft.Bcl.AsyncInterfaces\"", csproj, StringComparison.Ordinal);
        var transport = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.Testing.Transport", "DevTools.Testing.Transport.csproj"));
        Assert.Contains("Condition=\"'$(TargetFramework)' == 'net48'\"", transport, StringComparison.Ordinal);
        Assert.Contains("PackageReference Include=\"System.Text.Json\"", transport, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.SectionName}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.Keys.HostName}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.Keys.HostVersion}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.Keys.ForceLaunch}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.Keys.PerTestTimeoutSeconds}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.Keys.LaunchTimeoutSeconds}&quot;", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("hostTimeoutSeconds", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("hostLaunchTimeoutSeconds", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("requestTimeoutSeconds", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("&quot;hostLaunch&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.Keys.RunnerPath}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{TestConfig.Keys.FrameworkId}&quot;", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("mtpAssembly", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("mtpEntry", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsMTPAssembly", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsMTPEntry", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsMTPCopy", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsTestingRunnerPath", props, StringComparison.Ordinal);
        Assert.DoesNotContain("discoveryAttributes", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("TestingDiscoveryAttributes", targets, StringComparison.Ordinal);
        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.Testing")));
        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.Testing.Discovery")));
        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, "tests", "DevTools.Testing.Tests")));
    }

    [TestMethod]
    public void Net48_mtp_ilrepacks_own_dll_not_consumer_exe()
    {
        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var targets = File.ReadAllText(Path.Combine(mtpDir, "build", "RevitDevTool.TestAdapter.targets"));
        var csproj = File.ReadAllText(Path.Combine(mtpDir, "DevTools.TestAdapter.csproj"));
        var ilRepackTargets = File.ReadAllText(Path.Combine(RepositoryRoot, "props", "ILRepack.targets"));

        Assert.DoesNotContain("ILRepack", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepack.Lib.MSBuild.Task", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference Include=\"ILRepack\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"ILRepack\"", ilRepackTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRepackable", ilRepackTargets, StringComparison.Ordinal);
        Assert.Contains("ILRepackable", csproj, StringComparison.Ordinal);
        Assert.Contains("ILRepackInternalize", csproj, StringComparison.Ordinal);
        Assert.Contains("RepackBinariesKeep", csproj, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions.dll", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(TargetFramework)' == 'net48'", csproj, StringComparison.Ordinal);
        Assert.Contains("'$(TargetFramework)' != ''", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith('net4')", csproj, StringComparison.Ordinal);
        Assert.Contains("'$(TargetFrameworkIdentifier)' == '.NETCoreApp'", csproj, StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "props", "ILRepack.targets")));
        Assert.IsFalse(File.Exists(Path.Combine(mtpDir, "ILRepack.targets")));
    }

    [TestMethod]
    public void Runner_owns_visual_studio_interop()
    {
        var debugging = Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestRunner",
            "Debugging",
            "VisualStudioAttach.cs");
        var attach = File.ReadAllText(debugging);
        Assert.Contains("EnvDTE", attach, StringComparison.Ordinal);
        Assert.Contains("DebuggedProcesses", attach, StringComparison.Ordinal);

        var runnerCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestRunner",
            "DevTools.TestRunner.csproj"));
        Assert.Contains("Microsoft.VisualStudio.Interop", runnerCsproj, StringComparison.Ordinal);

        var mtpCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "DevTools.TestAdapter.csproj"));
        Assert.DoesNotContain("Microsoft.VisualStudio.Interop", mtpCsproj, StringComparison.Ordinal);
    }
}
