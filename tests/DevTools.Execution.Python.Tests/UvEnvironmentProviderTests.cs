using DevTools.Execution.Providers.Python;
using DevTools.Utilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class UvEnvironmentProviderTests
{
    [TestMethod]
    public void UvEnvRoot_IsUnderApplicationData()
    {
        var expected = Path.Combine(AppUtils.GetApplicationDataPath(), "uv-env");
        Assert.AreEqual(expected, UvEnvironmentProvider.UvEnvRoot);
        Assert.AreEqual(Path.Combine(expected, "uv-python"), UvEnvironmentProvider.UvPythonInstallDir);
        Assert.AreEqual(Path.Combine(expected, "uv-cache"), UvEnvironmentProvider.UvCacheDir);
    }

    [TestMethod]
    public void BoundEnvDir_KeysToProbedHostVersion()
    {
        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => "3.13");

        Assert.AreEqual("3.13", provider.BoundPythonVersion);
        Assert.AreEqual(
            Path.Combine(UvEnvironmentProvider.UvEnvRoot, "3.13"),
            provider.BoundEnvDir);
        Assert.AreEqual(
            Path.Combine(provider.BoundEnvDir, "Scripts", "python.exe"),
            provider.PythonExe);
    }

    [TestMethod]
    public void BoundPythonVersion_ProbesHostOnlyOnce()
    {
        var probeCount = 0;
        var provider = new UvEnvironmentProvider(
            NullLogger<UvEnvironmentProvider>.Instance,
            () =>
            {
                probeCount++;
                return "3.13";
            });

        _ = provider.BoundPythonVersion;
        _ = provider.BoundPythonVersion;
        _ = provider.BoundPythonVersion;

        Assert.AreEqual(1, probeCount);
    }

    [TestMethod]
    public async Task SetupEnvironmentAsync_WithoutHostInterpreter_Throws()
    {
        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => null);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(provider.SetupEnvironmentAsync);
    }

    [TestMethod]
    public void PythonExe_EmptyWhenNoHostInterpreter()
    {
        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => null);
        Assert.AreEqual(string.Empty, provider.PythonExe);
        Assert.AreEqual(string.Empty, provider.BoundEnvDir);
        Assert.AreEqual(string.Empty, provider.SitePackagesDir);
        Assert.IsFalse(provider.IsEnvironmentReady());
    }

    [TestMethod]
    [DataRow("3")]
    [DataRow("3.13.2")]
    public void BoundPythonVersion_NullWhenProbeReturnsMalformedVersion(string malformed)
    {
        var provider = new UvEnvironmentProvider(
            NullLogger<UvEnvironmentProvider>.Instance,
            () => malformed);

        Assert.IsNull(provider.BoundPythonVersion);
        Assert.AreEqual(string.Empty, provider.BoundEnvDir);
    }
}
