using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.Dotnet;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Providers;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Runs a built DLL entry point, or an entry script inside a script folder, by exact path and name.</summary>
public sealed class RunCommandTool : IBuiltInMcpTool
{
    public const string ToolDescription =
        "Dev loop: run an entry point you just built. `path`: the built .dll, or the working " +
        "folder that holds your scripts (pass the folder, not a single script file, so sibling " +
        "modules resolve). `entry`: for a .dll, the Revit IExternalCommand full class name or the " +
        "AutoCAD CommandMethod as TypeFullName.MethodName; for a folder, the entry script path " +
        "relative to it (*script.py, *_ipy_script.py, *script.fsx, *script.csx). Exact match only; " +
        "nothing runs when it does not match. The DLL is loaded fresh in an isolated context for " +
        "each run. A command that opens a dialog or a pick prompt blocks until a person answers it.";

    private const string EntryScriptPatterns = "*script.py, *_ipy_script.py, *script.fsx, *script.csx";

    private readonly ICommandDiscovery _commandDiscovery;
    private readonly ICommandRunner _commandRunner;
    private readonly IHostContextExecutor _hostContext;
    private readonly IScriptExecutionStrategyFactory _scriptStrategyFactory;

    public RunCommandTool(
        ICommandDiscovery commandDiscovery,
        ICommandRunner commandRunner,
        IHostContextExecutor hostContext,
        IScriptExecutionStrategyFactory scriptStrategyFactory)
    {
        _commandDiscovery = commandDiscovery;
        _commandRunner = commandRunner;
        _hostContext = hostContext;
        _scriptStrategyFactory = scriptStrategyFactory;
        ServerTool = McpServerTool.Create(
            RunAsync,
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

    [Description("Run a built DLL entry point or an entry script from a folder in the running host process.")]
    private async Task<CallToolResult> RunAsync(
        [Description("Full path to the built .dll, or to the working folder that holds the entry script.")]
        string path,
        [Description("For a .dll: Revit IExternalCommand full class name, or AutoCAD TypeFullName.MethodName. For a folder: the entry script path relative to it.")]
        string? entry = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ToolHelpers.ErrorResult("path must not be empty.");

        var resolution = Resolve(path, entry);
        if (resolution.Strategy is null)
            return ToolHelpers.ErrorResult(resolution.Error ?? $"Nothing to run: {path}");

        var stopwatch = Stopwatch.StartNew();
        var result = await resolution.Strategy.ExecuteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        return ToResult(result, stopwatch.ElapsedMilliseconds);
    }

    private Resolution Resolve(string path, string? entry)
    {
        if (Directory.Exists(path))
            return ResolveScript(path, entry);

        if (!File.Exists(path))
            return Resolution.Fail($"File or folder not found: {path}");

        if (string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase))
            return ResolveAssembly(path, entry);

        if (ScriptEntryMode.TryResolve(path, out _))
            return Resolution.Fail(
                $"A single script file is not accepted: {path}. Pass the working folder as path and the script path relative to it as entry.");

        return Resolution.Fail(
            $"Unsupported file type '{Path.GetExtension(path)}': {path}. Use a .dll or the folder that holds your entry script.");
    }

    private Resolution ResolveAssembly(string path, string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return Resolution.Fail($"entry is required for a .dll: {path}");

        var command = _commandDiscovery
            .ParseCommands(path)
            .FirstOrDefault(item => string.Equals(item.FullClassName, entry, StringComparison.Ordinal));

        return command is null
            ? Resolution.Fail($"Entry '{entry}' is not an executable command in {path}")
            : Resolution.Run(new AssemblyExecutionStrategy(command, _hostContext, _commandRunner));
    }

    private Resolution ResolveScript(string folder, string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return Resolution.Fail($"entry is required for a folder: the entry script path relative to {folder}");

        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var scriptPath = Path.GetFullPath(Path.Combine(root, entry));

        if (!scriptPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return Resolution.Fail($"Entry '{entry}' is not inside {folder}");

        if (!File.Exists(scriptPath))
            return Resolution.Fail($"Entry '{entry}' not found in {folder}");

        if (!ScriptEntryMode.IsEntryScript(scriptPath) || !ScriptEntryMode.TryResolve(scriptPath, out var mode))
            return Resolution.Fail($"Entry '{entry}' in {folder} is not an entry script ({EntryScriptPatterns}).");

        return Resolution.Run(_scriptStrategyFactory.Create(mode, scriptPath, root));
    }

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

    private readonly record struct Resolution(IExecutionStrategy? Strategy, string? Error)
    {
        public static Resolution Run(IExecutionStrategy strategy) => new(strategy, null);
        public static Resolution Fail(string error) => new(null, error);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RunCommandResult(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName(IpcPropertyNames.Message)] string? Message,
        [property: JsonPropertyName("exceptionType")] string? ExceptionType,
        [property: JsonPropertyName("exceptionMessage")] string? ExceptionMessage,
        [property: JsonPropertyName("elapsedMs")] long ElapsedMs);
}
