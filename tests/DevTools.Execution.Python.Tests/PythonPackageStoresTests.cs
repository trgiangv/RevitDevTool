using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonPackageStoresTests
{
    [TestMethod]
    public void AddExecutionServices_RegistersOneStorePerBackend()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostAppInfo, FakeHostAppInfo>();
        services.AddExecutionServices();

        var stores = services
            .Where(d => d.ServiceType == typeof(IPythonPackageStore))
            .Select(d => d.ImplementationType)
            .ToList();

        Assert.IsTrue(stores.Contains(typeof(PixiPackageStore)));
        Assert.IsTrue(stores.Contains(typeof(UvPackageStore)));
        Assert.IsTrue(stores.Contains(typeof(PipPackageStore)));
        Assert.AreEqual(3, stores.Count);
    }

    [TestMethod]
    public void PyPiPackageList_ParsesWheelJson()
    {
        var packages = PyPiPackageList.Parse("""[{"name":"pytest","version":"9.1.1"}]""");
        Assert.AreEqual(1, packages.Count);
        var pytest = packages[0];
        Assert.AreEqual(Marketplace.PyPi, pytest.Marketplace);
        Assert.AreEqual("pytest", pytest.PackageId);
        Assert.AreEqual("9.1.1", pytest.Version);
        Assert.IsTrue(pytest.IsProtected);
    }

    [TestMethod]
    public void PyPiPackageList_SkipsNamelessAndNonArray()
    {
        Assert.IsEmpty(PyPiPackageList.Parse("""{"name":"pytest"}"""));
        Assert.IsEmpty(PyPiPackageList.Parse("""[{"version":"1.0"}]"""));
    }

    [TestMethod]
    public void PyPiPackageList_AllowsMissingVersion()
    {
        var packages = PyPiPackageList.Parse("""[{"name":"leftpad"}]""");
        Assert.AreEqual(1, packages.Count);
        var package = packages[0];
        Assert.AreEqual("leftpad", package.PackageId);
        Assert.IsNull(package.Version);
        Assert.IsFalse(package.IsProtected);
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }
}
