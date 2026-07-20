using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Execution.Tests;

#pragma warning disable MCPEXP001
public sealed class PytestMcpToolTests
{
    [Fact]
    public void Primitive_UsesReservedNameAndExactSnakeCaseSchema()
    {
        var tool = CreateTool().Primitive.ProtocolTool;

        Assert.Equal("pytest_run", tool.Name);
        Assert.Equal(ToolTaskSupport.Optional, tool.Execution?.TaskSupport);
        var required = tool.InputSchema.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal(["workspace_root", "test_root", "nodeids", "pytest_args"], required);
    }

    [Fact]
    public async Task PytestFailures_AreDomainResultsNotToolErrors()
    {
        var tool = CreateTool(response: OneFailedCase());

        var result = await InvokeAsync(tool);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(1, result.StructuredContent!.Value.GetProperty("exit_code").GetInt32());
        Assert.Equal("pytest exit 1: 0 passed, 1 failed, 0 errors", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public async Task InvalidRequest_ReturnsStableInfrastructureErrorWithoutPreparingOrExecuting()
    {
        var dependencies = new RecordingDependencyService();
        var execution = new RecordingExecutionService(OneFailedCase());
        var tool = CreateTool(dependencies: dependencies, execution: execution);

        var result = await tool.RunAsync(
            "not-an-existing-workspace",
            "tests",
            ["tests/test_sample.py::test_runs"],
            [],
            null!,
            null!,
            null!,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(PytestMcpErrorCodes.InvalidInput, result.StructuredContent!.Value.GetProperty("status").GetString());
        Assert.False(dependencies.Prepared);
        Assert.False(execution.Executed);
    }

    [Fact]
    public async Task TestRootOutsideWorkspace_ReturnsStableInfrastructureError()
    {
        var dependencies = new RecordingDependencyService();
        var tool = CreateTool(dependencies: dependencies);

        var result = await tool.RunAsync(
            Path.GetTempPath(),
            Path.GetPathRoot(Path.GetTempPath())!,
            ["tests/test_sample.py::test_runs"],
            [],
            null!,
            null!,
            null!,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(PytestMcpErrorCodes.InvalidInput, result.StructuredContent!.Value.GetProperty("status").GetString());
        Assert.False(dependencies.Prepared);
    }

    [Fact]
    public async Task EmptyNodeIds_ReturnsStableInfrastructureErrorWithoutPreparing()
    {
        var dependencies = new RecordingDependencyService();
        var tool = CreateTool(dependencies: dependencies);

        var result = await tool.RunAsync(
            Path.GetTempPath(),
            Path.GetTempPath(),
            [],
            [],
            null!,
            null!,
            null!,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(PytestMcpErrorCodes.InvalidInput, result.StructuredContent!.Value.GetProperty("status").GetString());
        Assert.False(dependencies.Prepared);
    }

    [Fact]
    public async Task DependencyPreparationFailure_ReturnsStableInfrastructureError()
    {
        var tool = CreateTool(dependencies: new RecordingDependencyService(new InvalidOperationException("Pixi unavailable")));

        var result = await InvokeAsync(tool);

        Assert.True(result.IsError);
        Assert.Equal(PytestMcpErrorCodes.DependencyPreparationFailed, result.StructuredContent!.Value.GetProperty("status").GetString());
        Assert.DoesNotContain("Pixi unavailable", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_SuppressesGuardAndPassesCancellationToHostContext()
    {
        var host = new RecordingHostContextExecutor();
        var tool = CreateTool(host: host);

        await InvokeAsync(tool);

        Assert.Equal(ExecutionGuardMode.Suppress, host.ObservedMode);
        Assert.Equal(TestContext.Current.CancellationToken, host.ObservedToken);
    }

    private static PytestRunTool CreateTool(
        PytestRunResponse? response = null,
        RecordingHostContextExecutor? host = null,
        RecordingDependencyService? dependencies = null,
        RecordingExecutionService? execution = null)
    {
        return new PytestRunTool(
            host ?? new RecordingHostContextExecutor(),
            dependencies ?? new RecordingDependencyService(),
            execution ?? new RecordingExecutionService(response ?? PassingCase()),
            NullLogger<PytestRunTool>.Instance);
    }

    private static async Task<CallToolResult> InvokeAsync(PytestRunTool tool)
    {
        var workspaceRoot = Path.GetTempPath();
        return await tool.RunAsync(
            workspaceRoot,
            workspaceRoot,
            ["tests/test_sample.py::test_runs"],
            ["-q"],
            null!,
            null!,
            null!,
            TestContext.Current.CancellationToken);
    }

    private static PytestRunResponse PassingCase() => new(
        0,
        new PytestSummary(1, 0, 0, 0, 0, 0),
        [],
        [],
        Path.GetTempPath());

    private static PytestRunResponse OneFailedCase() => new(
        1,
        new PytestSummary(0, 1, 0, 0, 0, 0),
        [new PytestCaseResult("tests/test_sample.py::test_runs", "failed", "call", 1, "", "", "failed", "")],
        [],
        Path.GetTempPath());

    private sealed class RecordingHostContextExecutor : IHostContextExecutor
    {
        public ExecutionGuardMode? ObservedMode { get; private set; }
        public CancellationToken ObservedToken { get; private set; }

        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            ObservedMode = ExecutionGuardContext.Mode;
            ObservedToken = token;
            return Task.FromResult(handler());
        }

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            ObservedMode = ExecutionGuardContext.Mode;
            ObservedToken = token;
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDependencyService(Exception? failure = null) : PytestDependencyService(null!)
    {
        public bool Prepared { get; private set; }

        public override Task PrepareRunAsync(PytestRunRequest request, CancellationToken cancellationToken = default)
        {
            Prepared = true;
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private sealed class RecordingExecutionService(PytestRunResponse response) : PytestExecutionService(null!)
    {
        public bool Executed { get; private set; }

        public override PytestRunResponse Run(PytestRunRequest request, Action<string>? progressCallback = null)
        {
            Executed = true;
            return response;
        }
    }
}
#pragma warning restore MCPEXP001
