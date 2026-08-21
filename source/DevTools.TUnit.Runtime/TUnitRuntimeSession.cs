using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using TUnit.Engine.Extensions;

namespace DevTools.TUnit.Runtime;

public sealed class TUnitRuntimeSession : ITestingRuntimeSession
{
    private readonly Assembly _testAssembly;
    private readonly string _assemblyPath;
    private readonly object _gate = new();
    private bool _disposed;

    public TUnitRuntimeSession(Assembly testAssembly, string assemblyPath, string generationId)
    {
        _testAssembly = testAssembly ?? throw new ArgumentNullException(nameof(testAssembly));
        _assemblyPath = Path.GetFullPath(Required(assemblyPath, nameof(assemblyPath)));
        GenerationId = Required(generationId, nameof(generationId));
    }

    public string GenerationId { get; }

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAssembly(request.Assembly.Path);

            RuntimeHelpers.RunModuleConstructor(_testAssembly.ManifestModule.ModuleHandle);
            var consumer = new ResultConsumer(request.RunId, eventSink);
            var previousHtmlSetting = Environment.GetEnvironmentVariable("TUNIT_DISABLE_HTML_REPORTER");
            Environment.SetEnvironmentVariable("TUNIT_DISABLE_HTML_REPORTER", "true");
            try
            {
                var builder = TestApplication.CreateBuilderAsync(BuildArguments(request))
                    .GetAwaiter().GetResult();
                builder.AddTUnit();
                builder.TestHost.AddDataConsumer(_ => consumer);
                using var application = builder.BuildAsync().GetAwaiter().GetResult();
                _ = application.RunAsync().GetAwaiter().GetResult();
            }
            finally
            {
                Environment.SetEnvironmentVariable("TUNIT_DISABLE_HTML_REPORTER", previousHtmlSetting);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var results = consumer.Results;
            return new TestingRunResponse(
                request.RunId,
                request.FrameworkId,
                GenerationId,
                results,
                TestingCancellationState.None,
                null,
                null);
        }
    }

    public void Cancel(Guid runId)
    {
        _ = runId;
    }

    public void Dispose()
    {
        lock (_gate)
            _disposed = true;
    }

    private static string[] BuildArguments(TestingRunRequest request)
    {
        var arguments = new List<string>
        {
            "--no-ansi",
            "--progress", "off",
            "--maximum-parallel-tests", "1",
        };

        var testIds = request.Selection.TestIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (testIds.Count > 0)
        {
            arguments.Add("--filter-uid");
            arguments.AddRange(testIds);
        }

        return arguments.ToArray();
    }

    private void ValidateAssembly(string requestAssemblyPath)
    {
        var normalized = Path.GetFullPath(requestAssemblyPath);
        if (!string.Equals(normalized, _assemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Assembly path '{normalized}' does not match the TUnit session assembly '{_assemblyPath}'.",
                nameof(requestAssemblyPath));
        }
    }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value;

    private sealed class ResultConsumer(Guid runId, ITestingRuntimeEventSink eventSink) : IDataConsumer
    {
        private readonly Dictionary<string, TestingCaseResult> _results = new(StringComparer.Ordinal);

        public string Uid => "DevTools.TUnit.Runtime.Results";
        public string Version => "1.0.0";
        public string DisplayName => "RevitDevTool TUnit result bridge";
        public string Description => "Maps TUnit MTP result nodes to the neutral host-testing contract.";
        public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyList<TestingCaseResult> Results => _results.Values.ToList();

        public Task ConsumeAsync(
            IDataProducer dataProducer,
            IData value,
            CancellationToken cancellationToken)
        {
            _ = dataProducer;
            cancellationToken.ThrowIfCancellationRequested();
            if (value is not TestNodeUpdateMessage update || TryMap(update.TestNode) is not { } result)
                return Task.CompletedTask;

            _results[result.TestId] = result;
            eventSink.Publish(new TestingRuntimeEvent(
                runId,
                TestingEventKinds.Case,
                result,
                null,
                null,
                TestingCancellationState.None));
            return Task.CompletedTask;
        }

        private static TestingCaseResult? TryMap(TestNode node)
        {
            var state = node.Properties.OfType<TestNodeStateProperty>().LastOrDefault();
            if (state is null
                || state is DiscoveredTestNodeStateProperty
                || state is InProgressTestNodeStateProperty)
                return null;

            var outcome = state switch
            {
                PassedTestNodeStateProperty => TestingOutcomes.Passed,
                SkippedTestNodeStateProperty => TestingOutcomes.Skipped,
                FailedTestNodeStateProperty => TestingOutcomes.Failed,
                _ => TestingOutcomes.Error,
            };
            var exception = state switch
            {
                FailedTestNodeStateProperty failed => failed.Exception,
                ErrorTestNodeStateProperty error => error.Exception,
                _ => null,
            };
            var timing = node.Properties.OfType<TimingProperty>().LastOrDefault();
            var output = string.Join(
                Environment.NewLine,
                node.Properties.OfType<StandardOutputProperty>().Select(property => property.StandardOutput));
            var location = node.Properties.OfType<TestFileLocationProperty>().LastOrDefault();

            return new TestingCaseResult(
                node.Uid.Value,
                node.DisplayName,
                outcome,
                timing?.GlobalTiming.Duration.TotalMilliseconds ?? 0,
                exception?.Message ?? state.Explanation,
                exception?.StackTrace,
                string.IsNullOrWhiteSpace(output) ? null : output,
                location is null
                    ? null
                    : new TestingSourceLocation(location.FilePath, location.LineSpan.Start.Line),
                [],
                []);
        }
    }
}
