using DevTools.Daemon.Mcp.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using System.Text.Json.Nodes;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class McpTaskLogFilterTests
{
    [TestMethod]
    public void TasksGet_RequestAndResponse_LogMethodTaskIdAndStatus()
    {
        var logger = new CapturingLogger();
        var filter = new McpTaskLogFilter(logger);

        filter.OnIncoming(new JsonRpcRequest
        {
            Id = new RequestId(7),
            Method = "tasks/get",
            Params = new JsonObject { ["taskId"] = "abc" },
        });
        filter.OnOutgoing(new JsonRpcResponse
        {
            Id = new RequestId(7),
            Result = new JsonObject { ["taskId"] = "abc", ["status"] = "completed" },
        });

        Assert.HasCount(2, logger.Entries);
        Assert.Contains("tasks/get request id=7 taskId=abc", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.AreEqual(LogLevel.Information, logger.Entries[1].Level);
        Assert.Contains("tasks/get ok id=7 taskId=abc status=completed", logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TasksGet_ErrorResponse_LogsWarningWithCodeAndMessage()
    {
        var logger = new CapturingLogger();
        var filter = new McpTaskLogFilter(logger);

        filter.OnIncoming(new JsonRpcRequest
        {
            Id = new RequestId("r1"),
            Method = "tasks/get",
            Params = new JsonObject { ["taskId"] = "missing" },
        });
        filter.OnOutgoing(new JsonRpcError
        {
            Id = new RequestId("r1"),
            Error = new JsonRpcErrorDetail { Code = -32602, Message = "Unknown task: 'missing'" },
        });

        var last = logger.Entries[^1];
        Assert.AreEqual(LogLevel.Warning, last.Level);
        Assert.Contains("tasks/get error id=r1 taskId=missing code=-32602", last.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown task", last.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void NonTaskMessages_AreNotLogged()
    {
        var logger = new CapturingLogger();
        var filter = new McpTaskLogFilter(logger);

        filter.OnIncoming(new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call" });
        filter.OnOutgoing(new JsonRpcResponse { Id = new RequestId(1), Result = new JsonObject() });

        Assert.IsEmpty(logger.Entries);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
