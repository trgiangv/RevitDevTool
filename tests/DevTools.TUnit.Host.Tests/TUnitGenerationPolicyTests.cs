using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.TUnit;
using DevTools.Testing.Transport;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace DevTools.TUnit.Host.Tests;

[TestClass]
public sealed class TUnitGenerationPolicyTests
{
    private static readonly Version Floor = new(1, 10, 0, 0);

    [TestMethod]
    public void Plan_takes_the_runtime_and_framework_from_the_output_only()
    {
        using var output = new OutputDirectory(new Version(1, 11, 0, 0));

        var plan = new TUnitGenerationPolicy().CreatePlan(output.ConsumerPath);

        Assert.AreEqual("DevTools.TUnit.Runtime.dll", plan.RuntimeAssemblyRelativePath);
        foreach (var file in plan.Files)
            StringAssert.StartsWith(file.SourcePath, output.Path, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Framework_below_the_floor_stops_with_the_sentence()
    {
        using var output = new OutputDirectory(new Version(1, 9, 0, 0));

        var stop = Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            new TUnitGenerationPolicy().CreatePlan(output.ConsumerPath));

        Assert.AreEqual(
            $"TUnit 1.9.0 is older than the 1.10.0 that RevitDevTool.TestAdapter {RuntimeManifest.ReadAdapterVersion(output.Path)} was built against. Update TUnit to 1.10.0 or later.",
            stop.Message);
    }

    private sealed class OutputDirectory : IDisposable
    {
        public OutputDirectory(Version framework)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tunit-policy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            ConsumerPath = System.IO.Path.Combine(Path, "Consumer.dll");
            WriteAssembly(ConsumerPath, "Consumer", new Version(1, 0, 0, 0));
            WriteAssembly(System.IO.Path.Combine(Path, "TUnit.Core.dll"), "TUnit.Core", framework);
            WriteAssembly(System.IO.Path.Combine(Path, "DevTools.TUnit.Runtime.dll"), "DevTools.TUnit.Runtime", new Version(1, 0, 0, 0));
            File.Copy(typeof(TUnitGenerationPolicy).Assembly.Location, System.IO.Path.Combine(Path, "DevTools.TestAdapter.dll"));
            File.WriteAllText(
                System.IO.Path.Combine(Path, RuntimeManifest.FileName(TestFrameworkId.TUnit)),
                JsonSerializer.Serialize(
                    new RuntimeManifest(
                        "TUnit",
                        "DevTools.TUnit.Runtime.TUnitRuntimeSession",
                        TestingProtocol.RuntimeRange,
                        "TUnit.Core",
                        Floor.ToString()),
                    TestingJsonContext.Default.RuntimeManifest));
        }

        public string Path { get; }

        public string ConsumerPath { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);

        private static void WriteAssembly(string path, string name, Version version)
        {
            var builder = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = version }, typeof(object).Assembly);
            builder.DefineDynamicModule(name).DefineType("Marker", TypeAttributes.Public).CreateType();
            builder.Save(path);
        }
    }
}
