using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

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

        var result = await InvokeAsync(tool, TestContext.Current.CancellationToken);

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

        var result = await InvokeAsync(tool, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(PytestMcpErrorCodes.DependencyPreparationFailed, result.StructuredContent!.Value.GetProperty("status").GetString());
        Assert.DoesNotContain("Pixi unavailable", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationDuringPreparation_IsPropagatedInsteadOfReturningInfrastructureError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var tool = CreateTool();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeAsync(tool, cancellation.Token));
    }

    [Fact]
    public async Task CallerCancellationDuringRunner_IsPropagatedInsteadOfReturningInfrastructureError()
    {
        using var cancellation = new CancellationTokenSource();
        var tool = CreateTool(execution: new RecordingExecutionService(
            PassingCase(),
            failure: new OperationCanceledException(cancellation.Token),
            beforeFailure: cancellation.Cancel));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeAsync(tool, cancellation.Token));
    }

    [Fact]
    public async Task Run_SuppressesGuardAndPassesCancellationToHostContext()
    {
        var host = new RecordingHostContextExecutor();
        var tool = CreateTool(host: host);

        await InvokeAsync(tool, TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionGuardMode.Suppress, host.ObservedMode);
        Assert.Equal(TestContext.Current.CancellationToken, host.ObservedToken);
    }

    [Fact]
    public async Task Progress_IsMonotonicAndStopsBeforeFinalResult()
    {
        var events = new List<string>();
        var progress = new RecordingProgress(events);
        var execution = new RecordingExecutionService(
            PassingCases(2),
            [PassingResult("tests/test_sample.py::first"), PassingResult("tests/test_sample.py::second")]);
        var tool = CreateTool(execution: execution);

        await InvokeAsync(
            tool,
            TestContext.Current.CancellationToken,
            progress,
            ["tests/test_sample.py::first", "tests/test_sample.py::second"]);
        events.Add("result");

        Assert.Equal([0f, 1f, 2f, 3f], progress.Values.Select(item => item.Progress));
        Assert.All(progress.Values, item => Assert.Equal((float?)3f, item.Total));
        Assert.Equal(["progress", "progress", "progress", "progress", "result"], events);
    }

    [Fact]
    public async Task Cancellation_DuringExecutionCompletesCleanupBeforePropagation()
    {
        using var cancellation = new CancellationTokenSource();
        var execution = new RecordingExecutionService(PassingCase(), beforeFailure: cancellation.Cancel);
        var tool = CreateTool(execution: execution);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeAsync(tool, cancellation.Token));

        Assert.True(execution.Executed);
    }

    [Fact]
    public async Task Cancellation_AfterHostCallbackEntryWaitsForRunnerCleanup()
    {
        using var cancellation = new CancellationTokenSource();
        var host = new EarlyCancellationHostContextExecutor();
        var execution = new BlockingExecutionService();
        var tool = CreateTool(host: host, execution: execution);

        var invocation = InvokeAsync(tool, cancellation.Token);
        await execution.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await host.CancellationCompletionSignaled.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.False(invocation.IsCompleted);
        execution.Release.TrySetResult(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        Assert.True(execution.CleanupObserved);
    }

    [Fact]
    public async Task Progress_CountsEachNodeOnceAcrossPhasesAndSetupFailure()
    {
        var events = new List<string>();
        var progress = new RecordingProgress(events);
        var execution = new RecordingExecutionService(
            PassingCases(2),
            [
                Result("first", "passed", "setup"),
                Result("first", "passed", "call"),
                Result("first", "passed", "teardown"),
                Result("second", "error", "setup")
            ]);
        var tool = CreateTool(execution: execution);

        await InvokeAsync(tool, TestContext.Current.CancellationToken, progress, ["first", "second"]);

        Assert.Equal([0f, 1f, 2f, 3f], progress.Values.Select(item => item.Progress));
        Assert.All(progress.Values, item => Assert.True(item.Progress <= item.Total));
    }

    [Fact]
    public async Task Logging_RecordsSafeRequestLifecycleWithoutPytestSourcesOrArguments()
    {
        var logger = new RecordingLogger<PytestRunTool>();
        var tool = CreateTool(logger: logger);
        var workspaceRoot = Path.GetTempPath();
        const string nodeId = "private_tests/test_internal.py::test_never_log_me";
        const string pytestArgument = "--sensitive-option=do-not-log";

        await tool.RunAsync(
            workspaceRoot,
            workspaceRoot,
            [nodeId],
            [pytestArgument],
            null!,
            null!,
            null!,
            TestContext.Current.CancellationToken);

        var messages = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("Pytest MCP request started. NodeCount=1", messages);
        Assert.Contains("Pytest MCP request ended.", messages);
        Assert.Contains("ExitCode=0", messages);
        Assert.Contains("Passed=1", messages);
        Assert.Contains("Cancelled=False", messages);
        Assert.DoesNotContain(workspaceRoot, messages, StringComparison.Ordinal);
        Assert.DoesNotContain(nodeId, messages, StringComparison.Ordinal);
        Assert.DoesNotContain(pytestArgument, messages, StringComparison.Ordinal);
    }

    private static PytestRunTool CreateTool(
        PytestRunResponse? response = null,
        IHostContextExecutor? host = null,
        PytestDependencyService? dependencies = null,
        PytestExecutionService? execution = null,
        ILogger<PytestRunTool>? logger = null)
    {
        return new PytestRunTool(
            host ?? new RecordingHostContextExecutor(),
            dependencies ?? new RecordingDependencyService(),
            execution ?? new RecordingExecutionService(response ?? PassingCase()),
            logger ?? NullLogger<PytestRunTool>.Instance);
    }

    private static async Task<CallToolResult> InvokeAsync(
        PytestRunTool tool,
        CancellationToken cancellationToken = default,
        IProgress<ProgressNotificationValue>? progress = null,
        string[]? nodeIds = null)
    {
        var workspaceRoot = Path.GetTempPath();
        return await tool.RunAsync(
            workspaceRoot,
            workspaceRoot,
            nodeIds ?? ["tests/test_sample.py::test_runs"],
            ["-q"],
            progress!,
            null!,
            null!,
            cancellationToken == default ? TestContext.Current.CancellationToken : cancellationToken);
    }

    private static PytestRunResponse PassingCase() => new(
        0,
        new PytestSummary(1, 0, 0, 0, 0, 0),
        [],
        [],
        Path.GetTempPath());

    private static PytestRunResponse PassingCases(int count) => new(
        0,
        new PytestSummary(count, 0, 0, 0, 0, 0),
        [],
        [],
        Path.GetTempPath());

    private static PytestCaseResult PassingResult(string nodeId) => new(nodeId, "passed", "call", 1, "", "", "", "");

    private static PytestCaseResult Result(string nodeId, string outcome, string phase) =>
        new(nodeId, outcome, phase, 1, "", "", "", "");

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

    private sealed class EarlyCancellationHostContextExecutor : IHostContextExecutor
    {
        public TaskCompletionSource<bool> CancellationCompletionSignaled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() =>
            {
                completion.TrySetCanceled(token);
                CancellationCompletionSignaled.TrySetResult(true);
            });
            _ = Task.Run(() =>
            {
                try
                {
                    completion.TrySetResult(handler());
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });
            return completion.Task;
        }

        public Task ExecuteAsync(Action action, CancellationToken token = default) =>
            ExecuteAsync(() =>
            {
                action();
                return true;
            }, token);
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

    private sealed class RecordingExecutionService(
        PytestRunResponse response,
        IReadOnlyList<PytestCaseResult>? progressResults = null,
        Exception? failure = null,
        Action? beforeFailure = null) : PytestExecutionService(null!)
    {
        public bool Executed { get; private set; }

        public override PytestRunResponse Run(
            PytestRunRequest request,
            Action<string>? progressCallback,
            CancellationToken cancellationToken)
        {
            Executed = true;
            foreach (var progressResult in progressResults ?? [])
                progressCallback?.Invoke(JsonSerializer.Serialize(progressResult));
            beforeFailure?.Invoke();
            if (failure is not null)
                throw failure;
            return response;
        }
    }

    private sealed class BlockingExecutionService : PytestExecutionService
    {
        public BlockingExecutionService() : base(null!) { }

        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CleanupObserved { get; private set; }

        public override PytestRunResponse Run(
            PytestRunRequest request,
            Action<string>? progressCallback,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            Release.Task.GetAwaiter().GetResult();
            CleanupObserved = true;
            return PassingCase();
        }
    }

    private sealed class RecordingProgress(IList<string> events) : IProgress<ProgressNotificationValue>
    {
        public List<ProgressNotificationValue> Values { get; } = [];

        public void Report(ProgressNotificationValue value)
        {
            Values.Add(value);
            events.Add("progress");
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose() { }
    }
}
#pragma warning restore MCPEXP001
