using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class BuiltInMcpRegistryProviderTests
{
    [TestMethod]
    public void Name_IsBuiltIn()
    {
        var provider = new BuiltInMcpRegistryProvider([], []);

        Assert.AreEqual("built-in", provider.Name);
        Assert.AreEqual(ExecutionMode.CSharp, provider.SourceKind);
    }

    [TestMethod]
    public void LoadCatalog_EmptyEnumerables_ReturnsEmptyCatalog()
    {
        var provider = new BuiltInMcpRegistryProvider([], []);

        var catalog = provider.LoadCatalog();

        Assert.IsEmpty(catalog.Tools);
        Assert.IsEmpty(catalog.Resources);
    }

    [TestMethod]
    public void ConfigurePaths_IsNoOp()
    {
        var provider = new BuiltInMcpRegistryProvider([], []);

        provider.ConfigurePaths(["C:\\ignored\\path.dll"]);

        var catalog = provider.LoadCatalog();

        Assert.IsEmpty(catalog.Tools);
        Assert.IsEmpty(catalog.Resources);
    }

    [TestMethod]
    public void LoadCatalog_WithMockTool_RegistersCSharpBuiltInBinding()
    {
        var serverTool = McpServerTool.Create(
            () => "ok",
            new McpServerToolCreateOptions { Name = "open_document" });

        var mockTool = new Mock<IBuiltInMcpTool>();
        mockTool.Setup(tool => tool.Name).Returns("open_document");
        mockTool.Setup(tool => tool.ServerTool).Returns(serverTool);

        var provider = new BuiltInMcpRegistryProvider([mockTool.Object], []);
        var catalog = provider.LoadCatalog();

        Assert.HasCount(1, catalog.Tools);
        Assert.IsEmpty(catalog.Resources);

        var registered = catalog.Tools[0];
        Assert.AreEqual("open_document", registered.Descriptor.Name);
        Assert.AreEqual(ExecutionMode.CSharp, registered.Binding.SourceKind);
        Assert.AreEqual("Built-in", registered.Binding.GroupName);
        Assert.AreEqual("BuiltIn", registered.Binding.ContainerType);
        Assert.AreEqual("open_document", registered.Binding.MethodName);
        Assert.IsEmpty(registered.Binding.SourcePath);
        Assert.AreEqual("BuiltIn.open_document", registered.Binding.SourceAddress);

        var expectedId = McpPrimitiveBinding.CreatePrimitiveId("open_document", "BuiltIn.open_document");
        Assert.AreEqual(expectedId, registered.Id);
    }

    [TestMethod]
    public void LoadCatalog_WithMockResource_RegistersCSharpBuiltInBinding()
    {
        var protocolResource = new Resource
        {
            Uri = "test://resource",
            Name = "test_resource",
            Description = "Test built-in resource",
            MimeType = "text/plain"
        };

        var mockResource = new Mock<IBuiltInMcpResource>();
        mockResource.Setup(resource => resource.ProtocolResource).Returns(protocolResource);

        var provider = new BuiltInMcpRegistryProvider([], [mockResource.Object]);
        var catalog = provider.LoadCatalog();

        Assert.IsEmpty(catalog.Tools);
        Assert.HasCount(1, catalog.Resources);

        var registered = catalog.Resources[0];
        Assert.IsNotNull(registered.Descriptor);
        Assert.AreEqual("test_resource", registered.Descriptor.Name);
        Assert.AreEqual(ExecutionMode.CSharp, registered.Binding.SourceKind);
        Assert.AreEqual("Built-in", registered.Binding.GroupName);
        Assert.AreEqual("BuiltIn", registered.Binding.ContainerType);
        Assert.AreEqual("test_resource", registered.Binding.MethodName);
        Assert.IsEmpty(registered.Binding.SourcePath);
        Assert.AreEqual("BuiltIn.test_resource", registered.Binding.SourceAddress);

        var expectedId = McpPrimitiveBinding.CreatePrimitiveId("test_resource", "BuiltIn.test_resource");
        Assert.AreEqual(expectedId, registered.Id);
    }
}

[TestClass]
public sealed class DotnetMcpRegistryProviderTests
{
    [TestMethod]
    public void Name_IsDotnetMcp()
    {
        var provider = CreateProvider();

        Assert.AreEqual("dotnet-mcp", provider.Name);
        Assert.AreEqual(ExecutionMode.Dotnet, provider.SourceKind);
    }

    [TestMethod]
    public void LoadCatalog_EmptyPaths_ReturnsEmptyCatalog()
    {
        var provider = CreateProvider();

        var catalog = provider.LoadCatalog();

        Assert.IsEmpty(catalog.Tools);
        Assert.IsEmpty(catalog.Resources);
    }

    [TestMethod]
    public void ConfigurePaths_WithMissingAssembly_ReturnsEmptyCatalog()
    {
        var provider = CreateProvider();

        provider.ConfigurePaths(["C:\\missing\\toolset.dll"]);

        var catalog = provider.LoadCatalog();

        Assert.IsEmpty(catalog.Tools);
        Assert.IsEmpty(catalog.Resources);
    }

    private static DotnetMcpRegistryProvider CreateProvider() =>
        new(new McpAssemblyParser(NullLogger<McpAssemblyParser>.Instance),
            NullLogger<DotnetMcpRegistryProvider>.Instance);
}
