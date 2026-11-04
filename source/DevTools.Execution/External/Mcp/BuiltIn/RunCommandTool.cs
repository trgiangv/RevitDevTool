using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Providers;
using DevTools.UI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>
/// Drives Command Execution from MCP: run an entry, add or remove a root, list roots and entries.
/// Every action goes through <see cref="IExecutionOrchestrator"/> on the host UI thread, like the panel.
/// </summary>
public sealed class RunCommandTool : IBuiltInMcpTool
{
    public const string ToolDescription =
        """
        Dev loop: run a built add-in command or an entry script in the running host after a rebuild, without restarting the host.
        Shares the Command Execution panel's tree and saved paths.

        Actions:
        - run (default): adds path if needed, finds entry, and runs it like a panel click (fresh DLL load). If entry is missing it reloads path once, then fails without suggestions.
        - add: load path into the tree.
        - remove: drop the root at exactly path.
        - list: roots with their entries in the form run accepts; all roots, or the one holding path.

        Arguments:
        - path: the built .dll, or the script folder (not a single script file).
        - entry (run only, exact match):
          - .dll: Revit IExternalCommand full class name, or AutoCAD CommandMethod as Namespace.Type.Method.
          - folder: entry script path relative to it (*script.py, *_ipy_script.py, *script.fsx, *script.csx).

        Dialogs and pick prompts block the call until a person answers.
        """;

    private const string EntryScriptPatterns = "*script.py, *_ipy_script.py, *script.fsx, *script.csx";

    private readonly IExecutionOrchestrator _orchestrator;

    public RunCommandTool(IExecutionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        ServerTool = McpServerTool.Create(
            InvokeAsync,
            new McpServerToolCreateOptions
            {
                Name = McpSpecKeys.Tool.RunCommand,
                Title = "Run Command",
                Description = ToolDescription,
                Destructive = true,
                OpenWorld = true
            });
    }

    public string Name => McpSpecKeys.Tool.RunCommand;
    public McpServerTool ServerTool { get; }

    [Description("Run, add, remove, or list Command Execution entries in the running host process.")]
    private Task<CallToolResult> InvokeAsync(
        [Description("run (default) | add | remove | list.")]
        string? action = null,
        [Description("Full path to the built .dll or the script folder. Optional for list.")]
        string? path = null,
        [Description("run only. .dll: IExternalCommand full class name or Namespace.Type.Method. Folder: entry script path relative to it.")]
        string? entry = null,
        CancellationToken cancellationToken = default)
    {
        return HostUiHelper.RunOnMainThreadAsync(async () =>
        {
            await _orchestrator.LoadSavedPathsAsync(cancellationToken).ConfigureAwait(true);

            return (string.IsNullOrWhiteSpace(action) ? "run" : action!.Trim().ToLowerInvariant()) switch
            {
                "run" => await RunEntryAsync(path, entry, cancellationToken).ConfigureAwait(true),
                "add" => await AddAsync(path, cancellationToken).ConfigureAwait(true),
                "remove" => Remove(path),
                "list" => List(path),
                _ => ToolHelpers.ErrorResult($"Unknown action '{action}'. Use run, add, remove, or list.")
            };
        });
    }

    private async Task<CallToolResult> RunEntryAsync(string? path, string? entry, CancellationToken cancellationToken)
    {
        if (!TryResolveTarget(path, entry, out var target, out var error))
            return ToolHelpers.ErrorResult(error);

        var (root, loadError) = await EnsureRootAsync(target.RootPath, cancellationToken).ConfigureAwait(true);
        if (root is null)
            return ToolHelpers.ErrorResult(loadError!);

        var node = FindEntry(root, target);
        if (node is null)
        {
            root = await _orchestrator.ReloadRootAsync(root, cancellationToken).ConfigureAwait(true);
            node = root is null ? null : FindEntry(root, target);
        }

        if (node is null)
            return ToolHelpers.ErrorResult(target.Container == ContainerMode.Assembly
                ? $"Entry '{entry}' is not an executable command in {path}"
                : $"Entry '{entry}' in {path} is not an entry script ({EntryScriptPatterns}).");

        var stopwatch = Stopwatch.StartNew();
        var result = await _orchestrator.ExecuteAsync(node, cancellationToken).ConfigureAwait(true);
        stopwatch.Stop();

        return ToResult(result, stopwatch.ElapsedMilliseconds);
    }

    private async Task<CallToolResult> AddAsync(string? path, CancellationToken cancellationToken)
    {
        if (!TryClassifyPath(path, out _, out var error))
            return ToolHelpers.ErrorResult(error);

        var existing = _orchestrator.FindRoot(path!);
        var (root, loadError) = await EnsureRootAsync(path!, cancellationToken).ConfigureAwait(true);
        if (root is null)
            return ToolHelpers.ErrorResult(loadError!);

        var status = existing is null ? "Added" : SamePath(existing.RootPath, path!) ? "AlreadyLoaded" : "CoveredByRoot";
        return ToolHelpers.Result(new RootChange(status, ToRootInfo(root)));
    }

    private CallToolResult Remove(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ToolHelpers.ErrorResult("path must not be empty.");

        var root = _orchestrator.FindRoot(path!);
        if (root is null || !SamePath(root.RootPath, path!))
            return ToolHelpers.ErrorResult($"'{path}' is not a root in Command Execution. Use action list to see the roots.");

        var info = ToRootInfo(root);
        _orchestrator.RemoveNode(root);
        return ToolHelpers.Result(new RootChange("Removed", info));
    }

