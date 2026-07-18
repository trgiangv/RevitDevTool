using DevTools.Daemon.Auth;
using DevTools.Daemon.Hosting;
using DevTools.Daemon.Mcp;
using DevTools.Mcp.Routing.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace RevitDevTool.Server.Tests;

public sealed class PublicSurfaceBudgetTests
{
    [Fact]
    public void CurrentSurface_IsMeasuredBeforeReplacement()
    {
        var engine = new McpEngine(
            new InstanceManager(NullLogger<InstanceManager>.Instance),
            new DynamicToolCatalog(),
            new DynamicResourceCatalog(),
            new DynamicPromptCatalog(),
            new UnauthenticatedAuthService(),
            Options.Create(new GatewayOptions()));

        var currentNames = engine.LocalTools
            .Select(tool => tool.ProtocolTool.Name)
            .ToArray();

        Assert.Equal(12, currentNames.Length);
        Assert.Equal(
            [
                "list_machines", "list_host_instances", "launch_host", "read_file_info",
                "open_model", "list_dynamic_tools", "call_dynamic_tool",
                "list_dynamic_resources", "read_dynamic_resource",
                "list_dynamic_prompts", "get_dynamic_prompt", "refresh_dynamic_catalog"
            ],
            currentNames);
    }

    private sealed class UnauthenticatedAuthService : IAuthService
    {
        public bool IsAuthenticated => false;
        public string? AccessToken => null;
        public string? UserId => null;
        public string? Email => null;
        public string? DisplayName => null;
        public string? AvatarUrl => null;

        public event EventHandler<AuthStateChangedArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public Task<AuthResult> SignInAsync(CancellationToken ct = default) =>
            Task.FromResult(new AuthResult(false));

        public Task SignOutAsync() => Task.CompletedTask;

        public Task<bool> RefreshAsync() => Task.FromResult(false);
    }
}
