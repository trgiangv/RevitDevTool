using DevTools.Execution.Providers.IronPython;
using DevTools.Utilities;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PydevdInstallerTests
{
    [TestMethod]
    public void ExtractRoot_IsAppDataPydevdLayout()
    {
        var expected = Path.Combine(
            AppUtils.GetApplicationDataPath(),
            "pydevd",
            "PyDev.Debugger-pydev_debugger_2_8_0");

        Assert.AreEqual(expected, PydevdInstaller.ExtractRoot);
        Assert.AreEqual(Path.Combine(expected, "pydevd.py"), PydevdInstaller.PydevdPyPath);
#pragma warning disable MSTEST0032 // Version is a public const; test documents the pinned layout contract.
        Assert.AreEqual("2.8.0", PydevdInstaller.Version);
#pragma warning restore MSTEST0032
    }

    [TestMethod]
    public void Install_writes_version_marker()
    {
        Pydevd.EnsureInstalled();

        Assert.IsTrue(PydevdInstaller.IsInstalled());
        Assert.IsTrue(File.Exists(PydevdInstaller.PydevdPyPath));
        Assert.AreEqual(
            PydevdInstaller.Version,
            File.ReadAllText(PydevdInstaller.VersionMarkerPath).Trim());
    }
}
