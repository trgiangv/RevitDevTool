using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using DevTools.NUnit.Host;
using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using NUnit.Framework;
using FactAttribute = Xunit.FactAttribute;

namespace DevTools.NUnit.Host.NetFramework.Tests;

public sealed class NetFrameworkGenerationTests
{
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
        Assert.That(probe.Output, Does.Contain("GenerationFrameworkIdentity="));
    }

    [Fact]
    public void Create_executes_two_generations_in_the_default_appdomain()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationTwo();
        Assert.That(generationTwo.GenerationId, Is.Not.EqualTo(generationOne.GenerationId));

        var factory = new NUnitRuntimeSessionFactory();
        using (var sessionOne = factory.Create(generationOne))
        {
            Assert.That(sessionOne.GenerationId, Is.EqualTo(generationOne.GenerationId));
            AssertGenerationMarkerCasePasses(sessionOne, generationOne);
        }

        using (var sessionTwo = factory.Create(generationTwo))
        {
            Assert.That(sessionTwo.GenerationId, Is.EqualTo(generationTwo.GenerationId));
            AssertGenerationMarkerCasePasses(sessionTwo, generationTwo);
        }
    }

    [Fact]
    public void Run_maps_the_source_assembly_request_to_the_loaded_shadow_generation()
    {
        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var factory = new NUnitRuntimeSessionFactory();
        using var session = factory.Create(manifest);

        var run = session.Run(
            CreateTestingRequest(
                Guid.NewGuid(),
                manifest.SourceAssemblyPath,
                "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"),
            new NoOpEventSink(),
            CancellationToken.None);

        Assert.That(run.Results.Single().Outcome, Is.EqualTo(TestingOutcomes.Passed));
    }

    [Fact]
    public void Create_resolves_same_identity_dependency_per_requesting_generation()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildDependencyGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildDependencyGenerationTwo();
        Assert.That(generationTwo.GenerationId, Is.Not.EqualTo(generationOne.GenerationId));

        var factory = new NUnitRuntimeSessionFactory();
        using var sessionOne = factory.Create(generationOne);
        using var sessionTwo = factory.Create(generationTwo);

        var caseOne = RunDependencyProbe(sessionOne, generationOne);
        Assert.That(caseOne.Outcome, Is.EqualTo(TestingOutcomes.Passed));
        Assert.That(caseOne.Output, Does.Contain("dependency-behavior=behavior-one"));

        var caseTwo = RunDependencyProbe(sessionTwo, generationTwo);
        Assert.That(caseTwo.Outcome, Is.EqualTo(TestingOutcomes.Passed));
        Assert.That(caseTwo.Output, Does.Contain("dependency-behavior=behavior-two"));
    }

    [Fact]
    public void Create_resolves_root_dependency_per_requesting_generation()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildRootDependencyGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildRootDependencyGenerationTwo();

        var factory = new NUnitRuntimeSessionFactory();
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

        var factory = new NUnitRuntimeSessionFactory();
        ITestingRuntimeSession? sessionOne = null;
        ITestingRuntimeSession? sessionTwo = null;

        var createOne = Task.Factory.StartNew(() => sessionOne = factory.Create(generationOne));
        var createTwo = Task.Factory.StartNew(() => sessionTwo = factory.Create(generationTwo));
        Task.WaitAll(createOne, createTwo);

        Assert.That(sessionOne, Is.Not.Null);
        Assert.That(sessionTwo, Is.Not.Null);

        try
        {
            Assert.That(sessionOne!.GenerationId, Is.EqualTo(generationOne.GenerationId));
            Assert.That(sessionTwo!.GenerationId, Is.EqualTo(generationTwo.GenerationId));
            AssertGenerationMarkerCasePasses(sessionOne, generationOne);
            AssertGenerationMarkerCasePasses(sessionTwo, generationTwo);
        }
        finally
        {
            sessionOne?.Dispose();
            sessionTwo?.Dispose();
        }

        var probe = RunGenerationProbe("concurrent-binding");
        Assert.That(probe.ExitCode, Is.EqualTo(0), probe.Output);
        Assert.That(probe.Output, Does.Contain("GenerationOneFramework="));
        Assert.That(probe.Output, Does.Contain("GenerationTwoFramework="));
    }

    [Fact]
    public void Dispose_unregisters_the_scoped_resolver_and_is_idempotent()
    {
        var factory = new NUnitRuntimeSessionFactory();
        var session = factory.Create(NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne());

        session.Dispose();
        session.Dispose();

        Assert.That(
            () => session.Run(
                CreateTestingRequest(
                    Guid.NewGuid(),
                    NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne().ShadowAssemblyPath,
                    null),
                new NoOpEventSink(),
                CancellationToken.None),
            Throws.TypeOf<ObjectDisposedException>());
    }

    [Fact]
    public void Create_binds_the_concrete_neutral_contract_identity()
    {
        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var factory = new NUnitRuntimeSessionFactory();
        using var session = factory.Create(manifest);
        var handle = (NUnitRuntimeSessionHandle)session;
        var runtimeContract = handle.GetLoadedRuntimeAssembly().GetReferencedAssemblies()
            .Single(reference => string.Equals(
                reference.Name,
                typeof(TestingRunRequest).Assembly.GetName().Name,
                StringComparison.OrdinalIgnoreCase));

        Assert.That(runtimeContract.FullName, Is.EqualTo(typeof(TestingRunRequest).Assembly.FullName));
        AssertGenerationMarkerCasePasses(session, manifest);
    }

    [Fact]
    public void Run_forwards_caller_cancellation_to_an_entered_test()
    {
        const string enteredEventVariable = "DEVTOOLS_NUNIT_CANCELLATION_ENTERED_EVENT";
        const string releaseEventVariable = "DEVTOOLS_NUNIT_CANCELLATION_RELEASE_EVENT";
        const string remainingEventVariable = "DEVTOOLS_NUNIT_CANCELLATION_REMAINING_EVENT";
        var previousEventName = Environment.GetEnvironmentVariable(enteredEventVariable);
        var previousReleaseEventName = Environment.GetEnvironmentVariable(releaseEventVariable);
        var previousRemainingEventName = Environment.GetEnvironmentVariable(remainingEventVariable);
        var enteredEventName = $"DevTools.NUnit.Cancellation.{Guid.NewGuid():N}";
        var releaseEventName = $"DevTools.NUnit.Cancellation.Release.{Guid.NewGuid():N}";
        var remainingEventName = $"DevTools.NUnit.Cancellation.Remaining.{Guid.NewGuid():N}";
        using var entered = new EventWaitHandle(false, EventResetMode.ManualReset, enteredEventName);
        using var release = new EventWaitHandle(false, EventResetMode.ManualReset, releaseEventName);
        using var remaining = new EventWaitHandle(false, EventResetMode.ManualReset, remainingEventName);
        using var cancellation = new CancellationTokenSource();
        var factory = new NUnitRuntimeSessionFactory();
        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        using var session = factory.Create(manifest);
        var request = CreateTestingRequest(
            Guid.NewGuid(),
            manifest.ShadowAssemblyPath,
            "<filter><test>DevTools.NUnit.Runtime.Fixtures.CancellationForwardingFixture</test></filter>");
        Task<TestingRunResponse>? run = null;

        try
        {
            Environment.SetEnvironmentVariable(enteredEventVariable, enteredEventName);
            Environment.SetEnvironmentVariable(releaseEventVariable, releaseEventName);
            Environment.SetEnvironmentVariable(remainingEventVariable, remainingEventName);
            run = Task.Factory.StartNew(() => session.Run(request, new NoOpEventSink(), cancellation.Token));

            Assert.That(entered.WaitOne(TimeSpan.FromSeconds(15)), Is.True, "The blocking test did not enter.");
            cancellation.Cancel();
            release.Set();

            Assert.That(run.Wait(TimeSpan.FromSeconds(15)), Is.True, "The cancelled run did not complete.");
            var response = run.GetAwaiter().GetResult();
            Assert.That(remaining.WaitOne(0), Is.False, "The remaining test body ran after cancellation.");
            Assert.That(response.Results, Has.None.Matches<TestingCaseResult>(testCase =>
                testCase.DisplayName == "RemainingTest_MustNotRunAfterCancellation"));
        }
        finally
        {
            cancellation.Cancel();
            release.Set();
            if (run is { IsCompleted: false })
            {
                session.Cancel(request.RunId);
                run.Wait(TimeSpan.FromSeconds(15));
            }

            Environment.SetEnvironmentVariable(enteredEventVariable, previousEventName);
            Environment.SetEnvironmentVariable(releaseEventVariable, previousReleaseEventName);
            Environment.SetEnvironmentVariable(remainingEventVariable, previousRemainingEventName);
        }
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

    private static void AssertGenerationMarkerCasePasses(ITestingRuntimeSession session, TestingGenerationManifest manifest)
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

    private static TestingCaseResult RunDependencyProbe(ITestingRuntimeSession session, TestingGenerationManifest manifest)
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
}
