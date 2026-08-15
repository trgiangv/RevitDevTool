using System.Diagnostics;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Mtp;

internal sealed class DevToolsNUnitFramework : ITestFramework, IDataProducer
{
    private readonly IRunnerTransport? _injectedTransport;
    private readonly ICommandLineOptions? _commandLine;
    private DevToolsNUnitSession? _session;
    private ProcessRunnerClient? _ownedClient;
    private HostRunOptions? _options;

    internal DevToolsNUnitFramework(
        IServiceProvider serviceProvider,
        IRunnerTransport? transport = null)
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
        _ownedClient?.Dispose();
        _session = null;
        _ownedClient = null;
        return Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }

    private async Task PublishDiscoveredAsync(
        string assemblyPath,
        DiscoverTestExecutionRequest request,
        ExecuteRequestContext context)
    {
        var filter = ResolveRunnerFilter(request.Filter);
        var cases = NUnitMetadataDiscoverer.Filter(
            NUnitMetadataDiscoverer.Discover(assemblyPath),
            filter.Names,
            filter.FullNames);
        foreach (var discovered in cases)
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        request.Session.SessionUid,
                        ToDiscoveredNode(discovered, assemblyPath)))
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
        IReadOnlyList<NUnitCaseResult> results;
        try
        {
            results = session.Run(assemblyPath, options, filter);
        }
        catch (Exception ex)
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        request.Session.SessionUid,
                        new TestNode
                        {
                            Uid = new TestNodeUid("devtools.nunit.runner"),
                            DisplayName = "DevTools.NUnit",
                            Properties = new PropertyBag(new ErrorTestNodeStateProperty(ex)),
                        }))
                .ConfigureAwait(false);
            return;
        }

        foreach (var result in results)
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

        _ownedClient = new ProcessRunnerClient(ProcessRunnerClient.ResolveRunnerPath(_options));
        _session = new DevToolsNUnitSession(_ownedClient);
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

    internal static TestNode ToResultNode(NUnitCaseResult result, string? assemblyPath = null)
    {
        var properties = new List<IProperty> { ToStateProperty(result) };
        AddSource(properties, result.Source);
        AddTraits(properties, result.Traits);
        AddMethodIdentifier(properties, result.FullName, result.Name, assemblyPath);

        var duration = TimeSpan.FromMilliseconds(result.DurationMs);
        var end = DateTimeOffset.UtcNow;
        properties.Add(new TimingProperty(new TimingInfo(end - duration, end, duration)));

        if (!string.IsNullOrWhiteSpace(result.Output))
            properties.Add(new StandardOutputProperty(result.Output!));

        return new TestNode
        {
            Uid = new TestNodeUid(StableUid(result.FullName, result.Id, result.Name)),
            DisplayName = result.Name,
            Properties = new PropertyBag(properties),
        };
    }

    private static IProperty ToStateProperty(NUnitCaseResult result) =>
        result.Outcome switch
        {
            "Passed" => PassedTestNodeStateProperty.CachedInstance,
            "Skipped" => new SkippedTestNodeStateProperty(result.SkipReason ?? result.Message),
            "Failed" => new FailedTestNodeStateProperty(CreateException(result)),
            _ => new ErrorTestNodeStateProperty(CreateException(result)),
        };

    private static Exception CreateException(NUnitCaseResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StackTrace))
            return new InvalidOperationException(result.Message ?? result.Outcome);

        return new InvalidOperationException($"{result.Message}{Environment.NewLine}{result.StackTrace}");
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

        var line = Math.Max(source.Line, 1);
        properties.Add(new TestFileLocationProperty(
            source.File,
            new LinePositionSpan(new LinePosition(line, 1), new LinePosition(line, 1))));
    }

    private static void AddTraits(List<IProperty> properties, IReadOnlyList<NUnitTrait>? traits)
    {
        if (traits is null)
            return;

        foreach (var trait in traits)
            properties.Add(new TestMetadataProperty(trait.Name, trait.Value));
    }

    private static string ResolveTestAssemblyPath()
    {
        var entry = System.Reflection.Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("MTP entry assembly is not available.");
        return entry.Location;
    }
}
