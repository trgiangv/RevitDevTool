using System.IO.Pipelines;
using System.Text.Json;
using DevTools.Daemon.Composition;
using DevTools.Mcp.Server.Hosting;
using DevTools.FileMetadata.Core;
using DevTools.Mcp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Tests;

public class ServerHostBuilderCompositionTests
{
    [Fact]
    public void StdioComposition_ResolvesExternalServerModules()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var services = host.Services;

        Assert.NotNull(services.GetRequiredService<IHostBroker>());
        Assert.NotNull(services.GetRequiredService<IFileReaderCatalog>());
        Assert.NotNull(services.GetRequiredService<McpEngine>());
    }

    [Fact]
    public async Task StdioComposition_ListsSixDaemonToolsViaSdkClient()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var engine = host.Services.GetRequiredService<McpEngine>();
        var options = McpServerFactory.CreateOptions(
            engine.ToolCollection, engine.PromptCollection, host.Services);

        Assert.Equal(6, engine.LocalTools.Count);
        Assert.Equal(2, engine.PromptCollection.Count);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var transport = new StreamServerTransport(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "stdio-test",
            NullLoggerFactory.Instance);
        await using var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, host.Services);
        var serverTask = server.RunAsync(cts.Token);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        var prompts = await client.ListPromptsAsync(cancellationToken: cts.Token);

        Assert.Equal(
            [
                "invoke_dynamic",
                "launch_host",
                "list_host_instances",
                "list_machines",
                "read_file_info",
                "search_dynamic",
            ],
            tools.Select(tool => tool.Name).OrderBy(name => name).ToArray());
        Assert.Equal(["acad_code", "revit_code"], prompts.Select(prompt => prompt.Name).OrderBy(name => name).ToArray());

        await client.DisposeAsync();
        await cts.CancelAsync();
        try { await serverTask; } catch { /* ignored */ }
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void StdioComposition_DaemonToolSchemas_AreCursorSafe()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var engine = host.Services.GetRequiredService<McpEngine>();

        foreach (var tool in engine.LocalTools)
        {
            var protocol = tool.ProtocolTool;
            Assert.Null(protocol.OutputSchema);

            var inputSchema = protocol.InputSchema;
            Assert.Equal(JsonValueKind.Object, inputSchema.ValueKind);
            AssertAllPropertiesHaveType(protocol.Name, inputSchema);
        }
    }

    private static void AssertAllPropertiesHaveType(string toolName, JsonElement schema)
    {
        if (!schema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind is not JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in properties.EnumerateObject())
        {
            Assert.True(
                property.Value.TryGetProperty("type", out _) ||
                property.Value.TryGetProperty("$ref", out _),
                $"{toolName} input property '{property.Name}' is missing a JSON Schema type or $ref.");

            if (!property.Value.TryGetProperty("type", out var type) ||
                type.ValueKind is not JsonValueKind.String)
            {
                continue;
            }

            var typeName = type.GetString();
            if (typeName is "object")
                AssertAllPropertiesHaveType($"{toolName}.{property.Name}", property.Value);
            if (typeName is "array" &&
                property.Value.TryGetProperty("items", out var items))
            {
                Assert.True(
                    items.TryGetProperty("type", out _) || items.TryGetProperty("$ref", out _),
                    $"{toolName} input property '{property.Name}' items are missing type or $ref.");
                if (items.TryGetProperty("type", out var itemType) &&
                    itemType.GetString() is "object")
                    AssertAllPropertiesHaveType($"{toolName}.{property.Name}[]", items);
            }
        }
    }
}
