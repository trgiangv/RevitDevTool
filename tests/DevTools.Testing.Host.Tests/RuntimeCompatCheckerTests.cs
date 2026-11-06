using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;
using DevTools.Testing.Host.TUnit;
using DevTools.Testing.Transport;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class RuntimeCompatCheckerTests
{
    private static readonly Version Built = new(5, 1, 0, 0);

    [TestMethod]
    public void No_manifest_names_the_adapter_update()
    {
        using var output = new OutputDirectory();

        Assert.AreEqual(
            $"RevitDevTool.TestAdapter {output.AdapterVersion} is not compatible with RevitDevTool {TestingProtocol.Sender.Version}. {TestingProtocol.UpdateAdapterMessage}",
            Check(output));
    }

    [TestMethod]
    public void Malformed_manifest_reads_as_no_manifest()
    {
        using var output = new OutputDirectory();
        File.WriteAllText(output.ManifestPath, "{ not json");

        Assert.Contains("is not compatible with RevitDevTool", Check(output), StringComparison.Ordinal);
    }

    [TestMethod]
    public void Manifest_of_another_framework_reads_as_no_manifest()
    {
        using var output = new OutputDirectory();
        output.WriteManifest(framework: "TUnit");

        Assert.Contains("is not compatible with RevitDevTool", Check(output), StringComparison.Ordinal);
    }

    [TestMethod]
    public void Adapter_above_the_range_says_to_update_RevitDevTool()
    {
        using var output = new OutputDirectory();
        output.WriteManifest(contract: new ProtocolRange(TestingRuntimeContract.Current + 2, TestingRuntimeContract.Current + 1));

        Assert.AreEqual(
            $"RevitDevTool.TestAdapter {output.AdapterVersion} is not compatible with RevitDevTool {TestingProtocol.Sender.Version}. {TestingProtocol.UpdateProductMessage}",
            Check(output));
    }

    [TestMethod]
    public void Other_major_names_the_adapter_for_that_major()
    {
        using var output = new OutputDirectory();
        output.WriteManifest();
        var found = new Version(Built.Major + 1, 0, 0, 0);
        output.WriteFramework(found);

        Assert.AreEqual(
            $"NUnit {found.ToString(3)} is not supported by RevitDevTool.TestAdapter {output.AdapterVersion} (built for NUnit {Built.Major}.x). Update RevitDevTool.TestAdapter to a version for NUnit {found.Major}.",
            Check(output));
    }

    [TestMethod]
    public void Below_the_floor_names_the_floor()
    {
        using var output = new OutputDirectory();
        output.WriteManifest();
        var found = new Version(Built.Major, Built.Minor - 1, 0, 0);
        output.WriteFramework(found);

        Assert.AreEqual(
            $"NUnit {found.ToString(3)} is older than the {Built.ToString(3)} that RevitDevTool.TestAdapter {output.AdapterVersion} was built against. Update NUnit to {Built.ToString(3)} or later.",
            Check(output));
    }

    [TestMethod]
    public void Comparison_and_display_keep_a_non_zero_fourth_part()
    {
        using var output = new OutputDirectory();
        var floor = new Version(Built.Major, Built.Minor, Built.Build, 2);
        var found = new Version(Built.Major, Built.Minor, Built.Build, 1);
        output.WriteManifest(version: floor.ToString());
        output.WriteFramework(found);

        Assert.AreEqual(
            $"NUnit {found} is older than the {floor} that RevitDevTool.TestAdapter {output.AdapterVersion} was built against. Update NUnit to {floor} or later.",
            Check(output));
    }

    [TestMethod]
    public void Floor_and_newer_in_the_major_pass()
    {
        using var output = new OutputDirectory();
        output.WriteManifest();

        output.WriteFramework(Built);
        Assert.AreEqual(Built.ToString(), RuntimeCompatChecker.Check(TestFrameworkId.NUnit, output.Path).Version);

        output.WriteFramework(new Version(Built.Major, Built.Minor + 1, 0, 0));
        Assert.AreEqual(Built.ToString(), RuntimeCompatChecker.Check(TestFrameworkId.NUnit, output.Path).Version);
    }

    [TestMethod]
    public void Missing_framework_is_not_a_compatibility_stop()
    {
        using var output = new OutputDirectory();
        output.WriteManifest();

        Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            RuntimeCompatChecker.Check(TestFrameworkId.NUnit, output.Path));
    }

    [TestMethod]
    public void Bind_failure_sentence_is_exact()
    {
        var found = new Version(Built.Major, Built.Minor + 1, 0, 0);

        Assert.AreEqual(
            $"NUnit {found.ToString(3)} changed an API that RevitDevTool.TestAdapter 1.2.3 uses (built against {Built.ToString(3)}). Update RevitDevTool.TestAdapter, or use NUnit {Built.ToString(3)}.",
            RuntimeBindFailure.TryExplain(
                new MissingMethodException("Method not found: 'Void NUnit.Framework.Internal.Foo.Bar()'."),
                Manifest("NUnit", "nunit.framework"),
                found,
                "1.2.3"));
    }

    [TestMethod]
    [DataRow("NUnit", "nunit.framework", "Method not found: 'Void NUnit.Framework.Internal.Foo.Bar()'.")]
    [DataRow("TUnit", "TUnit.Core", "'TUnit.Engine.Framework.TUnitServiceProvider..ctor' was not found in TUnit.Engine.")]
    [DataRow("MSTest", "MSTest.TestFramework", "Could not load type 'X' from assembly 'MSTestAdapter.PlatformServices, Version=1.0.0.0'.")]
    [DataRow("MSTest", "MSTest.TestFramework", "Method not found: 'Void Microsoft.VisualStudio.TestTools.UnitTesting.Foo.Bar()'.")]
    [DataRow("TUnit", "TUnit.Core", "Could not load type 'X' from assembly 'Microsoft.Testing.Platform, Version=1.0.0.0'.")]
    public void Failures_naming_the_framework_or_platform_map(string framework, string assembly, string message)
    {
        foreach (var failure in new Exception[]
                 {
                     new MissingMethodException(message),
                     new MissingFieldException(message),
                     new MissingMemberException(message),
                     new TypeLoadException(message),
                     new FileNotFoundException(message),
                     new FileLoadException(message),
                 })
        {
            Assert.IsNotNull(Explain(failure, framework, assembly), failure.GetType().Name);
        }
    }

    [TestMethod]
    public void Wrapped_failures_are_unwrapped()
    {
        var inner = new MissingMemberException("'NUnit.Framework.TestContext.DefaultWorkDirectory' was not found in nunit.framework.");
        Assert.IsNotNull(Explain(new TargetInvocationException(new TargetInvocationException(inner)), "NUnit", "nunit.framework"));

        var typeInitializer = new TypeInitializationException("Some.Type", inner);
        Assert.IsNotNull(Explain(typeInitializer, "NUnit", "nunit.framework"));
    }

    [TestMethod]
    public void Assembly_name_in_the_file_name_alone_maps()
    {
        var failure = new FileNotFoundException("Could not load the file.", "nunit.framework, Version=1.0.0.0");

        Assert.IsNotNull(Explain(failure, "NUnit", "nunit.framework"));
    }

    [TestMethod]
    [DataRow("Could not load type 'MyTests.Helper' from assembly 'MyTests, Version=1.0.0.0'.")]
    [DataRow("Could not load type 'Autodesk.Revit.DB.Foo' from assembly 'RevitAPI, Version=25.0.0.0'.")]
    [DataRow("Could not load type 'X' from assembly 'MyNUnit.Helpers, Version=1.0.0.0'.")]
    [DataRow("Could not load type 'X' from assembly 'Contoso.NUnit.Helpers, Version=1.0.0.0'.")]
    public void Type_load_of_a_user_or_revit_assembly_stays_out(string message)
    {
        Assert.IsNull(Explain(new TypeLoadException(message), "NUnit", "nunit.framework"));
    }

    [TestMethod]
    public void File_not_found_of_a_user_or_revit_assembly_stays_out()
    {
        Assert.IsNull(Explain(new FileNotFoundException("Could not load file or assembly 'RevitAPI'.", "RevitAPI"), "NUnit", "nunit.framework"));
        Assert.IsNull(Explain(new FileNotFoundException("Could not load file or assembly 'MyTests.Data'.", "MyTests.Data"), "NUnit", "nunit.framework"));
    }

    [TestMethod]
    public void Other_exceptions_stay_out_even_when_they_name_the_framework()
    {
        Assert.IsNull(Explain(new InvalidOperationException("NUnit.Framework.Internal failed."), "NUnit", "nunit.framework"));
    }

    [TestMethod]
    public void Current_path_keeps_the_add_in_runtime_over_one_in_the_user_output()
    {
        using var output = new OutputDirectory();
        using var addin = new OutputDirectory();
        var testAssembly = Path.Combine(output.Path, "Consumer.dll");
        WriteAssembly(testAssembly, "Consumer", new Version(1, 0, 0, 0));
        WriteAssembly(Path.Combine(output.Path, "TUnit.Core.dll"), "TUnit.Core", TUnitGenerationPolicy.ExpectedTUnitAssemblyVersion);
        WriteAssembly(Path.Combine(output.Path, TUnitGenerationPolicy.RuntimeAssemblyFileName), "DevTools.TUnit.Runtime", new Version(9, 9, 0, 0));
        File.WriteAllText(Path.Combine(output.Path, RuntimeManifest.FileName(TestFrameworkId.TUnit)), "{}");
        var addinRuntime = Path.Combine(addin.Path, TUnitGenerationPolicy.RuntimeAssemblyFileName);
        WriteAssembly(addinRuntime, "DevTools.TUnit.Runtime", new Version(1, 0, 0, 0));
        var policy = new TUnitGenerationPolicy(() => new RuntimeSource(addinRuntime, null, []));

        var plan = policy.CreatePlan(testAssembly);

        var runtime = plan.Files.Single(file => file.RelativePath == plan.RuntimeAssemblyRelativePath);
        Assert.AreEqual(addinRuntime, runtime.SourcePath);
    }

    private static string? Explain(Exception failure, string framework, string assembly) =>
        RuntimeBindFailure.TryExplain(failure, Manifest(framework, assembly), new Version(1, 0, 0, 0), "1.2.3");

    private static RuntimeManifest Manifest(string framework, string assembly) =>
        new(framework, "Entry.Type", TestingProtocol.RuntimeRange, assembly, Built.ToString());

    private static string Check(OutputDirectory output) =>
        Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            RuntimeCompatChecker.Check(TestFrameworkId.NUnit, output.Path)).Message;

    private static void WriteAssembly(string path, string name, Version version)
    {
        var builder = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = version }, typeof(object).Assembly);
        builder.DefineDynamicModule(name).DefineType("Marker", TypeAttributes.Public).CreateType();
        builder.Save(path);
    }

    private sealed class OutputDirectory : IDisposable
    {
        public OutputDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"runtime-compat-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            File.Copy(
                typeof(TestingRequestHandler).Assembly.Location,
                System.IO.Path.Combine(Path, "DevTools.TestAdapter.dll"));
        }

        public string Path { get; }

        public string ManifestPath => System.IO.Path.Combine(Path, RuntimeManifest.FileName(TestFrameworkId.NUnit));

        public string AdapterVersion => RuntimeManifest.ReadAdapterVersion(Path);

        public void WriteManifest(string framework = "NUnit", ProtocolRange? contract = null, string? version = null) =>
            File.WriteAllText(
                ManifestPath,
                JsonSerializer.Serialize(
                    new RuntimeManifest(
                        framework,
                        "DevTools.NUnit.Runtime.NUnitRuntimeSession",
                        contract ?? TestingProtocol.RuntimeRange,
                        "nunit.framework",
                        version ?? Built.ToString()),
                    TestingJsonContext.Default.RuntimeManifest));

        public void WriteFramework(Version version) =>
            WriteAssembly(System.IO.Path.Combine(Path, "nunit.framework.dll"), "nunit.framework", version);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
