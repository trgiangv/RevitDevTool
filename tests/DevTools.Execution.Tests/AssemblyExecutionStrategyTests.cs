using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class AssemblyExecutionStrategyTests
{
    [Fact]
    public async Task ExecuteAsync_RunsCommandThroughHostContext()
    {
        var command = new CommandItem("sample.dll", "Sample.Command") { Name = "Command" };
        var runner = new Mock<ICommandRunner>();
        runner
            .Setup(r => r.RunCommand(command))
            .Returns(ExecutionResult.Succeeded("ran", 3));
        var progress = new List<string>();
        var strategy = new AssemblyExecutionStrategy(
            command,
            ExecutionTestHelpers.InlineHostContext(),
            runner.Object);
        var result = await strategy.ExecuteAsync(new Progress<string>(message =>
        {
            lock (progress)
                progress.Add(message);
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        runner.Verify(r => r.RunCommand(command), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_ReturnsCancelledResult()
    {
        var command = new CommandItem("sample.dll", "Sample.Command");
        var host = new Mock<IHostContextExecutor>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        host
            .Setup(h => h.ExecuteAsync(It.IsAny<Func<ExecutionResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var strategy = new AssemblyExecutionStrategy(command, host.Object, Mock.Of<ICommandRunner>());
        var result = await strategy.ExecuteAsync(cancellationToken: cts.Token);

        Assert.True(result.IsCancelled);
    }
}
