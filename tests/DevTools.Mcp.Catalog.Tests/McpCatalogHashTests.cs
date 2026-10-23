using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpCatalogHashTests
{
    [TestMethod]
    public void ContentHash_ChangesWhenDescriptionChangesButIdStays()
    {
        var first = CatalogWithTool("ping", "first");
        var second = CatalogWithTool("ping", "second");

        Assert.AreNotEqual(McpCatalogHash.Compute(first), McpCatalogHash.Compute(second));
    }

    [TestMethod]
    public void ContentHash_StableForSameCatalog()
    {
        var catalog = CatalogWithTool("ping", "same");
        Assert.AreEqual(McpCatalogHash.Compute(catalog), McpCatalogHash.Compute(catalog));
    }

    private static RegistryCatalog CatalogWithTool(string name, string description) =>
        new()
        {
            Tools =
            [
                new RegisteredTool
                {
                    Id = name,
                    Descriptor = new Tool
                    {
                        Name = name,
                        Description = description,
                        InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
                    },
                    Binding = PrimitiveBinding.Create(ExecutionMode.Dotnet, "stub.dll", "Stub", name, "", ""),
                },
            ],
            Resources = [],
        };
}
