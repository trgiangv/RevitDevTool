using System.Diagnostics.CodeAnalysis;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;

namespace DevTools.TUnit.Runtime;

#pragma warning disable TPEXP

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed class TUnitEngineCommandLine : ICommandLineOptions
{
    /// <summary>TUnit.Engine <c>ICommandLineOptions</c> name; keep serial execution on the host thread.</summary>
    public const string MaximumParallelTests = "maximum-parallel-tests";

    public static readonly string[] SerialExecution = ["1"];

    public bool IsOptionSet(string optionName) =>
        string.Equals(optionName, MaximumParallelTests, StringComparison.Ordinal);

    public bool TryGetOptionArgumentList(
        string optionName,
        [NotNullWhen(true)] out string[]? arguments)
    {
        if (string.Equals(optionName, MaximumParallelTests, StringComparison.Ordinal))
        {
            arguments = SerialExecution;
            return true;
        }

        arguments = null;
        return false;
    }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed class TUnitEngineConfiguration(string workingDirectory, string resultDirectory) : IConfiguration
{
    public const string ResultDirectory = "platformOptions:resultDirectory";
    public const string CurrentWorkingDirectory = "platformOptions:currentWorkingDirectory";
    public const string TestHostWorkingDirectory = "platformOptions:testHostWorkingDirectory";

    public string? this[string key] => key switch
    {
        ResultDirectory => resultDirectory,
        CurrentWorkingDirectory => workingDirectory,
        TestHostWorkingDirectory => workingDirectory,
        _ => null,
    };
}

internal sealed class TUnitEngineLoggerFactory : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => TUnitEngineLogger.Instance;
}

internal sealed class TUnitEngineLogger : ILogger
{
    public static readonly TUnitEngineLogger Instance = new();

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(
        LogLevel logLevel,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
    }

    public Task LogAsync<TState>(
        LogLevel logLevel,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Task.CompletedTask;
}

internal sealed class TUnitEngineOutputDevice : IOutputDevice
{
    public Task DisplayAsync(
        IOutputDeviceDataProducer dataProducer,
        IOutputDeviceData data,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class TUnitEngineMessageBus : IMessageBus
{
    public Dictionary<string, TestNode> Nodes { get; } = new(StringComparer.Ordinal);

    public Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        if (data is TestNodeUpdateMessage update)
            Nodes[update.TestNode.Uid.Value] = update.TestNode;
        return Task.CompletedTask;
    }
}

internal sealed class TUnitEngineCompletionNotifier : IExecuteRequestCompletionNotifier
{
    public void Complete()
    {
    }
}