    private CallToolResult List(string? path)
    {
        var roots = _orchestrator.TreeRoot.OfType<ExecutionNodeRoot>();
        if (!string.IsNullOrWhiteSpace(path))
        {
            var match = _orchestrator.FindRoot(path!);
            roots = roots.Where(root => root == match);
        }

        return ToolHelpers.Result(new RootList(roots.Select(ToRootInfo).ToList()));
    }

    private async Task<(ExecutionNodeRoot? Root, string? Error)> EnsureRootAsync(string path, CancellationToken cancellationToken)
    {
        if (_orchestrator.FindRoot(path) is { } existing)
            return (existing, null);

        try
        {
            await _orchestrator.LoadFromPathAsync(path, cancellationToken).ConfigureAwait(true);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }

        return _orchestrator.FindRoot(path) is { } root
            ? (root, null)
            : (null, $"Nothing to load from {path}: no commands or entry scripts found.");
    }

    private static bool TryClassifyPath(string? path, out ContainerMode container, out string error)
    {
        container = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "path must not be empty.";
            return false;
        }

        if (Directory.Exists(path))
        {
            container = ContainerMode.Script;
            return true;
        }

        if (!File.Exists(path))
        {
            error = $"File or folder not found: {path}";
            return false;
        }

        if (string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            container = ContainerMode.Assembly;
            return true;
        }

        error = ScriptEntryMode.TryResolve(path!, out _)
            ? $"A single script file is not accepted: {path}. Pass the working folder as path and the script path relative to it as entry."
            : $"Unsupported file type '{Path.GetExtension(path)}': {path}. Use a .dll or the folder that holds your entry script.";
        return false;
    }

    private static bool TryResolveTarget(string? path, string? entry, out Target target, out string error)
    {
        target = default;
        if (!TryClassifyPath(path, out var container, out error))
            return false;

        if (string.IsNullOrWhiteSpace(entry))
        {
            error = container == ContainerMode.Assembly
                ? $"entry is required for a .dll: {path}"
                : $"entry is required for a folder: the entry script path relative to {path}";
            return false;
        }

        if (container == ContainerMode.Assembly)
        {
            target = new Target(Path.GetFullPath(path!), container, entry!);
            return true;
        }

        var root = Path.GetFullPath(path!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var scriptPath = Path.GetFullPath(Path.Combine(root, entry!));

        if (!scriptPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Entry '{entry}' is not inside {path}";
            return false;
        }

        if (!File.Exists(scriptPath))
        {
            error = $"Entry '{entry}' not found in {path}";
            return false;
        }

        if (!ScriptEntryMode.IsEntryScript(scriptPath))
        {
            error = $"Entry '{entry}' in {path} is not an entry script ({EntryScriptPatterns}).";
            return false;
        }

        target = new Target(root, container, scriptPath);
        return true;
    }

    private static ExecutionNode? FindEntry(ExecutionNodeRoot root, Target target) =>
        Executables(root).FirstOrDefault(node => target.Container == ContainerMode.Assembly
            ? string.Equals(node.ExecutablePath, target.Entry, StringComparison.Ordinal)
            : SamePath(node.ExecutablePath, target.Entry));

    private static IEnumerable<ExecutionNode> Executables(ExecutionNodeBase node)
    {
        foreach (var child in node.Children)
        {
            if (child is ExecutionNode executable)
                yield return executable;

            foreach (var nested in Executables(child))
                yield return nested;
        }
    }

    private static RootInfo ToRootInfo(ExecutionNodeRoot root) =>
        new(
            root.RootPath,
            root.ContainerMode == ContainerMode.Assembly ? "assembly" : "script",
            Executables(root)
                .Select(node => root.ContainerMode == ContainerMode.Assembly
                    ? node.ExecutablePath
                    : Path.GetRelativePath(root.RootPath, node.ExecutablePath))
                .ToList());

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static CallToolResult ToResult(ExecutionResult result, long measuredMs)
    {
        var elapsedMs = result.DurationMs > 0 ? result.DurationMs : measuredMs;
        var json = ToolHelpers.Serialize(new RunCommandResult(
            StatusOf(result),
            result.Message,
            result.Exception?.GetType().FullName,
            result.Exception?.Message,
            elapsedMs));

        if (result.IsCancelled)
            return ToolHelpers.ErrorResult(json);

        if (!result.Success)
            return ToolHelpers.ErrorResult($"{FailurePrefix(result.Message)}{json}");

        var rollback = ExecutionGuardContext.RollbackSummary;
        if (string.IsNullOrEmpty(rollback))
            return ToolHelpers.Result(json);

        return ToolHelpers.Result($"{json}\n\n {rollback}");
    }

    private static string StatusOf(ExecutionResult result)
    {
        if (result.Success)
            return "Succeeded";

        return result.IsCancelled ? "Cancelled" : "Failed";
    }

    private static string FailurePrefix(string message)
    {
        if (message.Contains("rolled back", StringComparison.OrdinalIgnoreCase))
            return $"{McpSpecKeys.Result.Rollback} Transaction failed due to unresolvable constraint.\n";

        return $"{McpSpecKeys.Result.Runtime} ";
    }

    private readonly record struct Target(string RootPath, ContainerMode Container, string Entry);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RunCommandResult(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName(IpcPropertyNames.Message)] string? Message,
        [property: JsonPropertyName("exceptionType")] string? ExceptionType,
        [property: JsonPropertyName("exceptionMessage")] string? ExceptionMessage,
        [property: JsonPropertyName("elapsedMs")] long ElapsedMs);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RootInfo(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("entries")] IReadOnlyList<string> Entries);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RootChange(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("root")] RootInfo Root);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RootList(
        [property: JsonPropertyName("roots")] IReadOnlyList<RootInfo> Roots);
}
