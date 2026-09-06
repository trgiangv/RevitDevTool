using DevTools.Testing.Abstractions.Config;

namespace DevTools.TestAdapter.Tests;

public sealed class AdapterArchitectureTests
{
    [Fact]
    public void TUnit_uses_the_existing_adapter_and_TestRunner_transport()
    {
        var adapterDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var props = File.ReadAllText(Path.Combine(adapterDir, "build", "RevitDevTool.TestAdapter.props"));
        var targets = File.ReadAllText(Path.Combine(adapterDir, "build", "RevitDevTool.TestAdapter.targets"));

        Assert.Contains("'$(TestingFramework)' == 'tunit'", props, StringComparison.Ordinal);
        Assert.Contains("DevTools.TestAdapter.TestingPlatformBuilderHook", props, StringComparison.Ordinal);
        Assert.DoesNotContain("supports only Revit 2023", props, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(HostVersion)' != '2023'", props, StringComparison.Ordinal);
        Assert.Contains("DevTools.TUnit.MTP.dll", targets, StringComparison.Ordinal);
        Assert.Contains("TestingPlatformBuilderHook Remove=\"6ADF853A-6945-4A06-9A4B-D99BC1DC1094\"", targets, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(adapterDir, "TUnitTestingPlatformBuilderHook.cs")));
        Assert.False(File.Exists(Path.Combine(adapterDir, "RevitTestHostLauncher.cs")));
        Assert.False(File.Exists(Path.Combine(adapterDir, "build", "TUnitRevitExecutor.cs")));
    }

