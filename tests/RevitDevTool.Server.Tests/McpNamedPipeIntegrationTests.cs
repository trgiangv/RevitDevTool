using System.IO.Pipes;
using DevTools.Execution.External.Mcp.Hosting;
using DevTools.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class McpPipeNameTests
{
    [Fact]
    public void FormatAndParse_UseProtocolVersionAndPidOnly()
    {
        var name = McpPipeName.Format(4217);

        Assert.Equal("DevTools.Mcp.v2.4217", name);
        Assert.True(McpPipeName.TryParse(name, out var processId));
        Assert.Equal(4217, processId);
        Assert.False(McpPipeName.TryParse("DevTools_Mcp_Revit_2025_4217", out _));
    }
}

public sealed class McpNamedPipeIntegrationTests
{
    [Fact]
    public async Task HostedService_InitializesSdkClientWithHostMetadata()
    {
        var hostInfo = new TestHostAppInfo();
        var optionsFactory = new HostMcpServerOptionsFactory(
            hostInfo,
            [],
            [],
            []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                McpPipeName.Format(Environment.ProcessId),
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000, TestContext.Current.CancellationToken);

            var transport = new StreamClientTransport(pipe, pipe, NullLoggerFactory.Instance);
            await using var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ClientInfo = new Implementation { Name = "integration-test", Version = "1.0" }
                },
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("Revit", client.ServerInfo.Name);
            Assert.Equal("2027", client.ServerInfo.Version);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    private sealed class TestHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2027";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }
}
