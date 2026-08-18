using System.Reflection;
using System.Runtime.InteropServices;
using DevTools.NUnit.Host.Loading;
using DevTools.NUnit.Host.NetFramework.Tests;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.NUnit.Host.NetFramework.Tests.Probe;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: NetFrameworkGenerationProbe <scenario>");
            return 10;
        }

        Console.WriteLine($"CLRVersion: {Environment.Version}");
        Console.WriteLine($"FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"TargetFrameworkName: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}");

        try
        {
            return args[0] switch
            {
                "conflicting-binding" => RunConflictingBinding(),
                "concurrent-binding" => RunConcurrentBinding(),
                _ => 11,
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 99;
        }
    }

    private static int RunConflictingBinding()
    {
        var conflicting = NetFrameworkGenerationTestEnvironment.LoadConflictingNUnitIntoAppDomain();
        if (!string.Equals(conflicting.GetName().Name, "nunit.framework", StringComparison.OrdinalIgnoreCase))
            return 1;

        var testHostNunit = FindLoadedNUnit(new Version(4, 6, 0, 0));
        if (testHostNunit is not null)
            return 2;

        var manifest = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        if (conflicting.Location.StartsWith(manifest.ShadowDirectory, StringComparison.OrdinalIgnoreCase))
            return 3;

        var factory = new NUnitRuntimeSessionFactory();
        using var session = factory.Create(manifest);
        var handle = (NUnitRuntimeSessionHandle)session;

        var run = session.Run(
            CreateRequest(
                Guid.NewGuid(),
                manifest.ShadowAssemblyPath,
                "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"),
            new NoOpEventSink(),
            CancellationToken.None);

        if (!string.Equals(run.GenerationId, manifest.GenerationId, StringComparison.Ordinal))
            return 4;

        if (!string.Equals(run.Results.Single().Outcome, TestingOutcomes.Passed, StringComparison.Ordinal))
            return 5;

        var generationFrameworkIdentity = handle.FrameworkAssemblyIdentityForTesting;
        var expectedFrameworkIdentity = AssemblyName.GetAssemblyName(
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest)).FullName;

        Console.WriteLine($"ConflictingLocation={conflicting.Location}");
        Console.WriteLine($"GenerationFrameworkIdentity={generationFrameworkIdentity}");

        if (!string.Equals(generationFrameworkIdentity, expectedFrameworkIdentity, StringComparison.OrdinalIgnoreCase))
            return 6;

        return 0;
    }

    private static int RunConcurrentBinding()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationTwo();

        var factory = new NUnitRuntimeSessionFactory();
        ITestingRuntimeSession? sessionOne = null;
        ITestingRuntimeSession? sessionTwo = null;

        var createOne = System.Threading.Tasks.Task.Factory.StartNew(() => sessionOne = factory.Create(generationOne));
        var createTwo = System.Threading.Tasks.Task.Factory.StartNew(() => sessionTwo = factory.Create(generationTwo));
        System.Threading.Tasks.Task.WaitAll(createOne, createTwo);

        if (sessionOne is null || sessionTwo is null)
            return 1;

        var handleOne = (NUnitRuntimeSessionHandle)sessionOne;
        var handleTwo = (NUnitRuntimeSessionHandle)sessionTwo;

        try
        {
            if (!string.Equals(handleOne.FrameworkAssemblyIdentityForTesting, AssemblyName.GetAssemblyName(NUnitGenerationPolicy.GetFrameworkAssemblyPath(generationOne)).FullName, StringComparison.OrdinalIgnoreCase))
                return 2;

            if (!string.Equals(handleTwo.FrameworkAssemblyIdentityForTesting, AssemblyName.GetAssemblyName(NUnitGenerationPolicy.GetFrameworkAssemblyPath(generationTwo)).FullName, StringComparison.OrdinalIgnoreCase))
                return 3;

            Console.WriteLine($"GenerationOneFramework={handleOne.FrameworkAssemblyIdentityForTesting}");
            Console.WriteLine($"GenerationTwoFramework={handleTwo.FrameworkAssemblyIdentityForTesting}");

            return 0;
        }
        finally
        {
            sessionOne.Dispose();
            sessionTwo.Dispose();
        }
    }

    private static Assembly? FindLoadedNUnit(Version version)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName();
            if (!string.Equals(name.Name, "nunit.framework", StringComparison.OrdinalIgnoreCase))
                continue;

            if (name.Version == version)
                return assembly;
        }

        return null;
    }

    private static TestingRunRequest CreateRequest(Guid runId, string assemblyPath, string? filter) => new(
        1, runId, "nunit", new TestingAssemblyReference(assemblyPath, "net48", null),
        new TestingSelection([], filter), new Dictionary<string, string>());

    private sealed class NoOpEventSink : ITestingRuntimeEventSink
    {
        public void Publish(TestingRuntimeEvent runtimeEvent)
        {
        }
    }
}
