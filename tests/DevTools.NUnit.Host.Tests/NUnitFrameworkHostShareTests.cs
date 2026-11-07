using DevTools.NUnit.Host.Tests.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;
using DevTools.Testing.Host.Runtime;
using DevTools.Testing.Transport;
using System.Diagnostics;
using System.Reflection;

namespace DevTools.NUnit.Host.Tests;

[TestClass]
public sealed class NUnitFrameworkHostShareTests
{
    private static string FixtureFramework =>
        Path.Combine(NUnitGenerationTestEnvironment.FixtureOutputDirectory, NUnitGenerationTestEnvironment.FrameworkFileName);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Same_file_version_reuses_the_shared_copy()
    {
        using var workspace = new TempDirectory();
        var copy = Path.Combine(workspace.Path, NUnitGenerationTestEnvironment.FrameworkFileName);
        File.Copy(FixtureFramework, copy);

        Assert.AreSame(
            NUnitFrameworkHostShare.GetOrLoadFromShadow(FixtureFramework, NUnitRuntimeTestEnvironment.HostName),
            NUnitFrameworkHostShare.GetOrLoadFromShadow(copy, NUnitRuntimeTestEnvironment.HostName));
    }

    [TestMethod]
    [DataRow("Revit")]
    [DataRow("Civil 3D")]
    public void Another_file_version_stops_with_the_sentence_naming_the_host(string hostName)
    {
        using var workspace = new TempDirectory();
        var patchedPath = Path.Combine(workspace.Path, NUnitGenerationTestEnvironment.FrameworkFileName);
        File.Copy(FixtureFramework, patchedPath);
        var patched = NUnitGenerationTestEnvironment.PatchFileVersion(patchedPath);
        NUnitFrameworkHostShare.GetOrLoadFromShadow(FixtureFramework, hostName);

        var stop = Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            NUnitFrameworkHostShare.GetOrLoadFromShadow(patchedPath, hostName));

        Assert.AreEqual(
            $"{hostName} has NUnit {Display(FileVersionInfo.GetVersionInfo(FixtureFramework).FileVersion!)} loaded from an earlier run. "
            + $"Restart {hostName} to run tests with NUnit {Display(patched)}.",
            stop.Message);
    }

    [TestMethod]
    public void A_foreign_copy_with_the_same_identity_and_another_file_version_is_not_reused()
    {
        using var workspace = new TempDirectory();
        var foreignPath = Path.Combine(workspace.Path, NUnitGenerationTestEnvironment.FrameworkFileName);
        File.Copy(FixtureFramework, foreignPath);
        var patched = NUnitGenerationTestEnvironment.PatchFileVersion(foreignPath);
        var foreign = Assembly.Load(File.ReadAllBytes(foreignPath));
        var identity = AssemblyName.GetAssemblyName(FixtureFramework);
        var fileVersion = FileVersionInfo.GetVersionInfo(FixtureFramework).FileVersion!;

        Assert.AreEqual(identity.FullName, foreign.FullName);
        Assert.IsFalse(NUnitFrameworkHostShare.TryFindSameCopy([foreign], identity, fileVersion, out _));
        Assert.IsTrue(NUnitFrameworkHostShare.TryFindSameCopy([foreign], identity, patched, out var same));
        Assert.AreSame(foreign, same);
    }

    [TestMethod]
    public void A_stop_keeps_the_factory_working_for_the_loaded_version()
    {
        using var workspace = new TempDirectory();
        var original = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Path, "original");
        var other = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Path, "other", output =>
            NUnitGenerationTestEnvironment.PatchFileVersion(Path.Combine(output, NUnitGenerationTestEnvironment.FrameworkFileName)));
        var builder = NUnitRuntimeTestEnvironment.CreateBuilder(NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot());
        var factory = NUnitRuntimeTestEnvironment.CreateFactory();
        var originalGeneration = builder.Build(original);
        using var first = factory.Create(originalGeneration);

        Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() => factory.Create(builder.Build(other)));

        using var second = factory.Create(originalGeneration);
        var response = second.Run(
            new TestRunRequest(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(originalGeneration.ShadowAssemblyPath),
                TestSelection.FromFrameworkFilter(
                    "filter-xml",
                    "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>")),
            NullTestEventSink.Instance,
            TestContext.CancellationToken);
        Assert.AreEqual(TestOutcomes.Passed, response.Results.Single().Outcome);
    }

    private static string Display(string fileVersion) => RuntimeManifest.Display(Version.Parse(fileVersion));

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DevTools.nunit." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); }
            catch { }
        }
    }
}
