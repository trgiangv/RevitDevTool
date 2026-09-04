using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class BuiltInMcpRegistryProviderTests
{
    [Fact]
    public void Name_IsBuiltIn()
    {
        var provider = new BuiltInMcpRegistryProvider([], []);

        Assert.Equal("built-in", provider.Name);
        Assert.Equal(ExecutionMode.CSharp, provider.SourceKind);
    }

    [Fact]
    public void LoadCatalog_EmptyEnumerables_ReturnsEmptyCatalog()
    {
        var provider = new BuiltInMcpRegistryProvider([], []);

        var catalog = provider.LoadCatalog();

        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);
    }

    [Fact]
    public void ConfigurePaths_IsNoOp()
    {
        var provider = new BuiltInMcpRegistryProvider([], []);

        provider.ConfigurePaths(["C:\\ignored\\path.dll"]);

        var catalog = provider.LoadCatalog();

        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);
    }

    [Fact]
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

        Assert.Single(catalog.Tools);
        Assert.Empty(catalog.Resources);

        var registered = catalog.Tools[0];
        Assert.Equal("open_document", registered.Descriptor.Name);
        Assert.Equal(ExecutionMode.CSharp, registered.Binding.SourceKind);
        Assert.Equal("Built-in", registered.Binding.GroupName);
        Assert.Equal("BuiltIn", registered.Binding.ContainerType);
        Assert.Equal("open_document", registered.Binding.MethodName);
        Assert.Empty(registered.Binding.SourcePath);
        Assert.Equal("BuiltIn.open_document", registered.Binding.SourceAddress);

        var expectedId = McpPrimitiveBinding.CreatePrimitiveId("open_document", "BuiltIn.open_document");
        Assert.Equal(expectedId, registered.Id);
    }

    [Fact]
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

        Assert.Empty(catalog.Tools);
        Assert.Single(catalog.Resources);

        var registered = catalog.Resources[0];
        Assert.NotNull(registered.Descriptor);
        Assert.Equal("test_resource", registered.Descriptor.Name);
        Assert.Equal(ExecutionMode.CSharp, registered.Binding.SourceKind);
        Assert.Equal("Built-in", registered.Binding.GroupName);
        Assert.Equal("BuiltIn", registered.Binding.ContainerType);
        Assert.Equal("test_resource", registered.Binding.MethodName);
        Assert.Empty(registered.Binding.SourcePath);
        Assert.Equal("BuiltIn.test_resource", registered.Binding.SourceAddress);

        var expectedId = McpPrimitiveBinding.CreatePrimitiveId("test_resource", "BuiltIn.test_resource");
        Assert.Equal(expectedId, registered.Id);
    }
}

public sealed class DotnetMcpRegistryProviderTests
{
    [Fact]
    public void Name_IsDotnetMcp()
    {
        var provider = CreateProvider();

        Assert.Equal("dotnet-mcp", provider.Name);
        Assert.Equal(ExecutionMode.Dotnet, provider.SourceKind);
    }

    [Fact]
    public void LoadCatalog_EmptyPaths_ReturnsEmptyCatalog()
    {
        var provider = CreateProvider();

        var catalog = provider.LoadCatalog();

        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);
    }

    [Fact]
    public void ConfigurePaths_WithMissingAssembly_ReturnsEmptyCatalog()
    {
        var provider = CreateProvider();

        provider.ConfigurePaths(["C:\\missing\\toolset.dll"]);

        var catalog = provider.LoadCatalog();

        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);
    }

    private static DotnetMcpRegistryProvider CreateProvider() =>
        new(new McpAssemblyParser(NullLogger<McpAssemblyParser>.Instance),
            NullLogger<DotnetMcpRegistryProvider>.Instance);
}
