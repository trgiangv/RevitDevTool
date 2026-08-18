using System.Diagnostics;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
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
                    await PublishRunAsync(EnsureSession(), assemblyPath, run, context).ConfigureAwait(false);
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

    internal static IReadOnlyList<TestNode> DiscoverNodes(
        string assemblyPath,
        TestingSelection selection,
        IReadOnlyList<string> attributeTypeNames)
    {
        var cases = MetadataTestDiscoverer.Filter(
            MetadataTestDiscoverer.Discover(assemblyPath, attributeTypeNames),
            selection.Names,
            selection.TestIds);
        return cases.Select(discovered => ToDiscoveredNode(discovered, assemblyPath)).ToList();
    }

    private async Task PublishDiscoveredAsync(
        string assemblyPath,
        DiscoverTestExecutionRequest request,
        ExecuteRequestContext context)
    {
        var options = _options ?? HostOptionsLoader.Load(RequireConfiguration());
        var filter = ResolveRunnerFilter(request.Filter);
        foreach (var node in DiscoverNodes(assemblyPath, filter, options.DiscoveryAttributes ?? []))
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(request.Session.SessionUid, node))
                .ConfigureAwait(false);
        }
    }

    private async Task PublishRunAsync(
        HostTestSession session,
        string assemblyPath,
        RunTestExecutionRequest request,
        ExecuteRequestContext context)
    {
        var options = ApplyDebugParent(
            _options ?? throw new InvalidOperationException("Host run options were not loaded."));
        var filter = ResolveRunnerFilter(request.Filter);
        var runOptions = ScaleForRun(
            options,
            CountRunTests(assemblyPath, filter, options.DiscoveryAttributes ?? []));
        TestingRunResponse response;
        try
        {
            response = session.Run(
                assemblyPath,
                runOptions,
                filter);
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

        foreach (var result in response.Results)
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        request.Session.SessionUid,
                        ToResultNode(result, assemblyPath)))
                .ConfigureAwait(false);
        }
    }

    internal static int CountRunTests(
        string assemblyPath,
        TestingSelection selection,
        IReadOnlyList<string> attributeTypeNames) =>
        MetadataTestDiscoverer.Filter(
            MetadataTestDiscoverer.Discover(assemblyPath, attributeTypeNames),
            selection.Names,
            selection.TestIds).Count;

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
        AddMethodIdentifier(properties, test.FullName, test.DisplayName, assemblyPath);
        return new TestNode
        {
            Uid = new TestNodeUid(StableUid(test.FullName, test.TestId, test.DisplayName)),
            DisplayName = test.DisplayName,
            Properties = new PropertyBag(properties),
        };
    }

    internal static TestNode ToResultNode(TestingCaseResult result, string? assemblyPath = null)
    {
        var properties = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(properties, result);
        AddMethodIdentifier(properties, result.FullName ?? result.TestId, result.DisplayName, assemblyPath);

        return new TestNode
        {
            Uid = new TestNodeUid(StableUid(result.FullName, result.TestId, result.DisplayName)),
            DisplayName = result.DisplayName,
            Properties = new PropertyBag(properties),
        };
    }

    private static string StableUid(string? fullName, string id, string name)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName!;
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        return name;
    }

    private static void AddMethodIdentifier(
        List<IProperty> properties,
        string? fullName,
        string methodName,
        string? assemblyPath)
    {
        var identity = fullName;
        if (string.IsNullOrWhiteSpace(identity))
            identity = methodName;
        if (string.IsNullOrWhiteSpace(identity))
            return;

        var paren = identity!.IndexOf('(');
        var core = paren >= 0 ? identity.Substring(0, paren) : identity;
        var lastDot = core.LastIndexOf('.');
        var parsedMethod = lastDot < 0 ? core : core.Substring(lastDot + 1);
        var typeFull = lastDot < 0 ? string.Empty : core.Substring(0, lastDot);
        var nsDot = typeFull.LastIndexOf('.');
        var ns = nsDot < 0 ? string.Empty : typeFull.Substring(0, nsDot);
        var typeName = nsDot < 0 ? typeFull : typeFull.Substring(nsDot + 1);
        if (string.IsNullOrWhiteSpace(typeName))
            typeName = parsedMethod;

        properties.Add(new TestMethodIdentifierProperty(
            ResolveAssemblyFullName(assemblyPath),
            ns,
            typeName,
            string.IsNullOrWhiteSpace(parsedMethod) ? methodName : parsedMethod,
            methodArity: 0,
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
            return Path.GetFileNameWithoutExtension(assemblyPath) ?? string.Empty;

        return System.Reflection.Assembly.GetEntryAssembly()?.GetName().FullName ?? string.Empty;
    }

    private static string ResolveTestAssemblyPath()
    {
        var entry = System.Reflection.Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Test entry assembly is not available.");
        return entry.Location;
    }
}
