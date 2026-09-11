using DevTools.Execution.Providers.IronPython;
using DevTools.Utilities;

namespace DevTools.Execution.Tests;

public sealed class PydevdInstallerTests
{
    [Fact]
    public void ExtractRoot_IsAppDataPydevdLayout()
    {
        var expected = Path.Combine(
            AppUtils.GetApplicationDataPath(),
            "pydevd",
            "PyDev.Debugger-pydev_debugger_2_8_0");

        Assert.Equal(expected, PydevdInstaller.ExtractRoot);
        Assert.Equal(Path.Combine(expected, "pydevd.py"), PydevdInstaller.PydevdPyPath);
        Assert.Equal("2.8.0", PydevdInstaller.Version);
    }

    [Fact]
    public void VersionMarker_MatchesWhenInstalled()
    {
        if (!PydevdInstaller.IsInstalled())
            Assert.Skip("pydevd 2.8.0 extract is not on disk.");

        Assert.True(File.Exists(PydevdInstaller.PydevdPyPath));
        Assert.Equal(
            PydevdInstaller.Version,
            File.ReadAllText(PydevdInstaller.VersionMarkerPath).Trim());
    }

    [Fact]
    public async Task EnsureInstalled_SkipsWhenNetworkThrows()
    {
        if (PydevdInstaller.IsInstalled())
        {
            Assert.True(File.Exists(PydevdInstaller.PydevdPyPath));
            return;
        }

        try
        {
            await PydevdInstaller.EnsureInstalledAsync(cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            Assert.Skip($"pydevd download failed: {ex.Message}");
        }

        Assert.True(PydevdInstaller.IsInstalled());
        Assert.Equal(
            PydevdInstaller.Version,
            File.ReadAllText(PydevdInstaller.VersionMarkerPath).Trim());
    }
}
