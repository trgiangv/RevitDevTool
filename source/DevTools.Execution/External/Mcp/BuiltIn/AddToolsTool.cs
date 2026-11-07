using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>
/// Registers, reloads, and removes the host's MCP toolsets from MCP, through <see cref="McpCatalogStore"/> like the registry panel.
/// </summary>
public sealed class AddToolsTool : IBuiltInMcpTool
{
    public const string ToolDescription =
        """
        Dev loop for your own MCP toolsets: register a built [McpTool] .dll or a Python toolset folder in the running host, and reload it after a rebuild or an edit, without restarting the host.
        Shares the MCP registry panel's toolsets and saved paths.

        Actions:
        - add (default): register path, or reload it when it is already registered (fresh DLL load). Returns the tools and resources path provides.
        - reload: reload every registered toolset.
        - remove: unregister the toolset at exactly path.
        - list: registered toolsets with their tools and resources.

        Arguments:
        - path: the built .dll with [McpTool] methods, or a folder with *mcp.py files. Not needed for reload and list.

        New or changed tools reach SearchAsync and InvokeAsync once the daemon re-lists this host; call them from the next code_mode program.
        """;

    private readonly IServiceProvider _services;
    private McpCatalogStore? _store;

    public AddToolsTool(IServiceProvider services)
    {
        _services = services;
        ServerTool = McpServerTool.Create(
            InvokeAsync,
            new McpServerToolCreateOptions
            {
                Name = McpSpecKeys.Tool.AddTools,
                Title = "Add Tools",
                Description = ToolDescription,
                Destructive = true,
                OpenWorld = true
            });
    }

    public string Name => McpSpecKeys.Tool.AddTools;
    public McpServerTool ServerTool { get; }

    private McpCatalogStore Store => _store ??= _services.GetRequiredService<McpCatalogStore>();

    [Description("Add, reload, remove, or list the host's MCP toolsets.")]
    private async Task<CallToolResult> InvokeAsync(
        [Description("add (default) | reload | remove | list.")]
        string? action = null,
        [Description("Full path to the built .dll or the Python toolset folder. Not needed for reload and list.")]
        string? path = null)
    {
        return (string.IsNullOrWhiteSpace(action) ? "add" : action!.Trim().ToLowerInvariant()) switch
        {
            "add" => await AddAsync(path).ConfigureAwait(false),
            "reload" => await ReloadAsync().ConfigureAwait(false),
            "remove" => await RemoveAsync(path).ConfigureAwait(false),
            "list" => List(),
            _ => ToolHelpers.ErrorResult($"Unknown action '{action}'. Use add, reload, remove, or list.")
        };
    }

    private async Task<CallToolResult> AddAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ToolHelpers.ErrorResult("path must not be empty.");

        var change = await Store.AddPathAsync(path!).ConfigureAwait(false);
        return change switch
        {
            ToolsetChange.Added or ToolsetChange.Reloaded =>
                ToolHelpers.Result(new ToolsetResult(change.ToString(), ToToolsetInfo(Path.GetFullPath(path!)))),
            ToolsetChange.NoItems => ToolHelpers.ErrorResult(NothingToRegister(Path.GetFullPath(path!))),
            _ => ToolHelpers.ErrorResult(UnsupportedReason(path!))
        };
    }

    private async Task<CallToolResult> ReloadAsync()
    {
        await Store.ReloadAsync().ConfigureAwait(false);
        return List();
    }

    private async Task<CallToolResult> RemoveAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ToolHelpers.ErrorResult("path must not be empty.");

        var info = ToToolsetInfo(path!);
        return await Store.RemovePathAsync(path!).ConfigureAwait(false) == ToolsetChange.Removed
            ? ToolHelpers.Result(new ToolsetResult(nameof(ToolsetChange.Removed), info))
            : ToolHelpers.ErrorResult($"'{path}' is not a registered toolset. Use action list to see the toolsets.");
    }

    private CallToolResult List() =>
        ToolHelpers.Result(new ToolsetList(Store.ToolsetPaths.Select(ToToolsetInfo).ToList()));

    private ToolsetInfo ToToolsetInfo(string path)
    {
        Store.EnsureLoaded();
        return new ToolsetInfo(
            path,
            string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : "python",
            Store.RegisteredTools.Where(tool => McpPathValidator.BelongsTo(path, tool.Binding)).Select(tool => tool.Descriptor.Name).ToList(),
            Store.ResourceCatalog.Where(resource => McpPathValidator.BelongsTo(path, resource.Binding)).Select(ResourceName).ToList());
    }

    private static string ResourceName(RegisteredResource resource) =>
        resource.Descriptor?.Uri ?? resource.TemplateDescriptor?.UriTemplate ?? resource.DisplayName;

    private string NothingToRegister(string path)
    {
        if (!Store.LoadFailures.TryGetValue(path, out var failure))
            return $"Nothing to register from {path}: no [McpTool] methods or *mcp.py tools found. The path is not saved.";

        var sdkHint = failure.Contains(nameof(ModelContextProtocol), StringComparison.Ordinal)
            ? " A .NET toolset ships the MCP SDK: reference ModelContextProtocol without ExcludeAssets=runtime and rebuild."
            : string.Empty;
        return $"Could not load tools from {path}: {failure}{sdkHint} The path is not saved.";
    }

    private static string UnsupportedReason(string path)
    {
        if (Directory.Exists(path))
            return $"No {McpPathValidator.PythonToolPattern} files in {path}. Pass a built .dll or a Python toolset folder.";

        return File.Exists(path)
            ? $"Unsupported file type '{Path.GetExtension(path)}': {path}. Pass a built .dll or a Python toolset folder."
            : $"File or folder not found: {path}";
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ToolsetInfo(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("tools")] IReadOnlyList<string> Tools,
        [property: JsonPropertyName("resources")] IReadOnlyList<string> Resources);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ToolsetResult(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("toolset")] ToolsetInfo Toolset);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ToolsetList(
        [property: JsonPropertyName("toolsets")] IReadOnlyList<ToolsetInfo> Toolsets);
}
