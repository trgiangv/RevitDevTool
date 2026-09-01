using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Execution.Tests;

public sealed class PythonPackageStoresTests
{
    [Fact]
    public void AddExecutionServices_RegistersOneStorePerBackend()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostAppInfo, FakeHostAppInfo>();
        services.AddExecutionServices();

        var stores = services
            .Where(d => d.ServiceType == typeof(IPythonPackageStore))
            .Select(d => d.ImplementationType)
            .ToList();

        Assert.Contains(typeof(PixiPackageStore), stores);
        Assert.Contains(typeof(UvPackageStore), stores);
        Assert.Contains(typeof(PipPackageStore), stores);
        Assert.Equal(3, stores.Count);
    }

    [Fact]
    public void PyPiPackageList_ParsesWheelJson()
    {
        var packages = PyPiPackageList.Parse("""[{"name":"pytest","version":"9.1.1"}]""");
        var pytest = Assert.Single(packages);
        Assert.Equal(Marketplace.PyPi, pytest.Marketplace);
        Assert.Equal("pytest", pytest.PackageId);
        Assert.Equal("9.1.1", pytest.Version);
        Assert.True(pytest.IsProtected);
    }

    [Fact]
    public void PyPiPackageList_SkipsNamelessAndNonArray()
    {
        Assert.Empty(PyPiPackageList.Parse("""{"name":"pytest"}"""));
        Assert.Empty(PyPiPackageList.Parse("""[{"version":"1.0"}]"""));
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }
}
