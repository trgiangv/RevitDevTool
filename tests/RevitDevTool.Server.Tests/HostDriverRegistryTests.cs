using DevTools.Daemon.Contracts;
using DevTools.Daemon.Hosts;

namespace RevitDevTool.Server.Tests;

public sealed class HostDriverRegistryTests
{
    [Theory]
    [InlineData("model.rvt", typeof(RevitHostDriver))]
    [InlineData("family.rfa", typeof(RevitHostDriver))]
    [InlineData("drawing.dwg", typeof(AcadHostDriver))]
    [InlineData("template.dwt", typeof(AcadHostDriver))]
    [InlineData("template.rft", typeof(RevitHostDriver))]
    [InlineData("project.rte", typeof(RevitHostDriver))]
    [InlineData("exchange.dxf", typeof(AcadHostDriver))]
    [InlineData("published.dwf", typeof(AcadHostDriver))]
    [InlineData("MODEL.RVT", typeof(RevitHostDriver))]
    [InlineData("TEMPLATE.DWT", typeof(AcadHostDriver))]
    public void ForFile_selects_the_driver_registered_for_the_file_extension(string fileName, Type expectedDriver)
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.IsType(expectedDriver, registry.ForFile(fileName));
    }

    [Fact]
    public void TryForFile_returns_null_when_no_driver_owns_the_extension()
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.Null(registry.TryForFile("notes.txt"));
    }

    [Fact]
    public void Constructor_rejects_duplicate_extensions_without_regard_to_case()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new HostDriverRegistry(
        [
            new TestHostDriver("first", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".abc" }),
            new TestHostDriver("second", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABC" })
        ]));

        Assert.Contains(".abc", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HostApp.AutoCad)]
    [InlineData(HostApp.Civil3D)]
    [InlineData(HostApp.Plant3D)]
    [InlineData(HostApp.AcadArch)]
    [InlineData(HostApp.AcadMech)]
    [InlineData(HostApp.AcadElec)]
    [InlineData(HostApp.AcadMep)]
    [InlineData(HostApp.AcadMap3D)]
    public void ForHost_selects_the_same_AutoCAD_family_driver_for_every_supported_product(HostApp hostApp)
    {
        var acadDriver = new AcadHostDriver();
        var registry = new HostDriverRegistry([new RevitHostDriver(), acadDriver]);

        Assert.Same(acadDriver, registry.ForHost(hostApp));
    }

    [Fact]
    public void ForHost_selects_the_Revit_driver_for_Revit()
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.IsType<RevitHostDriver>(registry.ForHost(HostApp.Revit));
    }

    [Theory]
    [InlineData(HostApp.Navisworks)]
    [InlineData(HostApp.Rhino)]
    [InlineData(HostApp.Tekla)]
    public void ForHost_returns_null_when_no_driver_supports_the_product(HostApp hostApp)
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.Null(registry.TryForHost(hostApp));
    }

    [Theory]
    [InlineData(HostApp.Revit, "FRA", "model.rvt")]
    [InlineData(HostApp.Civil3D, "", "drawing.dwg")]
    public async Task LaunchAsync_receives_the_requested_product_and_language_unchanged(
        HostApp hostApp,
        string languageCode,
        string filePath)
    {
        var driver = new CapturingHostDriver();
        var registry = new HostDriverRegistry([driver]);
        var request = new HostLaunchRequest(hostApp, "2025", languageCode, filePath);

        await registry.ForHost(hostApp).LaunchAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(request, driver.Request);
    }

    private sealed class TestHostDriver(string hostId, IReadOnlySet<string> fileExtensions) : IHostDriver
    {
        public string HostId => hostId;
        public IReadOnlySet<HostApp> SupportedHostApps { get; } = new HashSet<HostApp>();
        public IReadOnlySet<string> FileExtensions => fileExtensions;
        public bool SupportsVersion(string version) => true;
        public Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingHostDriver : IHostDriver
    {
        public string HostId => "Revit";
        public IReadOnlySet<HostApp> SupportedHostApps { get; } = new HashSet<HostApp> { HostApp.Revit, HostApp.Civil3D };
        public IReadOnlySet<string> FileExtensions { get; } = new HashSet<string> { ".rvt" };
        public HostLaunchRequest? Request { get; private set; }
        public bool SupportsVersion(string version) => true;

        public Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(default(HostLaunchResult)!);
        }

        public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