    [Fact]
    public void TUnit_runtime_is_isolated_from_the_host_and_consumer_output()
    {
        var props = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TestAdapter", "build", "RevitDevTool.TestAdapter.props"));
        var hostProject = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "RevitDevTool", "RevitDevTool.csproj"));
        var packaging = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TUnit.Runtime", "build", "TUnitHostPackaging.targets"));

        Assert.DoesNotContain("ILRepackable", props, StringComparison.Ordinal);
        Assert.Contains("DevTools.TUnit.Host", hostProject, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(RevitVersion)' == '2023' OR '$(RevitVersion)' == '2025'", hostProject, StringComparison.Ordinal);
        Assert.Contains("TUnitRuntime\\", packaging, StringComparison.Ordinal);
        Assert.Contains("TUnit.Core.dll must be deployed under TUnitRuntime", packaging, StringComparison.Ordinal);
        Assert.Contains("TUnit.Engine.dll must be deployed under TUnitRuntime", packaging, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Testing.Platform.dll must be deployed under TUnitRuntime", packaging, StringComparison.Ordinal);
        Assert.DoesNotContain("must not ship Microsoft.Testing.Platform", packaging, StringComparison.Ordinal);
        Assert.Contains("must not be copied at the add-in root", packaging, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(RevitVersion)' == '2023' OR '$(RevitVersion)' == '2025'", packaging, StringComparison.Ordinal);
    }

    [Fact]
    public void Revit_and_Acad_compositions_register_tunit_host_services()
    {
        var revitComposition = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "RevitDevTool", "Composition", "RevitServiceRegistration.cs"));
        var acadComposition = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "AcadDevTool", "Composition", "AcadServiceRegistration.cs"));

        Assert.Contains("AddTUnitHostServices", revitComposition, StringComparison.Ordinal);
        Assert.Contains("AddTUnitHostServices", acadComposition, StringComparison.Ordinal);
    }

    [Fact]
    public void Revit_TUnit_execution_reuses_the_generic_testing_run_handler()
    {
        var root = Path.Combine(RepositoryRoot, "source", "RevitDevTool");
        var composition = File.ReadAllText(Path.Combine(root, "Composition", "RevitServiceRegistration.cs"));
        var handler = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.Testing.Host", "MarshaledTestRequestHandler.cs"));
        var genericHosting = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.Testing.Host", "TestingHostingExtensions.cs"));
        var nunitHosting = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.NUnit.Host", "NUnitHostingExtensions.cs"));

        Assert.Contains("AddTUnitHostServices", composition, StringComparison.Ordinal);
        Assert.Contains("TestingProviderRegistry", genericHosting, StringComparison.Ordinal);
        Assert.DoesNotContain("TestingProviderRegistry", nunitHosting, StringComparison.Ordinal);
        Assert.DoesNotContain("REVIT2023 || REVIT2025", composition, StringComparison.Ordinal);
        Assert.Contains("_hostContext.ExecuteAsync", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("GetResult(), ct)", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("RevitTestExecutionDispatcher", composition, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "Testing", "RevitTestHostApplicationLauncher.cs")));
        Assert.False(File.Exists(Path.Combine(root, "Testing", "RevitTestExecutionDispatcher.cs")));
    }

    [Fact]
    public void TUnit_in_host_uses_engine_instead_of_nested_mtp()
    {
        var runtimeDir = Path.Combine(RepositoryRoot, "source", "DevTools.TUnit.Runtime");
        var session = File.ReadAllText(Path.Combine(runtimeDir, "TUnitRuntimeSession.cs"));
        var host = File.ReadAllText(Path.Combine(runtimeDir, "TUnitEngineHost.cs"));
        var catalog = File.ReadAllText(Path.Combine(runtimeDir, "TUnitCatalog.cs"));
        var identity = File.ReadAllText(Path.Combine(runtimeDir, "TUnitTestIdentity.cs"));
        var project = File.ReadAllText(Path.Combine(runtimeDir, "DevTools.TUnit.Runtime.csproj"));
        var discoverer = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.TUnit.MTP", "TUnitHostTestDiscoverer.cs"));
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
        Assert.Contains("TestingDiscoveryOptions", discoverer, StringComparison.Ordinal);
        Assert.Contains("InheritanceDepth", identity, StringComparison.Ordinal);
        Assert.Contains("_Deferred", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitAot", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitAot", session, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(runtimeDir, "TUnitExecutor.cs")));
        Assert.False(File.Exists(Path.Combine(runtimeDir, "TUnitHooks.cs")));
        Assert.Contains("TestingRunTraceScope", host, StringComparison.Ordinal);
        Assert.Contains("TUnitEngineMessageBus(traceScope)", host, StringComparison.Ordinal);
        Assert.Contains("TestingRunTraceScope", File.ReadAllText(Path.Combine(runtimeDir, "TUnitEnginePlatform.cs")), StringComparison.Ordinal);
        Assert.Contains("TestingRunTraceScope.Merge", File.ReadAllText(Path.Combine(runtimeDir, "TUnitEngineResults.cs")), StringComparison.Ordinal);
        Assert.Contains("TestingEventKinds.Output", session, StringComparison.Ordinal);

        var expansion = File.ReadAllText(Path.Combine(runtimeDir, "TUnitExpansion.cs"));
        Assert.Contains("GetDataRowsAsync", expansion, StringComparison.Ordinal);
        Assert.Contains("RepeatTimes", expansion, StringComparison.Ordinal);
        Assert.Contains("ResolvePropertyDataSources", expansion, StringComparison.Ordinal);
        Assert.Contains("new SourceRow([], 1, 1, null)", expansion, StringComparison.Ordinal);
        Assert.Contains("[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]", expansion, StringComparison.Ordinal);
        Assert.Contains("TUnitCombinationIndices", expansion, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitAot", expansion, StringComparison.Ordinal);
    }

    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
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

        Assert.Empty(offenders);
    }

    [Fact]
    public void Mtp_discovery_does_not_invoke_host_runner()
    {
        var framework = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "HostTestFramework.cs"));

        Assert.DoesNotContain("MetadataTestDiscoverer", framework, StringComparison.Ordinal);
        Assert.Contains("HostTestDiscovery.Provider", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("session.Discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("_transport.Discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("IDebugSession", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDebugSession", framework, StringComparison.Ordinal);
        Assert.Contains("Debugger.IsAttached", framework, StringComparison.Ordinal);
        Assert.Contains("EnsureSession()", framework, StringComparison.Ordinal);
        Assert.Contains("PublishRunAsync(assemblyPath", framework, StringComparison.Ordinal);
        Assert.Contains("ApplyDebugParent", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultFrameworkId", File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "HostOptionsLoader.cs")), StringComparison.Ordinal);

        var session = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "HostTestSession.cs"));
        Assert.DoesNotContain("Discover(", session, StringComparison.Ordinal);

        var client = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.Testing.Transport",
            "ProcessTestRunnerClient.cs"));
        Assert.DoesNotContain("NUnitRunnerCli.DiscoverCommand", client, StringComparison.Ordinal);
        Assert.DoesNotContain("IReadOnlyList<TestingDiscoveredTest> Discover", client, StringComparison.Ordinal);
        Assert.Contains("TestingRunnerCli.BuildRunArguments", client, StringComparison.Ordinal);

        var transport = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.Testing.Transport",
            "ITestRunnerTransport.cs"));
        Assert.DoesNotContain("Discover(", transport, StringComparison.Ordinal);
    }

    [Fact]
    public void Mtp_uses_the_generic_runner_client()
    {
        var client = Path.Combine(
            RepositoryRoot, "source", "DevTools.Testing.Transport", "ProcessTestRunnerClient.cs");
        Assert.True(File.Exists(client));
        Assert.False(File.Exists(Path.Combine(
            RepositoryRoot, "source", "DevTools.TestAdapter", "ProcessRunnerClient.cs")));
        Assert.False(File.Exists(Path.Combine(
            RepositoryRoot, "source", "DevTools.TestAdapter", "NUnitProcessTransportAdapter.cs")));
        Assert.False(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Client")));

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

    [Fact]
    public void Local_discovery_is_nunit_explore_tests_not_pe_metadata()
    {
        Assert.False(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.Testing.Discovery")));
        Assert.False(File.Exists(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "MetadataTestDiscoverer.cs")));

        var framework = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "HostTestFramework.cs"));
        Assert.Contains("HostTestDiscovery.Provider", framework, StringComparison.Ordinal);
        Assert.Contains("HostTestDiscovery.RunMapper", framework, StringComparison.Ordinal);
        Assert.Contains("FoldResults", framework, StringComparison.Ordinal);
        Assert.Contains("ToHostSelection", framework, StringComparison.Ordinal);
        Assert.Contains("devtools.testadapter.discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitCollapsedSelection", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("using DevTools.NUnit.Runtime", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("ToMetadataTypeName", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("LastDotAtDepthZero", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("TrySplitIdentity", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataTestDiscoverer", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("PEReader", framework, StringComparison.Ordinal);
    }

    [Fact]
    public void Adapter_hook_is_framework_neutral_and_delegates_mtp_registration()
    {
        var adapterDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var abstractionsDir = Path.Combine(RepositoryRoot, "source", "DevTools.Testing.Abstractions");
        var hook = File.ReadAllText(Path.Combine(adapterDir, "TestingPlatformBuilderHook.cs"));
        var bootstrap = File.ReadAllText(Path.Combine(adapterDir, "AdapterBootstrap.cs"));
        var registration = File.ReadAllText(Path.Combine(adapterDir, "HostMtpRegistration.cs"));
        var abstractionsSources = Directory.EnumerateFiles(abstractionsDir, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.Contains("AdapterBootstrap.Initialize()", hook, StringComparison.Ordinal);
        Assert.Contains("HostTestDiscovery", hook, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitMTP", hook, StringComparison.Ordinal);
        Assert.DoesNotContain("TUnitMTP", hook, StringComparison.Ordinal);
        Assert.Contains("TryReadPluginConfig", bootstrap, StringComparison.Ordinal);
        Assert.Contains("HostMtpRegistration.Register", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireFrameworkId()", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("TryResolvePlugin", registration, StringComparison.Ordinal);
        Assert.Contains("Path.GetFileName", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnit.MTP", string.Concat(abstractionsSources), StringComparison.Ordinal);
        Assert.DoesNotContain("TUnit.MTP", string.Concat(abstractionsSources), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(abstractionsDir, "Mtp", "HostMTPRegistration.cs")));
    }

    [Fact]
    public void NUnit_mtp_owns_authoritative_discovery_and_loads_beside_the_adapter()
    {
        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.MTP");
        var discoverer = File.ReadAllText(Path.Combine(mtpDir, "NUnitHostTestDiscoverer.cs"));

        Assert.Contains("NUnitTestAssemblyRunner", discoverer, StringComparison.Ordinal);
        Assert.Contains("ExploreTests", discoverer, StringComparison.Ordinal);
        Assert.Contains("test.FullName", discoverer, StringComparison.Ordinal);
        Assert.Contains("ToSourceTypeSegment", discoverer, StringComparison.Ordinal);
        Assert.Contains("ToHostSelection", File.ReadAllText(Path.Combine(mtpDir, "NUnitHostTestDiscoverer.RunMapping.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("HostLocator", discoverer, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", discoverer, StringComparison.Ordinal);
        Assert.Contains("HostTestDiscovery.RunMapper", File.ReadAllText(Path.Combine(mtpDir, "NUnitMTP.cs")), StringComparison.Ordinal);
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

        var mtpCsproj = File.ReadAllText(Path.Combine(mtpDir, "DevTools.NUnit.MTP.csproj"));
        Assert.DoesNotContain("DevTools.TestAdapter.csproj", mtpCsproj, StringComparison.Ordinal);
        Assert.Contains("DevTools.Testing.Abstractions.csproj", mtpCsproj, StringComparison.Ordinal);
    }

    [Fact]
    public void Net48_consumer_props_enable_binding_redirects()
    {
        var props = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "build",
            "RevitDevTool.TestAdapter.props"));

        Assert.Contains("GenerateBindingRedirectsOutputType", props, StringComparison.Ordinal);
        Assert.Contains("TargetFrameworkIdentifier", props, StringComparison.Ordinal);
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

    [Fact]
    public void Adapter_writes_mtp_testconfig_devtools_section_and_skips_polyfill()
    {
        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.TestAdapter");
        var targets = File.ReadAllText(Path.Combine(mtpDir, "build", "RevitDevTool.TestAdapter.targets"));
        var props = File.ReadAllText(Path.Combine(mtpDir, "build", "RevitDevTool.TestAdapter.props"));
        var csproj = File.ReadAllText(Path.Combine(mtpDir, "DevTools.TestAdapter.csproj"));
        var loader = File.ReadAllText(Path.Combine(mtpDir, "HostOptionsLoader.cs"));

        Assert.Contains("WriteDiscoveryRefs", targets, StringComparison.Ordinal);
        Assert.Contains("CopyMTPSibling", targets, StringComparison.Ordinal);
        Assert.Contains("_StagePackageRuntime", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("testhost-bcl", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("PackNet48Bcl", csproj, StringComparison.Ordinal);
        Assert.Contains("MTPAssembly", targets, StringComparison.Ordinal);
        Assert.Contains("MTPEntry", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_MTPFileName", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_MTPFromRepo", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_UserTestConfigNormalized", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyDevToolsNUnitMtp", targets, StringComparison.Ordinal);
        var siblingTarget = targets[targets.IndexOf("<Target Name=\"CopyMTPSibling\"", StringComparison.Ordinal)..];
        var siblingEnd = siblingTarget.IndexOf("</Target>", StringComparison.Ordinal);
        if (siblingEnd >= 0)
            siblingTarget = siblingTarget[..(siblingEnd + "</Target>".Length)];
        Assert.DoesNotContain("'$(TestingFramework)' != 'tunit'", siblingTarget, StringComparison.Ordinal);
        Assert.DoesNotContain("testhost-bcl", siblingTarget, StringComparison.Ordinal);
        Assert.Contains("'$(TestingFramework)' == 'nunit'", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_DevToolsMTPSkipPackageCopy", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("$(_PackageRuntimeDir)*.dll", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("$(_PackageRuntimeDir)DevTools.Ipc.dll", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyRuntimeClosure", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("_AdapterOut", targets, StringComparison.Ordinal);
        Assert.Contains("$(_PackageRuntimeDir)DevTools.Testing.Abstractions.dll", targets, StringComparison.Ordinal);
        Assert.Contains("$(_PackageRuntimeDir)$(MTPAssembly)", targets, StringComparison.Ordinal);
        Assert.Contains("Exists('$(OutDir)$(MTPAssembly)')", targets, StringComparison.Ordinal);
        Assert.Contains("MTPCopy", targets, StringComparison.Ordinal);
        Assert.Contains("_ResolveRuntimeDir", targets, StringComparison.Ordinal);
        Assert.Contains("VersionGreaterThanOrEquals", targets, StringComparison.Ordinal);
        Assert.Contains("has no runtime assets", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("PackRuntimeClosure", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("StageNet48Abstractions", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("$(TargetDir)*.dll", csproj, StringComparison.Ordinal);
        Assert.Contains("ReferenceOutputAssembly>false", targets, StringComparison.Ordinal);
        Assert.Contains("discovery-refs.txt", targets, StringComparison.Ordinal);
        Assert.Contains("%(ReferencePath.NuGetPackageId)", targets, StringComparison.Ordinal);
        Assert.Contains("%(ReferencePath.CopyLocal)", targets, StringComparison.Ordinal);
        Assert.Contains(@"\dotnet\packs\", targets, StringComparison.Ordinal);
        Assert.Contains(@"\Reference Assemblies\", targets, StringComparison.Ordinal);
        Assert.Contains("UpToDateCheckBuilt", targets, StringComparison.Ordinal);
        Assert.Contains("SkipUnchangedFiles=\"false\"", targets, StringComparison.Ordinal);
        Assert.Contains("_TestingPlatformConfigurationFileSourcePath", targets, StringComparison.Ordinal);
        Assert.Contains("$(IntermediateOutputPath)testconfig.json", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("$(OutputPath)$(TargetName).testconfig.json", targets, StringComparison.Ordinal);
        Assert.Contains("IConfiguration", loader, StringComparison.Ordinal);
        Assert.Contains("HostTestConfig.Keys", loader, StringComparison.Ordinal);
        Assert.Contains("HostTestConfig.FileName", loader, StringComparison.Ordinal);
        Assert.Contains("HostTestConfig.SectionName", loader, StringComparison.Ordinal);
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
        Assert.Contains($"&quot;{HostTestConfig.SectionName}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.HostName}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.HostVersion}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.ForceLaunch}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.PerTestTimeoutSeconds}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.LaunchTimeoutSeconds}&quot;", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("hostTimeoutSeconds", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("hostLaunchTimeoutSeconds", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("requestTimeoutSeconds", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("&quot;hostLaunch&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.RunnerPath}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.FrameworkId}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.MTPAssembly}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains($"&quot;{HostTestConfig.Keys.MTPEntry}&quot;", targets, StringComparison.Ordinal);
        Assert.Contains("MTPAssembly", props, StringComparison.Ordinal);
        Assert.Contains("MTPEntry", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsMTPAssembly", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsMTPEntry", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsMTPCopy", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsTestingRunnerPath", props, StringComparison.Ordinal);
        Assert.DoesNotContain("discoveryAttributes", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("TestingDiscoveryAttributes", targets, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.Testing")));
        Assert.False(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.Testing.Discovery")));
        Assert.False(Directory.Exists(Path.Combine(RepositoryRoot, "tests", "DevTools.Testing.Tests")));
    }

    [Fact]
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
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, "props", "ILRepack.targets")));
        Assert.False(File.Exists(Path.Combine(mtpDir, "ILRepack.targets")));
    }

    [Fact]
    public void Runner_owns_visual_studio_interop()
    {
        var debugging = Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestRunner.Core",
            "Debugging",
            "VisualStudioAttach.cs");
        var attach = File.ReadAllText(debugging);
        Assert.Contains("EnvDTE", attach, StringComparison.Ordinal);
        Assert.Contains("DebuggedProcesses", attach, StringComparison.Ordinal);

        var runnerCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestRunner.Core",
            "DevTools.TestRunner.Core.csproj"));
        Assert.Contains("Microsoft.VisualStudio.Interop", runnerCsproj, StringComparison.Ordinal);

        var mtpCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestAdapter",
            "DevTools.TestAdapter.csproj"));
        Assert.DoesNotContain("Microsoft.VisualStudio.Interop", mtpCsproj, StringComparison.Ordinal);
    }
}
