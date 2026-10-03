using DevTools.Execution.Abstractions;
using DevTools.Mcp;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Settings;
using DevTools.Settings.Configs;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Catalog.Tests.Harness;

internal static class McpHostTestHarness
{
    public static McpCatalogStore CreateCatalogStore(params RegisteredTool[] tools)
    {
        var catalog = new RegistryCatalog
        {
            Tools = tools,
            Resources = [],
        };

        var loader = new Mock<ICatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(catalog);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());

        return new McpCatalogStore(loader.Object, settings.Object);
    }

    public static RegisteredTool CreateRegisteredTool(string name, string? description = null) => new()
    {
        Id = name,
        Descriptor = new Tool
        {
            Name = name,
            Description = description ?? $"{name} description",
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
        },
        Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name, "", ""),
    };
}
