using System.Diagnostics;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Mtp;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.Mtp;

internal sealed class DevToolsNUnitFramework : ITestFramework, IDataProducer
{
    private readonly ITestRunnerTransport? _injectedTransport;
    private readonly ICommandLineOptions? _commandLine;
    private DevToolsNUnitSession? _session;
    private ITestRunnerTransport? _ownedTransport;
    private HostRunOptions? _options;

    internal DevToolsNUnitFramework(
        IServiceProvider serviceProvider,
        ITestRunnerTransport? transport = null)
    {
        _commandLine = serviceProvider.GetService(typeof(ICommandLineOptions)) as ICommandLineOptions;
        _injectedTransport = transport;
    }

    public string Uid => "DevTools.NUnit";

    public string Version => "1.0.0";

    public string DisplayName => "DevTools.NUnit";

    public string Description =>
        "Runs NUnit tests inside a Revit or AutoCAD-family host. Requires RevitDevTool.";

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
                        $"Unsupported MTP request '{context.Request.GetType().FullName}'.");
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

    internal static IReadOnlyList<TestNode> DiscoverNodes(string assemblyPath, RunnerTestFilter filter)
    {
        var cases = NUnitMetadataDiscoverer.Filter(
            NUnitMetadataDiscoverer.Discover(assemblyPath),
            filter.Names,
            filter.FullNames);
        return cases.Select(discovered => ToDiscoveredNode(discovered, assemblyPath)).ToList();
    }

    private async Task PublishDiscoveredAsync(
        string assemblyPath,
        DiscoverTestExecutionRequest request,
        ExecuteRequestContext context)
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

    private async Task PublishRunAsync(
        DevToolsNUnitSession session,
        string assemblyPath,
        RunTestExecutionRequest request,
        ExecuteRequestContext context)
    {
        var options = ApplyDebugParent(
            _options ?? throw new InvalidOperationException("Host run options were not loaded."));
        var filter = ResolveRunnerFilter(request.Filter);
        TestingRunResponse response;
        try
        {
            response = session.Run(
                assemblyPath,
                NUnitTestingMapping.ToHostOptions(options),
                NUnitTestingMapping.ToSelection(filter));
        }
        catch (Exception ex)
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        request.Session.SessionUid,
                        TestingMtpSession.CreateErrorNode("devtools.nunit.runner", "DevTools.NUnit", ex)))
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

    internal static RunnerTestFilter ToRunnerFilter(
        ITestExecutionFilter? filter,
        string? nameFilter = null)
    {
        if (filter is TestNodeUidListFilter uidFilter)
        {
            return RunnerTestFilter.FromFullNames(
                uidFilter.TestNodeUids.Select(uid => uid.Value).ToArray());
        }

        return string.IsNullOrWhiteSpace(nameFilter)
            ? RunnerTestFilter.Empty
            : RunnerTestFilter.FromNames(nameFilter!);
    }

    private RunnerTestFilter ResolveRunnerFilter(ITestExecutionFilter? filter) =>
        ToRunnerFilter(filter, ReadOption(DevToolsNUnitCommandLineProvider.FilterOptionName));

    private string? ReadOption(string name)
    {
        if (_commandLine is null)
            return null;

        return _commandLine.TryGetOptionArgumentList(name, out var arguments)
               && arguments is { Length: > 0 }
            ? arguments[0]
            : null;
    }

    private static HostRunOptions ApplyDebugParent(HostRunOptions options) =>
        Debugger.IsAttached
            ? options with { DebugParentPid = Environment.ProcessId }
            : options;

    private DevToolsNUnitSession EnsureSession()
    {
        if (_session is not null)
            return _session;

        _options = HostOptionsLoader.Load();
        if (_injectedTransport is not null)
        {
            _session = new DevToolsNUnitSession(_injectedTransport);
            return _session;
        }

        var runnerPath = ProcessRunnerClient.ResolveRunnerPath(_options);
        var processClient = new ProcessRunnerClient(runnerPath);
        _ownedTransport = new NUnitProcessTransportAdapter(processClient);
        _session = new DevToolsNUnitSession(_ownedTransport);
        return _session;
    }

    internal static TestNode ToDiscoveredNode(NUnitDiscoveredTest test, string? assemblyPath = null)
    {
        var properties = new List<IProperty> { DiscoveredTestNodeStateProperty.CachedInstance };
        AddSource(properties, test.Source);
        AddTraits(properties, test.Traits);
        AddMethodIdentifier(properties, test.FullName, test.Name, assemblyPath);
        return new TestNode
        {
            Uid = new TestNodeUid(StableUid(test.FullName, test.Id, test.Name)),
            DisplayName = test.Name,
            Properties = new PropertyBag(properties),
        };
    }

    internal static TestNode ToResultNode(NUnitCaseResult result, string? assemblyPath = null) =>
        ToResultNode(NUnitTestingMapping.ToTesting(result), assemblyPath, result.FullName, result.Id, result.Name);

    internal static TestNode ToResultNode(
        TestingCaseResult result,
        string? assemblyPath = null,
        string? fullName = null,
        string? protocolId = null,
        string? methodName = null)
    {
        var properties = new List<IProperty>();
        TestingNodeProperties.AddCommonResultProperties(properties, result);
        AddMethodIdentifier(properties, fullName ?? result.TestId, methodName ?? result.DisplayName, assemblyPath);

        return new TestNode
        {
            Uid = new TestNodeUid(StableUid(fullName, protocolId ?? result.TestId, methodName ?? result.DisplayName)),
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

    private static void AddSource(List<IProperty> properties, NUnitSourceLocation? source)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.File))
            return;

        TestingNodeProperties.AddSource(properties, new TestingSourceLocation(source.File, source.Line));
    }

    private static void AddTraits(List<IProperty> properties, IReadOnlyList<NUnitTrait>? traits)
    {
        if (traits is null)
            return;

        TestingNodeProperties.AddTraits(
            properties,
            traits.Select(trait => new TestingTrait(trait.Name, trait.Value)).ToList());
    }

    private static string ResolveTestAssemblyPath()
    {
        var entry = System.Reflection.Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("MTP entry assembly is not available.");
        return entry.Location;
    }
}
