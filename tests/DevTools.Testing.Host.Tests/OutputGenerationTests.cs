using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Transport;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class OutputGenerationTests
{
    [TestMethod]
    [DataRow(TestFrameworkId.NUnit, "nunit.framework")]
    [DataRow(TestFrameworkId.TUnit, "TUnit.Core")]
    [DataRow(TestFrameworkId.MSTest, "MSTest.TestFramework")]
    public void Plan_uses_output_files_only(TestFrameworkId framework, string assembly)
    {
        var output = Path.Combine(Path.GetTempPath(), $"output-generation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        try
        {
            var runtime = $"DevTools.{framework}.Runtime";
            var consumer = Path.Combine(output, "Consumer.dll");
            WriteAssembly(consumer, "Consumer");
            WriteAssembly(Path.Combine(output, assembly + ".dll"), assembly);
            WriteAssembly(Path.Combine(output, runtime + ".dll"), runtime);
            File.Copy(typeof(OutputGeneration).Assembly.Location, Path.Combine(output, "DevTools.TestAdapter.dll"));
            File.WriteAllText(
                Path.Combine(output, RuntimeManifest.FileName(framework)),
                JsonSerializer.Serialize(
                    new RuntimeManifest(framework.ToString(), "Entry.Type", TestingProtocol.RuntimeRange, assembly, "1.0.0.0"),
                    TestingJsonContext.Default.RuntimeManifest));

            var plan = new OutputGeneration(framework).CreatePlan(consumer);

            Assert.AreEqual(framework, plan.FrameworkId);
            Assert.AreEqual(runtime + ".dll", plan.RuntimeAssemblyRelativePath);
            Assert.IsTrue(plan.Files.Any(file => file.RelativePath == assembly + ".dll"));
            CollectionAssert.AreEqual(
                new[] { output },
                plan.Files.Select(file => Path.GetDirectoryName(file.SourcePath)).Distinct().ToList());
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static void WriteAssembly(string path, string name)
    {
        var builder = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = new Version(1, 0, 0, 0) }, typeof(object).Assembly);
        builder.DefineDynamicModule(name).DefineType("Marker", TypeAttributes.Public).CreateType();
        builder.Save(path);
    }
}
