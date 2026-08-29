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

internal sealed class HostTestFramework : ITestFramework, IDataProducer
{
    private readonly ITestRunnerTransport? _injectedTransport;
    private readonly ICommandLineOptions? _commandLine;
    private readonly IConfiguration? _configuration;
    private HostTestSession? _session;
    private ITestRunnerTransport? _ownedTransport;
    private TestingHostOptions? _options;

    internal HostTestFramework(
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
        TestingSelection selection)
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
            var options = ApplyDebugParent(_options!);
            var filter = ResolveRunnerFilter(request.Filter);
            var discoverer = RequireDiscoverer();
            var cases = discoverer.Select(assemblyPath, filter, TestingDiscoveryOptions.Testhost);
            var mapper = RequireRunMapper();
            var hostSelection = mapper.ToHostSelection(filter, cases);
            var testCount = Math.Max(cases.Count, filter.TestIds.Count);
            if (testCount == 0 && IsConstrained(filter))
            {
                foreach (var missing in mapper.ResultsForUnreported(filter, cases, []))
                {
                    await context.MessageBus.PublishAsync(
                            this,
                            new TestNodeUpdateMessage(
                                request.Session.SessionUid,
                                ToResultNode(missing, assemblyPath, cases)))
                        .ConfigureAwait(false);
                }

                return;
            }

            var runOptions = ScaleForRun(options, testCount);
            TestingRunResponse response;
            try
            {
                response = session.Run(
                    assemblyPath,
                    runOptions,
                    hostSelection);
            }
            catch (Exception ex)
            {
                await context.MessageBus.PublishAsync(
                        this,
                        new TestNodeUpdateMessage(
                            request.Session.SessionUid,
                            TestNodeProperties.CreateErrorNode("devtools.testadapter.runner", "DevTools.TestAdapter", ex)))
                    .ConfigureAwait(false);
                return;
            }

            var published = mapper.FoldResults(filter, cases, response.Results);
            foreach (var result in published)
            {
                await context.MessageBus.PublishAsync(
                        this,
                        new TestNodeUpdateMessage(
                            request.Session.SessionUid,
                            ToResultNode(result, assemblyPath, cases)))
                    .ConfigureAwait(false);
            }

            foreach (var missing in mapper.ResultsForUnreported(filter, cases, published))
            {
                await context.MessageBus.PublishAsync(
                        this,
                        new TestNodeUpdateMessage(
                            request.Session.SessionUid,
                            ToResultNode(missing, assemblyPath, cases)))
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
                            "devtools.testadapter.run",
                            "Test run failed",
                            ex)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Csproj <c>PerTestTimeout</c> is per test. After this, <see cref="TestingHostOptions.PerTestTimeoutSeconds"/>
    /// is the scaled pipe wait (<c>PerTestTimeout × test count</c>) that TestRunner
    /// receives as <c>--per-test-timeout</c>.
    /// </summary>
    internal static TestingHostOptions ScaleForRun(TestingHostOptions options, int testCount) =>
        options with
        {
            PerTestTimeoutSeconds = TestingHostTiming.ScalePerTestTimeoutSeconds(
                options.PerTestTimeoutSeconds,
                testCount),
        };

    internal static TestingSelection ToRunnerFilter(
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
            return new TestingSelection(uids);
        }

        if (string.IsNullOrWhiteSpace(nameFilter))
            return new TestingSelection([]);

        return new TestingSelection([], Names: [nameFilter!.Trim()]);
    }

    internal static IReadOnlyList<TestingDiscoveredTest> SelectCases(
        string assemblyPath,
        TestingSelection selection) =>
        RequireDiscoverer().Select(assemblyPath, selection, TestingDiscoveryOptions.Testhost);

    private static bool IsConstrained(TestingSelection selection) =>
        selection.TestIds is { Count: > 0 } || selection.Names is { Count: > 0 };

    private TestingSelection ResolveRunnerFilter(ITestExecutionFilter? filter) =>
        ToRunnerFilter(filter, ReadOption(HostCommandLineProvider.FilterOptionName));

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

    private static IHostTestRunMapper RequireRunMapper() =>
        HostTestDiscovery.RunMapper ?? HostTestRunMappers.PassThrough;

    private static IHostTestDiscoverer RequireDiscoverer()
    {
        if (HostTestDiscovery.Provider is { } provider)
            return provider;

        var detail = HostMtpRegistration.LastError;
        var suffix = string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : " " + detail;
        var assemblyName = AdapterTestConfig.TryReadMtpAssembly() ?? "mtpAssembly";
        throw new InvalidOperationException(
            $"Local discovery requires {assemblyName} next to the test executable. "
            + "RevitDevTool.TestAdapter copies the selected sibling at build; do not add it as a ProjectReference."
            + suffix);
    }

    private static TestingHostOptions ApplyDebugParent(TestingHostOptions options) =>
        Debugger.IsAttached
            ? options with { DebugParentPid = Process.GetCurrentProcess().Id }
            : options;

    private HostTestSession EnsureSession()
    {
        if (_session is not null)
            return _session;

        _options = HostOptionsLoader.Load(RequireConfiguration());
        if (_injectedTransport is not null)
        {
            _session = new HostTestSession(_injectedTransport);
            return _session;
        }

        var runnerPath = TestingRunnerPaths.ResolveRunnerPath(_options.RunnerPath);
        _ownedTransport = new ProcessTestRunnerClient(runnerPath);
        _session = new HostTestSession(_ownedTransport);
        return _session;
    }

    internal static TestNode ToDiscoveredNode(TestingDiscoveredTest test, string? assemblyPath = null)
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
        TestingCaseResult result,
        string? assemblyPath = null,
        IReadOnlyList<TestingDiscoveredTest>? discovered = null)
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

    private static TestingDiscoveredTest? FindDiscovered(
        IReadOnlyList<TestingDiscoveredTest>? discovered,
        TestingCaseResult result)
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
        TestingDiscoveredTest? test,
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
            [],
            "System.Void"));
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
