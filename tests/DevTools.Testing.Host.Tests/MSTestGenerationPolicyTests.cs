using System.Reflection;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.MSTest;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class MSTestGenerationPolicyTests
{
    [TestMethod]
    public void Policy_pins_mstest_and_mtp_assembly_versions()
    {
        ValidatePin("MSTest.TestFramework.dll",
            FindRuntimeAssembly("MSTest.TestFramework.dll", MSTestGenerationPolicy.ExpectedMSTestAssemblyVersion));
        ValidatePin("MSTest.TestAdapter.dll",
            FindRuntimeAssembly("MSTest.TestAdapter.dll", MSTestGenerationPolicy.ExpectedMSTestAssemblyVersion));
        ValidatePin("MSTestAdapter.PlatformServices.dll",
            FindRuntimeAssembly("MSTestAdapter.PlatformServices.dll", MSTestGenerationPolicy.ExpectedMSTestAssemblyVersion));
        ValidatePin("Microsoft.Testing.Platform.dll",
            FindRuntimeAssembly("Microsoft.Testing.Platform.dll", MSTestGenerationPolicy.ExpectedMtpAssemblyVersion));

        var framework = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            ValidatePin("MSTest.TestFramework.dll", typeof(MSTestGenerationPolicyTests).Assembly.Location));
        Assert.Contains("4.4.1.0", framework.Message, StringComparison.Ordinal);

        var adapter = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            ValidatePin("MSTest.TestAdapter.dll", typeof(MSTestGenerationPolicyTests).Assembly.Location));
        Assert.Contains("4.4.1.0", adapter.Message, StringComparison.Ordinal);

        var platformServices = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            ValidatePin("MSTestAdapter.PlatformServices.dll", typeof(MSTestGenerationPolicyTests).Assembly.Location));
        Assert.Contains("4.4.1.0", platformServices.Message, StringComparison.Ordinal);

        var mtp = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            ValidatePin("Microsoft.Testing.Platform.dll", typeof(MSTestGenerationPolicyTests).Assembly.Location));
        Assert.Contains("2.4.1.0", mtp.Message, StringComparison.Ordinal);
    }

    private static void ValidatePin(string fileName, string path) =>
        MSTestGenerationPolicy.Spec.Pins.Single(pin => pin.FileName == fileName).Validate(path, null);

    private static string FindRuntimeAssembly(string fileName, Version expected)
    {
        var bin = Path.Combine(FindRepositoryRoot(), "source", "DevTools.MSTest.Runtime", "bin");
        if (!Directory.Exists(bin))
            Assert.Fail($"MSTest runtime output was not built: {bin}");

        foreach (var path in Directory.EnumerateFiles(bin, fileName, SearchOption.AllDirectories))
        {
            if (AssemblyName.GetAssemblyName(path).Version == expected)
                return path;
        }

        Assert.Fail($"Did not find {fileName} {expected} under {bin}.");
        return string.Empty;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
