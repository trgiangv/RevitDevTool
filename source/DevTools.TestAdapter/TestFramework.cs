using System.Diagnostics;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.TestAdapter;

internal sealed class TestFramework : ITestFramework, IDataProducer
{
    private readonly ITestRunnerTransport? _injectedTransport;
    private readonly ICommandLineOptions? _commandLine;
    private readonly IConfiguration? _configuration;
    private TestRunSession? _session;
    private ITestRunnerTransport? _ownedTransport;
    private TestRunSettings? _settings;

    internal TestFramework(
        IServiceProvider serviceProvider,
        ITestRunnerTransport? transport = null)
    {
        _commandLine = serviceProvider.GetService(typeof(ICommandLineOptions)) as ICommandLineOptions;
        _configuration = serviceProvider.GetService(typeof(IConfiguration)) as IConfiguration;
        _injectedTransport = transport;
    }

    public string Uid => "DevTools.TestAdapter";

    public string Version => "1.0.0";

    public string DisplayName => "DevTools.TestAdapter";

    public string Description =>
        "Runs tests inside a Revit or AutoCAD-family host. Requires RevitDevTool.";

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        _ = context;
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    }

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        try
        {
            var assemblyPath = ResolveTestAssemblyPath();

            switch (context.Request)
            {
                case DiscoverTestExecutionRequest discover:
                    await PublishDiscoveredAsync(assemblyPath, discover, context)
                        .ConfigureAwait(false);
                    break;
                case RunTestExecutionRequest run:
                    await PublishRunAsync(assemblyPath, run, context).ConfigureAwait(false);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported test request '{context.Request.GetType().FullName}'.");
            }
        }
        finally
        {
            context.Complete();
        }
    }

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        _session?.Cancel();
        _ownedTransport?.Dispose();
        _session = null;
        _ownedTransport = null;
        return Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }

    internal static List<TestNode> DiscoverNodes(
        string assemblyPath,
        TestSelection selection)
    {
        var cases = SelectCases(assemblyPath, selection);
        return cases.Select(discovered => ToDiscoveredNode(discovered, assemblyPath)).ToList();
    }

    private async Task PublishDiscoveredAsync(
        string assemblyPath,
        DiscoverTestExecutionRequest request,
        ExecuteRequestContext context)
    {
        // Host-free: do not read testconfig host options. A throw here is
        // Test Explorer "discovery aborted: 0 Tests found".
        try
        {
            var filter = ResolveRunnerFilter(request.Filter);
            foreach (var node in DiscoverNodes(assemblyPath, filter))
            {
                await context.MessageBus.PublishAsync(
                        this,
                        new TestNodeUpdateMessage(request.Session.SessionUid, node))
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        request.Session.SessionUid,
                        TestNodeProperties.CreateErrorNode(
                            "devtools.testadapter.discover",
                            "Test discovery failed",
                            ex)))
                .ConfigureAwait(false);
        }
    }

    private async Task PublishRunAsync(
        string assemblyPath,
        RunTestExecutionRequest request,
        ExecuteRequestContext context)
    {
        try
        {
            var session = EnsureSession();
            var options = ApplyDebugParent(_settings!.Host);
            var filter = ResolveRunnerFilter(request.Filter);
            var bridge = RequireBridge();
            var cases = bridge.Discoverer.Discover(assemblyPath, filter);
            var hostSelection = bridge.RunMapper.ToRunSelection(filter, cases);
            var publish = new RunPublish(
                context,
                request,
                bridge,
                filter,
                cases,
                assemblyPath,
                new HashSet<string>(StringComparer.Ordinal));
            var testCount = Math.Max(cases.Count, IdCount(filter));
            if (await TryPublishEmptyConstrainedAsync(publish, testCount).ConfigureAwait(false))
                return;

            if (context.CancellationToken.IsCancellationRequested)
            {
                await PublishRunErrorAsync(context, request, new OperationCanceledException())
                    .ConfigureAwait(false);
                return;
            }

            if (!TryRunHost(
                    session,
                    publish,
                    ScaleForRun(options, testCount),
                    hostSelection,
                    out var response,
                    out var runError))
            {
                await PublishHostFailureAsync(publish, runError!).ConfigureAwait(false);
                return;
            }

            await PublishCompletedRunAsync(publish, response).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await PublishRunErrorAsync(context, request, ex).ConfigureAwait(false);
        }
    }

    private static int IdCount(TestSelection filter) =>
        filter.Kind == TestSelectionKind.TestIds ? filter.TestIds.Count : 0;

    private readonly record struct RunPublish(
        ExecuteRequestContext Context,
        RunTestExecutionRequest Request,
        TestingDiscoveryBridge Bridge,
        TestSelection Filter,
        IReadOnlyList<TestDiscoveredTest> Cases,
        string AssemblyPath,
        HashSet<string> StreamedIds);

    private async Task<bool> TryPublishEmptyConstrainedAsync(RunPublish publish, int testCount)
    {
        if (testCount != 0 || !publish.Filter.IsConstrained)
            return false;

        await PublishResultNodesAsync(
                publish,
                publish.Bridge.RunMapper.ResultsForUnreported(publish.Filter, publish.Cases, []))
            .ConfigureAwait(false);
        return true;
    }

    private bool TryRunHost(
        TestRunSession session,
        RunPublish publish,
        TestHostOptions runOptions,
        TestSelection hostSelection,
        out TestRunResponse response,
        out Exception? runError)
    {
        try
        {
            var discoveredIds = publish.Cases.Select(test => test.TestId).ToHashSet(StringComparer.Ordinal);
            using (publish.Context.CancellationToken.Register(session.Cancel))
            {
                response = session.Run(
                    publish.AssemblyPath,
                    runOptions,
                    _settings!.FrameworkId,
                    hostSelection,
                    testingEvent => PublishStreamedCase(publish, discoveredIds, testingEvent));
            }

            runError = null;
            return true;
        }
        catch (Exception ex)
        {
            response = null!;
            runError = ex;
            return false;
        }
    }

    private void PublishStreamedCase(
        RunPublish publish,
        HashSet<string> discoveredIds,
        TestEvent testingEvent)
    {
        if (testingEvent.Case is not { } streamed)
            return;
        if (!discoveredIds.Contains(streamed.TestId))
            return;

        publish.StreamedIds.Add(streamed.TestId);
        publish.Context.MessageBus.PublishAsync(
                this,
                new TestNodeUpdateMessage(
                    publish.Request.Session.SessionUid,
                    ToResultNode(streamed, publish.AssemblyPath, publish.Cases)))
            .GetAwaiter()
            .GetResult();
    }

    private async Task PublishHostFailureAsync(RunPublish publish, Exception runError)
    {
        var unreported = await PublishUnreportedAsync(publish, [], runError.ToString())
            .ConfigureAwait(false);
        if (unreported == 0)
            await PublishRunErrorAsync(publish.Context, publish.Request, runError).ConfigureAwait(false);
    }

    private async Task PublishCompletedRunAsync(RunPublish publish, TestRunResponse response)
    {
        var published = publish.Bridge.RunMapper.FoldResults(publish.Filter, publish.Cases, response.Results);
        await PublishResultNodesAsync(publish, NotYetPublished(published, publish.StreamedIds))
            .ConfigureAwait(false);
        var overlay = DiagnosticOverlay(response);
        var unreportedCount = await PublishUnreportedAsync(publish, published, overlay)
            .ConfigureAwait(false);
        if (unreportedCount == 0 && published.Count == 0 && !string.IsNullOrWhiteSpace(overlay))
        {
            await PublishRunErrorAsync(
                    publish.Context,
                    publish.Request,
                    new InvalidOperationException(overlay))
                .ConfigureAwait(false);
        }
    }

    private static string? DiagnosticOverlay(TestRunResponse response)
    {
        var overlay = response.DiagnosticMessage ?? response.DiagnosticCode;
        if (response.CancellationState is TestCancellationState.Completed
            or TestCancellationState.Poisoned)
        {
            overlay ??= $"Run ended with cancellation state {response.CancellationState}.";
        }

        return overlay;
    }

    private static IReadOnlyList<TestCaseResult> NotYetPublished(
        IReadOnlyList<TestCaseResult> folded,
        HashSet<string> streamedIds)
    {
        if (streamedIds.Count == 0)
            return folded;

        return folded.Where(result => !streamedIds.Contains(result.TestId)).ToList();
    }

    private async Task PublishResultNodesAsync(RunPublish publish, IReadOnlyList<TestCaseResult> results)
    {
        foreach (var result in results)
        {
            await publish.Context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        publish.Request.Session.SessionUid,
                        ToResultNode(result, publish.AssemblyPath, publish.Cases)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Csproj <c>PerTestTimeout</c> is per test. <see cref="TestHostOptions.RequestTimeoutSeconds"/>
    /// is the pipe wait for this run (<c>PerTestTimeout × test count</c>) and is
    /// sent separately so the per-test field does not change meaning.
    /// </summary>
    internal static TestHostOptions ScaleForRun(TestHostOptions options, int testCount) =>
        options with
        {
            RequestTimeoutSeconds = TestHostTiming.ScalePerTestTimeoutSeconds(
                options.PerTestTimeoutSeconds,
                testCount),
        };

    private static TestHostOptions ApplyDebugParent(TestHostOptions options) =>
        Debugger.IsAttached
            ? options with { DebugParentPid = Process.GetCurrentProcess().Id }
            : options;

    internal static TestSelection ToRunnerFilter(
        ITestExecutionFilter? filter,
        string? nameFilter = null)
    {
        if (filter is TestNodeUidListFilter uidFilter)
        {
            var uids = uidFilter.TestNodeUids
                .Select(uid => uid.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return TestSelection.FromTestIds(uids);
        }

        if (string.IsNullOrWhiteSpace(nameFilter))
            return TestSelection.All;

        return TestSelection.FromNames([nameFilter!.Trim()]);
    }

    internal static IReadOnlyList<TestDiscoveredTest> SelectCases(
        string assemblyPath,
        TestSelection selection) =>
        RequireDiscoverer().Discover(assemblyPath, selection);

    private async Task<int> PublishUnreportedAsync(
        RunPublish publish,
        IReadOnlyList<TestCaseResult> hostResults,
        string? overlayMessage)
    {
        var published = 0;
        foreach (var missing in publish.Bridge.RunMapper.ResultsForUnreported(
                     publish.Filter, publish.Cases, hostResults))
        {
            var result = string.IsNullOrWhiteSpace(overlayMessage)
                ? missing
                : missing with { Message = overlayMessage };
            await publish.Context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        publish.Request.Session.SessionUid,
                        ToResultNode(result, publish.AssemblyPath, publish.Cases)))
                .ConfigureAwait(false);
            published++;
        }

        return published;
    }

    private async Task PublishRunErrorAsync(
        ExecuteRequestContext context,
        RunTestExecutionRequest request,
        Exception exception)
    {
        await context.MessageBus.PublishAsync(
                this,
                new TestNodeUpdateMessage(
                    request.Session.SessionUid,
                    TestNodeProperties.CreateErrorNode(
                        "devtools.testadapter.run",
                        "Test run failed",
                        exception)))
            .ConfigureAwait(false);
    }

    private TestSelection ResolveRunnerFilter(ITestExecutionFilter? filter) =>
        ToRunnerFilter(filter, ReadOption(TestCommandLineProvider.FilterOptionName));

    private string? ReadOption(string name)
    {
        if (_commandLine is null)
            return null;

        return _commandLine.TryGetOptionArgumentList(name, out var arguments)
               && arguments is { Length: > 0 }
            ? arguments[0]
            : null;
    }

    private IConfiguration RequireConfiguration() =>
        _configuration ?? throw new InvalidOperationException(
            "Microsoft.Testing.Platform IConfiguration is required to read the devtools section of testconfig.json.");

    private static TestingDiscoveryBridge RequireBridge() =>
        TestingDiscovery.Current
        ?? throw new InvalidOperationException(
            "Local discovery requires TestingDiscovery.Register from the selected MTP provider hook. "
            + "Set TestingFramework to nunit or tunit so RevitDevTool.TestAdapter references the matching sibling.");

    private static ITestDiscoverer RequireDiscoverer() => RequireBridge().Discoverer;

    private TestRunSession EnsureSession()
    {
        if (_session is not null)
            return _session;

        _settings = TestRunSettingsLoader.Load(RequireConfiguration());
        if (_injectedTransport is not null)
        {
            _session = new TestRunSession(_injectedTransport);
            return _session;
        }

        var runnerPath = TestingRunnerPaths.ResolveRunnerPath(_settings.RunnerPath);
        _ownedTransport = new ProcessTestRunnerClient(runnerPath);
        _session = new TestRunSession(_ownedTransport);
        return _session;
    }

    internal static TestNode ToDiscoveredNode(TestDiscoveredTest test, string? assemblyPath = null)
    {
        var properties = new List<IProperty> { DiscoveredTestNodeStateProperty.CachedInstance };
        AddMethodIdentifier(properties, test, assemblyPath);
        TestNodeProperties.AddSource(properties, test.Source);
        return new TestNode
        {
            Uid = new TestNodeUid(OpaqueUid(test.TestId, test.FullName, test.DisplayName)),
            DisplayName = test.DisplayName,
            Properties = new PropertyBag(properties),
        };
    }

    internal static TestNode ToResultNode(
        TestCaseResult result,
        string? assemblyPath = null,
        IReadOnlyList<TestDiscoveredTest>? discovered = null)
    {
        var properties = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(properties, result);
        AddMethodIdentifier(properties, FindDiscovered(discovered, result), assemblyPath);

        return new TestNode
        {
            Uid = new TestNodeUid(OpaqueUid(result.TestId, result.FullName, result.DisplayName)),
            DisplayName = result.DisplayName,
            Properties = new PropertyBag(properties),
        };
    }

    private static TestDiscoveredTest? FindDiscovered(
        IReadOnlyList<TestDiscoveredTest>? discovered,
        TestCaseResult result)
    {
        if (discovered is null || discovered.Count == 0)
            return null;

        return discovered.FirstOrDefault(test => string.Equals(test.TestId, result.TestId, StringComparison.Ordinal));
    }

    private static string OpaqueUid(string id, string? fullName, string name)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName!;
        return name;
    }

    private static void AddMethodIdentifier(
        List<IProperty> properties,
        TestDiscoveredTest? test,
        string? assemblyPath)
    {
        if (test is null)
            return;
        if (string.IsNullOrWhiteSpace(test.TypeName) || string.IsNullOrWhiteSpace(test.MethodName))
            return;

        properties.Add(new TestMethodIdentifierProperty(
            ResolveAssemblyFullName(assemblyPath),
            test.Namespace ?? string.Empty,
            test.TypeName!,
            test.MethodName!,
            test.MethodArity,
            ToParameterTypes(test.ParameterTypeFullNames),
            string.IsNullOrWhiteSpace(test.ReturnTypeFullName) ? "System.Void" : test.ReturnTypeFullName!));
    }

    private static string[] ToParameterTypes(IReadOnlyList<string>? types)
    {
        if (types is null || types.Count == 0)
            return [];
        if (types is string[] array)
            return array;
        return types.ToArray();
    }

    private static string ResolveAssemblyFullName(string? assemblyPath)
    {
        if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
        {
            try
            {
                return System.Reflection.AssemblyName.GetAssemblyName(assemblyPath!).FullName;
            }
            catch
            {
                // Fall through to the file name.
            }
        }

        if (!string.IsNullOrWhiteSpace(assemblyPath))
            return Path.GetFileNameWithoutExtension(assemblyPath);

        return System.Reflection.Assembly.GetEntryAssembly()?.GetName().FullName ?? string.Empty;
    }

    private static string ResolveTestAssemblyPath()
    {
        var entry = System.Reflection.Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Test entry assembly is not available.");
        return entry.Location;
    }
}
