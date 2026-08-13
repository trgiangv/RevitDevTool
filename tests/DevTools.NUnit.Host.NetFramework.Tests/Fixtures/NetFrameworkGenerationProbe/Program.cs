using System.Reflection;
using System.Runtime.InteropServices;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Core.Runtime;
using DevTools.NUnit.Host.Loading;
using DevTools.NUnit.Host.NetFramework.Tests;

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

        using var factory = new NetfxNUnitRuntimeSessionFactory();
        using var session = factory.Create(manifest);
        var handle = (NetfxNUnitSessionHandle)session;

        var run = session.Run(
            new NUnitRunRequest(
                Guid.NewGuid(),
                manifest.ShadowAssemblyPath,
                "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"),
            new NoOpEventSink(),
            CancellationToken.None);

        if (!string.Equals(run.GenerationId, manifest.GenerationId, StringComparison.Ordinal))
            return 4;

        if (!string.Equals(run.Cases.Single().Outcome, NUnitOutcomes.Passed, StringComparison.Ordinal))
            return 5;

        var generationFramework = handle.GetLoadedFrameworkAssembly();
        var binding = handle.GetRunnerBindingDiagnostic();

        Console.WriteLine($"ConflictingLocation={conflicting.Location}");
        Console.WriteLine($"GenerationFrameworkLocation={generationFramework.Location}");
        Console.WriteLine($"RunnerLocation={binding.RunnerAssembly.Location}");

        if (ReferenceEquals(generationFramework, conflicting))
            return 6;

        if (!string.Equals(
                generationFramework.Location,
                manifest.FrameworkAssemblyPath,
                StringComparison.OrdinalIgnoreCase))
            return 7;

        if (!ReferenceEquals(binding.GenerationFrameworkAssembly, generationFramework))
            return 8;

        if (!ReferenceEquals(binding.RunnerAssembly, generationFramework))
            return 9;

        if (!string.Equals(
                binding.RunnerAssembly.Location,
                generationFramework.Location,
                StringComparison.OrdinalIgnoreCase))
            return 10;

        if (!binding.RunnerAssembly.Location.StartsWith(manifest.ShadowDirectory, StringComparison.OrdinalIgnoreCase))
            return 11;

        return 0;
    }

    private static int RunConcurrentBinding()
    {
        var generationOne = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationOne();
        var generationTwo = NetFrameworkGenerationTestEnvironment.BuildFixtureGenerationTwo();

        using var factory = new NetfxNUnitRuntimeSessionFactory();
        INUnitRuntimeSession? sessionOne = null;
        INUnitRuntimeSession? sessionTwo = null;

        var createOne = System.Threading.Tasks.Task.Factory.StartNew(() => sessionOne = factory.Create(generationOne));
        var createTwo = System.Threading.Tasks.Task.Factory.StartNew(() => sessionTwo = factory.Create(generationTwo));
        System.Threading.Tasks.Task.WaitAll(createOne, createTwo);

        if (sessionOne is null || sessionTwo is null)
            return 1;

        var handleOne = (NetfxNUnitSessionHandle)sessionOne;
        var handleTwo = (NetfxNUnitSessionHandle)sessionTwo;

        try
        {
            if (!ReferenceEquals(
                    handleOne.GetRunnerBindingDiagnostic().RunnerAssembly,
                    handleOne.GetLoadedFrameworkAssembly()))
                return 2;

            if (!ReferenceEquals(
                    handleTwo.GetRunnerBindingDiagnostic().RunnerAssembly,
                    handleTwo.GetLoadedFrameworkAssembly()))
                return 3;

            Console.WriteLine($"GenerationOneFramework={handleOne.GetLoadedFrameworkAssembly().Location}");
            Console.WriteLine($"GenerationTwoFramework={handleTwo.GetLoadedFrameworkAssembly().Location}");
            Console.WriteLine($"GenerationOneRunner={handleOne.GetRunnerBindingDiagnostic().RunnerAssembly.Location}");
            Console.WriteLine($"GenerationTwoRunner={handleTwo.GetRunnerBindingDiagnostic().RunnerAssembly.Location}");

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

    private sealed class NoOpEventSink : INUnitRuntimeEventSink
    {
        public void Publish(NUnitRuntimeEvent runtimeEvent)
        {
        }
    }
}
