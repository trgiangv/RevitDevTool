using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Mtp;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.MSTest.Runtime;

internal sealed class MSTestNodeConsumer : IDataConsumer
{
    private readonly TestRunTraceScope _traceScope;
    private readonly MSTestConsoleCapture _consoleCapture;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, TestNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MSTestCaseOutput> _capturedByUid = new(StringComparer.Ordinal);
    private readonly HashSet<string> _consoleForwarded = new(StringComparer.Ordinal);

    public MSTestNodeConsumer(TestRunTraceScope traceScope, MSTestConsoleCapture consoleCapture)
    {
        ArgumentNullException.ThrowIfNull(traceScope);
        ArgumentNullException.ThrowIfNull(consoleCapture);
        _traceScope = traceScope;
        _consoleCapture = consoleCapture;
    }

    public string Uid => "devtools-mstest-node-results";

    public string Version => "1.0.0";

    public string DisplayName => "MSTest node results";

    public string Description => "Collects TestNodeUpdateMessage values for the in-host MSTest session.";

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public IReadOnlyList<TestNode> Snapshot()
    {
        lock (_gate)
            return _nodes.Values.ToList();
    }

    public IReadOnlyDictionary<string, MSTestCaseOutput> CapturedByUid
    {
        get
        {
            lock (_gate)
                return new Dictionary<string, MSTestCaseOutput>(_capturedByUid, StringComparer.Ordinal);
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer dataProducer, IData data, CancellationToken cancellationToken)
    {
        if (data is TestNodeUpdateMessage update)
            Capture(update.TestNode);

        return Task.CompletedTask;
    }

    private void Capture(TestNode node)
    {
        var uid = node.Uid.Value;
        string? frameworkOutput = null;
        lock (_gate)
        {
            _nodes[uid] = node;
            if (!MtpNodeResults.HasTerminalState(node))
                return;

            if (!_capturedByUid.ContainsKey(uid))
            {
                var console = _consoleCapture.CompleteCase(node.DisplayName);
                var trace = _traceScope.CompleteCase();
                _capturedByUid[uid] = new MSTestCaseOutput(console, trace);
            }

            if (!_consoleForwarded.Contains(uid))
            {
                var captured = _capturedByUid[uid];
                var frameworkOutputText = MtpNodeResults.FrameworkOutput(node);
                frameworkOutput = string.IsNullOrWhiteSpace(frameworkOutputText)
                    ? captured.Console
                    : frameworkOutputText;
                if (!string.IsNullOrWhiteSpace(frameworkOutput))
                    _consoleForwarded.Add(uid);
            }
        }

        if (!string.IsNullOrWhiteSpace(frameworkOutput))
            _traceScope.WriteThrough(frameworkOutput);
    }
}
