using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using DevTools.NUnit.Host;
using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Utilities.AssemblyLoading;
using NUnit.Framework;
using FactAttribute = Xunit.FactAttribute;

namespace DevTools.NUnit.Host.NetFramework.Tests;

public sealed class NetFrameworkGenerationTests
{
    private const string DependencyConsumerAssemblyName = "DependencyConsumer";
    private const string GenerationPrivateDependencyAssemblyName = "GenerationPrivateDependency";

    [Fact]
    public void SharedAssemblyPolicy_keeps_netfx_polyfills_generation_private()
    {
        HostSharedAssemblies.Use(new HostSharedAssemblyNames(["RevitAPI"], ["Autodesk."]));
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("System"), Is.True);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("System.Core"), Is.True);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("System.Runtime"), Is.False);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("System.Custom"), Is.False);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("Microsoft.Custom"), Is.False);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("Microsoft.Win32.Registry"), Is.False);
        Assert.That(
            new[]
            {
                "System.Buffers",
                "System.Collections.Immutable",
                "System.Diagnostics.DiagnosticSource",
                "System.IO.Hashing",
                "System.IO.Pipelines",
                "System.Memory",
                "System.Numerics.Vectors",
                "System.Runtime.CompilerServices.Unsafe",
                "System.Text.Encodings.Web",
                "System.Text.Json",
                "System.Threading.Channels",
                "System.Threading.Tasks.Extensions",
            }.All(name => !NUnitSharedAssemblyPolicy.IsShared(name)),
            Is.True);
        Assert.That(NUnitSharedAssemblyPolicy.IsManagedAssemblyFile("HostSmokeTests.exe"), Is.True);
        Assert.That(NUnitSharedAssemblyPolicy.IsManagedAssemblyFile("nunit.framework.dll"), Is.True);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("System.Reflection.Metadata"), Is.False);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("Microsoft.Extensions.Logging.Abstractions"), Is.False);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("MahApps.Metro"), Is.True);
        Assert.That(NUnitSharedAssemblyPolicy.IsShared("Autodesk.Revit.DB"), Is.True);
    }

    [Fact]
    public void GenerationBuilder_keeps_versioned_system_and_microsoft_dependencies_private()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var testAssembly = NetFrameworkGenerationTestEnvironment.CreateGenerationOneAssembly(
                workspace,
                "versioned-bcl-dependencies");
            var outputDirectory = Path.GetDirectoryName(testAssembly)!;
            var testDirectory = Path.GetDirectoryName(typeof(NetFrameworkGenerationTests).Assembly.Location)!;

            foreach (var fileName in new[] { "System.Text.Json.dll", "Microsoft.Bcl.AsyncInterfaces.dll" })
            {
                File.Copy(
                    Path.Combine(testDirectory, fileName),
                    Path.Combine(outputDirectory, fileName),
                    overwrite: true);
            }

            var generationsRoot = NetFrameworkGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
            var manifest = NetFrameworkGenerationTestEnvironment.CreateBuilder(generationsRoot).Build(testAssembly);

            Assert.That(File.Exists(Path.Combine(manifest.ShadowDirectory, "System.Text.Json.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(manifest.ShadowDirectory, "Microsoft.Bcl.AsyncInterfaces.dll")), Is.True);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void GenerationBuilder_excludes_loose_testing_abstractions_identity_from_netfx_generation()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var testAssembly = NetFrameworkGenerationTestEnvironment.CreateGenerationOneAssembly(workspace, "testing-abstractions-shared");
            var outputDirectory = Path.GetDirectoryName(testAssembly)!;
            var renamedPath = Path.Combine(outputDirectory, "PrivateTestingContract.dll");
            File.Copy(typeof(TestingRunRequest).Assembly.Location, renamedPath, overwrite: true);

            var manifest = NetFrameworkGenerationTestEnvironment.CreateBuilder(
                NetFrameworkGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

            Assert.That(File.Exists(Path.Combine(manifest.ShadowDirectory, "PrivateTestingContract.dll")), Is.False);
            Assert.That(NUnitSharedAssemblyPolicy.ShouldExcludeFromGenerationCopy(renamedPath), Is.True);
            Assert.That(NUnitSharedAssemblyPolicy.IsShared(typeof(TestingRunRequest).Assembly.GetName().Name!), Is.True);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void Process_runs_on_clr_48()
    {
        Assert.That(Environment.Version.Major, Is.EqualTo(4));
        Assert.That(Environment.Version, Is.GreaterThanOrEqualTo(new Version(4, 0, 30319)));
        Assert.That(RuntimeInformation.FrameworkDescription, Does.Contain(".NET Framework"));
        Assert.That(AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName, Does.Contain(".NETFramework"));

        var currentProcess = Process.GetCurrentProcess();
        TestContext.WriteLine($"Process: {currentProcess.ProcessName} ({currentProcess.Id})");
        TestContext.WriteLine($"Environment.Version: {Environment.Version}");
        TestContext.WriteLine($"FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
        TestContext.WriteLine($"TargetFrameworkName: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}");
        TestContext.WriteLine($"Test host base directory: {AppContext.BaseDirectory}");
    }

    [Fact]
    public void Create_executes_with_conflicting_preloaded_nunit_identity()
    {
        var conflicting = NetFrameworkGenerationTestEnvironment.LoadConflictingNUnitIntoAppDomain();
        Assert.That(conflicting.GetName().Name, Is.EqualTo("nunit.framework").IgnoreCase);

        var testHostNUnit = FindTestHostNUnitAssembly();
        Assert.That(testHostNUnit, Is.Not.Null);
        Assert.That(testHostNUnit!.GetName().Version, Is.EqualTo(new Version(4, 6, 0, 0)));

        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        Assert.That(
            conflicting.Location.StartsWith(manifest.ShadowDirectory, StringComparison.OrdinalIgnoreCase),
            Is.False);

        var probe = RunGenerationProbe("conflicting-binding");
        Assert.That(probe.ExitCode, Is.EqualTo(0), probe.Output);
        Assert.That(probe.Output, Does.Contain("GenerationFrameworkLocation="));
        Assert.That(probe.Output, Does.Contain("RunnerLocation="));
        Assert.That(probe.Output, Does.Not.Contain($"RunnerLocation={testHostNUnit.Location}"));
    }

    [Fact]
    public void Create_loads_two_generations_in_one_appdomain()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationTwo();
        Assert.That(generationTwo.GenerationId, Is.Not.EqualTo(generationOne.GenerationId));

        using var factory = new NetfxNUnitRuntimeSessionFactory();
        using (var sessionOne = factory.Create(generationOne))
        {
            var handleOne = (NetfxNUnitSessionHandle)sessionOne;
            Assert.That(ReadGenerationMarker(handleOne.GetLoadedTestAssembly()), Is.EqualTo("generation-one"));
            AssertGenerationMarkerCasePasses(sessionOne, generationOne);
        }

        using (var sessionTwo = factory.Create(generationTwo))
        {
            var handleTwo = (NetfxNUnitSessionHandle)sessionTwo;
            Assert.That(ReadGenerationMarker(handleTwo.GetLoadedTestAssembly()), Is.EqualTo("generation-two"));
            AssertGenerationMarkerCasePasses(sessionTwo, generationTwo);
        }

        Assert.That(factory.RetainedGenerationCount, Is.EqualTo(2));
        var diagnostic = factory.CreateRetainedDiagnostic();
        Assert.That(diagnostic.Code, Is.EqualTo("generation.retained"));
        Assert.That(diagnostic.Message, Does.Contain("2"));
    }

    [Fact]
    public void Create_resolves_same_identity_dependency_per_requesting_generation()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildDependencyGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildDependencyGenerationTwo();
        Assert.That(generationTwo.GenerationId, Is.Not.EqualTo(generationOne.GenerationId));

        using var factory = new NetfxNUnitRuntimeSessionFactory();
        using var sessionOne = factory.Create(generationOne);
        using var sessionTwo = factory.Create(generationTwo);

        var handleOne = (NetfxNUnitSessionHandle)sessionOne;
        var handleTwo = (NetfxNUnitSessionHandle)sessionTwo;

        Assert.That(handleOne.Generation.OwnsAssemblyNamed(GenerationPrivateDependencyAssemblyName), Is.False);
        Assert.That(handleTwo.Generation.OwnsAssemblyNamed(GenerationPrivateDependencyAssemblyName), Is.False);
        Assert.That(factory.LazyResolutionRecords.Count, Is.EqualTo(0));

        var caseOne = RunDependencyProbe(sessionOne, generationOne);
        Assert.That(caseOne.Outcome, Is.EqualTo(TestingOutcomes.Passed));
        Assert.That(caseOne.Output, Does.Contain("dependency-behavior=behavior-one"));

        var caseTwo = RunDependencyProbe(sessionTwo, generationTwo);
        Assert.That(caseTwo.Outcome, Is.EqualTo(TestingOutcomes.Passed));
        Assert.That(caseTwo.Output, Does.Contain("dependency-behavior=behavior-two"));

        Assert.That(handleOne.Generation.LazyResolutionCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(handleTwo.Generation.LazyResolutionCount, Is.GreaterThanOrEqualTo(1));

        var records = factory.LazyResolutionRecords
            .Where(record =>
                string.Equals(
                    record.RequestedAssemblyName,
                    GenerationPrivateDependencyAssemblyName,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.That(records.Count, Is.GreaterThanOrEqualTo(2));

        var recordOne = records.Single(record => record.GenerationId == generationOne.GenerationId);
        var recordTwo = records.Single(record => record.GenerationId == generationTwo.GenerationId);

        Assert.That(recordOne.RequestingAssemblyName, Is.EqualTo(DependencyConsumerAssemblyName).IgnoreCase);
        Assert.That(recordTwo.RequestingAssemblyName, Is.EqualTo(DependencyConsumerAssemblyName).IgnoreCase);
        Assert.That(recordOne.RequestingAssemblyLocation, Does.StartWith(generationOne.ShadowDirectory).IgnoreCase);
        Assert.That(recordTwo.RequestingAssemblyLocation, Does.StartWith(generationTwo.ShadowDirectory).IgnoreCase);
        Assert.That(recordOne.RequestingAssemblyLocation, Is.Not.EqualTo(recordTwo.RequestingAssemblyLocation).IgnoreCase);
        Assert.That(recordOne.ResolvedAssemblyLocation, Does.StartWith(generationOne.ShadowDirectory).IgnoreCase);
        Assert.That(recordTwo.ResolvedAssemblyLocation, Does.StartWith(generationTwo.ShadowDirectory).IgnoreCase);

        var dependencyOne = LoadDependencyFromRecord(recordOne);
        var dependencyTwo = LoadDependencyFromRecord(recordTwo);

        Assert.That(dependencyOne, Is.Not.SameAs(dependencyTwo));
        Assert.That(dependencyOne.Location, Is.Not.EqualTo(dependencyTwo.Location).IgnoreCase);

        Assert.That(factory.RetainedGenerationCount, Is.EqualTo(2));
    }

    [Fact]
    public void Create_resolves_root_dependency_per_requesting_generation()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildRootDependencyGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildRootDependencyGenerationTwo();

        using var factory = new NetfxNUnitRuntimeSessionFactory();
        using var sessionOne = factory.Create(generationOne);
        using var sessionTwo = factory.Create(generationTwo);

        var caseOne = RunDependencyProbe(sessionOne, generationOne);
        var caseTwo = RunDependencyProbe(sessionTwo, generationTwo);

        Assert.That(caseOne.Output, Does.Contain("dependency-behavior=behavior-one"));
        Assert.That(caseTwo.Output, Does.Contain("dependency-behavior=behavior-two"));
    }

    [Fact]
    public void Create_concurrent_generations_remain_isolated()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationTwo();

        using var factory = new NetfxNUnitRuntimeSessionFactory();
        ITestingRuntimeSession? sessionOne = null;
        ITestingRuntimeSession? sessionTwo = null;

        var createOne = Task.Factory.StartNew(() => sessionOne = factory.Create(generationOne));
        var createTwo = Task.Factory.StartNew(() => sessionTwo = factory.Create(generationTwo));
        Task.WaitAll(createOne, createTwo);

        Assert.That(sessionOne, Is.Not.Null);
        Assert.That(sessionTwo, Is.Not.Null);

        var handleOne = (NetfxNUnitSessionHandle)sessionOne!;
        var handleTwo = (NetfxNUnitSessionHandle)sessionTwo!;

        try
        {
            Assert.That(handleOne.GenerationId, Is.EqualTo(generationOne.GenerationId));
            Assert.That(handleTwo.GenerationId, Is.EqualTo(generationTwo.GenerationId));

            Assert.That(
                handleOne.GetLoadedFrameworkAssembly().Location,
                Does.StartWith(generationOne.ShadowDirectory).IgnoreCase);
            Assert.That(
                handleTwo.GetLoadedFrameworkAssembly().Location,
                Does.StartWith(generationTwo.ShadowDirectory).IgnoreCase);
            Assert.That(
                handleOne.GetLoadedTestAssembly().Location,
                Does.StartWith(generationOne.ShadowDirectory).IgnoreCase);
            Assert.That(
                handleTwo.GetLoadedTestAssembly().Location,
                Does.StartWith(generationTwo.ShadowDirectory).IgnoreCase);
            Assert.That(
                handleOne.GetLoadedRuntimeAssembly().Location,
                Does.StartWith(generationOne.ShadowDirectory).IgnoreCase);
            Assert.That(
                handleTwo.GetLoadedRuntimeAssembly().Location,
                Does.StartWith(generationTwo.ShadowDirectory).IgnoreCase);

            Assert.That(ReadGenerationMarker(handleOne.GetLoadedTestAssembly()), Is.EqualTo("generation-one"));
            Assert.That(ReadGenerationMarker(handleTwo.GetLoadedTestAssembly()), Is.EqualTo("generation-two"));

            Assert.That(factory.RetainedGenerationCount, Is.EqualTo(2));
        }
        finally
        {
            sessionOne?.Dispose();
            sessionTwo?.Dispose();
        }

        var probe = RunGenerationProbe("concurrent-binding");
        Assert.That(probe.ExitCode, Is.EqualTo(0), probe.Output);
        Assert.That(probe.Output, Does.Contain("GenerationOneRunner="));
        Assert.That(probe.Output, Does.Contain("GenerationTwoRunner="));
    }

    [Fact]
    public void Dispose_unregisters_handler_without_leaking_duplicate_subscriptions()
    {
        using (var factory = new NetfxNUnitRuntimeSessionFactory())
        {
            Assert.That(factory.HandlerIsRegisteredForTesting, Is.True);
            _ = factory.Create(NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne());
        }

        using (var factory = new NetfxNUnitRuntimeSessionFactory())
        {
            Assert.That(factory.HandlerIsRegisteredForTesting, Is.True);
        }

        Assert.That(
            () =>
            {
                using var factory = new NetfxNUnitRuntimeSessionFactory();
                Assert.That(factory.HandlerIsRegisteredForTesting, Is.True);
            },
            Throws.Nothing);
    }

    [Fact]
    public void Dispose_is_idempotent_and_serializes_against_create()
    {
        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var factory = new NetfxNUnitRuntimeSessionFactory();
        var createStarted = new ManualResetEventSlim(false);
        Exception? createFailure = null;
        ITestingRuntimeSession? session = null;

        var createTask = Task.Factory.StartNew(() =>
        {
            createStarted.Set();
            try
            {
                session = factory.Create(manifest);
            }
            catch (Exception ex)
            {
                createFailure = ex;
            }
        });

        Assert.That(createStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);
        factory.Dispose();
        factory.Dispose();
        Assert.That(createTask.Wait(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(factory.HandlerIsRegisteredForTesting, Is.False);

        if (createFailure is not null)
        {
            Assert.That(createFailure, Is.TypeOf<ObjectDisposedException>());
            return;
        }

        Assert.That(session, Is.Not.Null);
        session!.Dispose();
    }

    [Fact]
    public void Dispose_retains_generations_after_session_disposal()
    {
        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();

        using (var factory = new NetfxNUnitRuntimeSessionFactory())
        {
            using (var session = factory.Create(manifest))
            {
                var handle = (NetfxNUnitSessionHandle)session;
                Assert.That(handle.GetLoadedFrameworkAssembly().Location, Is.Not.Empty);
            }

            Assert.That(factory.RetainedGenerationCount, Is.EqualTo(1));
            var diagnostic = factory.CreateRetainedDiagnostic();
            Assert.That(diagnostic.Code, Is.EqualTo("generation.retained"));
            Assert.That(diagnostic.Message, Does.Contain("1"));
        }
    }

    [Fact]
    public void ResolveAssembly_binds_shared_core_from_host_appdomain()
    {
        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        using var factory = new NetfxNUnitRuntimeSessionFactory();
        using var session = factory.Create(manifest);
        var handle = (NetfxNUnitSessionHandle)session;
        var runtimeAssembly = handle.GetLoadedRuntimeAssembly();
        var hostCore = typeof(ITestingRuntimeSession).Assembly;

        var coreReference = runtimeAssembly
            .GetReferencedAssemblies()
            .Single(reference =>
                string.Equals(reference.Name, hostCore.GetName().Name, StringComparison.OrdinalIgnoreCase));

        var resolved = AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly =>
                string.Equals(assembly.GetName().Name, coreReference.Name, StringComparison.OrdinalIgnoreCase));

        Assert.That(resolved, Is.SameAs(hostCore));
    }

    [Fact]
    public void ResolveAssembly_binds_shared_testing_abstractions_from_host_appdomain()
    {
        var hostAbstractions = typeof(TestingRunRequest).Assembly;
        var resolved = NetfxNUnitSharedAssemblyResolver.TryResolveFromAppDomain(hostAbstractions.GetName());

        Assert.That(resolved, Is.SameAs(hostAbstractions));
    }

    private static Assembly? FindTestHostNUnitAssembly() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "nunit.framework", StringComparison.OrdinalIgnoreCase)
                && !assembly.Location.Contains("Generations", StringComparison.OrdinalIgnoreCase)
                && !assembly.Location.Contains("ConflictingDefault", StringComparison.OrdinalIgnoreCase));

    private static (int ExitCode, string Output) RunGenerationProbe(string scenario)
    {
        var probePath = Path.Combine(
            NetFrameworkGenerationTestEnvironment.RepositoryRoot,
            "tests",
            "DevTools.NUnit.Host.NetFramework.Tests",
            "Fixtures",
            "NetFrameworkGenerationProbe",
            "bin",
            "Debug",
            "net48",
            "NetFrameworkGenerationProbe.exe");

        if (!File.Exists(probePath))
        {
            throw new FileNotFoundException(
                $"Generation probe was not built: {probePath}",
                probePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = probePath,
            Arguments = scenario,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start generation probe process.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        TestContext.WriteLine(output);
        if (!string.IsNullOrWhiteSpace(error))
            TestContext.WriteLine(error);

        return (process.ExitCode, output + error);
    }

    private static string ReadGenerationMarker(Assembly testAssembly)
    {
        const string markerTypeName = "DevTools.NUnit.Runtime.Fixtures.GenerationMarker";
        var markerType = testAssembly.GetType(markerTypeName, throwOnError: true)!;

        var getValue = markerType.GetMethod(
            "GetValue",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (getValue is not null)
            return (string)getValue.Invoke(null, null)!;

        var valueField = markerType.GetField("Value", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GenerationMarker.Value field was not found.");

        return valueField.GetRawConstantValue() as string
            ?? throw new InvalidOperationException("GenerationMarker.Value constant was not available.");
    }

    private static void AssertGenerationMarkerCasePasses(ITestingRuntimeSession session, NUnitGenerationManifest manifest)
    {
        var run = session.Run(
            CreateTestingRequest(
                Guid.NewGuid(),
                manifest.ShadowAssemblyPath,
                "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"),
            new NoOpEventSink(),
            CancellationToken.None);

        Assert.That(run.GenerationId, Is.EqualTo(manifest.GenerationId));
        Assert.That(run.Results.Single().Outcome, Is.EqualTo(TestingOutcomes.Passed));
    }

    private static Assembly LoadDependencyFromRecord(GenerationAssemblyResolutionRecord record) =>
        AppDomain.CurrentDomain.GetAssemblies().Single(assembly =>
            string.Equals(assembly.Location, record.ResolvedAssemblyLocation, StringComparison.OrdinalIgnoreCase));

    private static TestingCaseResult RunDependencyProbe(ITestingRuntimeSession session, NUnitGenerationManifest manifest)
    {
        var run = session.Run(
            CreateTestingRequest(
                Guid.NewGuid(),
                manifest.ShadowAssemblyPath,
                "<filter><test>DependencyConsumer.DependencyProbeFixture.DependencyBehavior_IsGenerationSpecific</test></filter>"),
            new NoOpEventSink(),
            CancellationToken.None);

        Assert.That(run.GenerationId, Is.EqualTo(manifest.GenerationId));
        return run.Results.Single();
    }

    private sealed class NoOpEventSink : ITestingRuntimeEventSink
    {
        public void Publish(TestingRuntimeEvent runtimeEvent)
        {
        }
    }

    private static TestingRunRequest CreateTestingRequest(Guid runId, string assemblyPath, string? filter) => new(
        1,
        runId,
        NUnitFramework.Id,
        new TestingAssemblyReference(assemblyPath, "net48", null),
        new TestingSelection([], filter),
        new Dictionary<string, string>());

    private sealed class NoOpTestingSink : ITestingEventSink
    {
        public void Publish(TestingEvent testingEvent)
        {
        }
    }
}
