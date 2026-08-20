using System.Diagnostics;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.NUnit.Runtime;
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
        _options ??= HostOptionsLoader.Load(RequireConfiguration());
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
        HostTestSession session,
        string assemblyPath,
        RunTestExecutionRequest request,
        ExecuteRequestContext context)
    {
        var options = ApplyDebugParent(
            _options ?? throw new InvalidOperationException("Host run options were not loaded."));
        var filter = ResolveRunnerFilter(request.Filter);
        var cases = SelectCases(assemblyPath, filter);
        var hostSelection = ToHostSelection(filter, cases);
        var testCount = Math.Max(cases.Count, filter.TestIds?.Count ?? 0);
        if (testCount == 0 && IsConstrained(filter))
        {
            foreach (var missing in ResultsForUnreportedIds(filter, cases, []))
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

        var published = FoldHostResults(filter, cases, response.Results);
        foreach (var result in published)
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        request.Session.SessionUid,
                        ToResultNode(result, assemblyPath, cases)))
                .ConfigureAwait(false);
        }

        foreach (var missing in ResultsForUnreportedIds(filter, cases, published))
        {
            await context.MessageBus.PublishAsync(
                    this,
                    new TestNodeUpdateMessage(
                        request.Session.SessionUid,
                        ToResultNode(missing, assemblyPath, cases)))
                .ConfigureAwait(false);
        }
    }

    internal static int CountRunTests(
        string assemblyPath,
        TestingSelection selection) =>
        SelectCases(assemblyPath, selection).Count;

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

    internal static bool IsConstrained(TestingSelection selection) =>
        selection.TestIds is { Count: > 0 } || selection.Names is { Count: > 0 };

    /// <summary>
    /// UID list → host NUnit filter XML. Testhost stub / Test Explorer
    /// identifier <c>Class.Method</c> also matches in-host
    /// <c>Class("args").Method</c> and <c>TestName</c> / <c>SetName</c>
    /// children even when local Select missed. CLI <c>--filter</c> /
    /// <c>Name=</c> stays <c>Names</c> → <c>&lt;name&gt;</c>. IDs with
    /// depth-0 <c>(</c> stay exact <c>&lt;test&gt;</c>.
    /// </summary>
    internal static TestingSelection ToHostSelection(
        TestingSelection selection,
        IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        if (!IsConstrained(selection))
            return selection;

        if (selection.TestIds is not { Count: > 0 } && selection.Names is { Count: > 0 })
            return selection;

        var ids = (selection.TestIds ?? [])
            .Select(id => ToNunitFullName(id, discovered))
            .Concat(discovered.Select(test => test.FullName ?? test.TestId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0)
            return selection;

        return new TestingSelection([], NUnitCollapsedSelection.ToFilterXml(ids));
    }

    private static string ToNunitFullName(string id, IReadOnlyList<TestingDiscoveredTest> discovered)
    {
        foreach (var test in discovered)
        {
            if (string.Equals(test.TestId, id, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(test.FullName))
                return test.FullName!;
        }

        return id;
    }

    internal static TestingSelection ToHostSelection(
        string assemblyPath,
        TestingSelection selection)
    {
        var cases = SelectCases(assemblyPath, selection);
        return ToHostSelection(selection, cases);
    }

    internal static IReadOnlyList<TestingDiscoveredTest> SelectCases(
        string assemblyPath,
        TestingSelection selection)
    {
        var provider = HostTestDiscovery.Provider
            ?? throw new InvalidOperationException(
                $"Local discovery requires {TestingPlatformBuilderHook.NUnitMTPAssemblyFileName} next to the test executable. "
                + "RevitDevTool.TestAdapter copies it at build. Reference NUnit in the test project; do not add DevTools.NUnit.MTP as a ProjectReference.");

        return provider.Select(assemblyPath, selection);
    }

    internal const string UnreportedFullNameMessage =
        "Host NUnit did not report this FullName. UID is ITest.FullName from testhost ExploreTests; in-host source expansion uses a different FullName.";

    /// <summary>
    /// Identity-preserving: every requested UID gets a result node.
    /// NUnit3 maps missing/NotRunnable to Failed. Do not drop UIDs (VS yellow bar).
    /// </summary>
    internal static IReadOnlyList<TestingCaseResult> ResultsForUnreportedIds(
        TestingSelection request,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        if (request.TestIds is not { Count: > 0 })
            return [];

        var reported = new HashSet<string>(
            hostResults.Select(result => result.TestId),
            StringComparer.Ordinal);
        var display = discovered
            .Where(test => !string.IsNullOrWhiteSpace(test.TestId))
            .GroupBy(test => test.TestId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);

        var missing = new List<TestingCaseResult>();
        foreach (var id in request.TestIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal))
        {
            if (reported.Contains(id))
                continue;

            var name = display.TryGetValue(id, out var displayName) ? displayName : id;
            missing.Add(new TestingCaseResult(
                id,
                name,
                TestingOutcomes.Failed,
                0,
                UnreportedFullNameMessage,
                null,
                null,
                null,
                [],
                []));
        }

        return missing;
    }

    /// <summary>
    /// UID runs publish onto the requested identity. Multiple in-host
    /// expansions (fixture source / SetName) fold into that one UID.
    /// Discovered TestName/SetName leaves also keep their own result
    /// nodes so Test Explorer children update. Names-only CLI runs keep
    /// per-leaf host identities.
    /// </summary>
    internal static IReadOnlyList<TestingCaseResult> FoldHostResults(
        TestingSelection request,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults)
    {
        if (request.TestIds is not { Count: > 0 })
            return hostResults;

        var display = discovered
            .Where(test => !string.IsNullOrWhiteSpace(test.TestId))
            .GroupBy(test => test.TestId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);

        var folded = new List<TestingCaseResult>();
        foreach (var id in request.TestIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal))
        {
            var nunitId = ToNunitFullName(id, discovered);
            var matches = hostResults
                .Where(result => NUnitCollapsedSelection.Matches(
                        id,
                        result.TestId,
                        result.FullName,
                        result.ParentTestId)
                    || NUnitCollapsedSelection.Matches(
                        nunitId,
                        result.TestId,
                        result.FullName,
                        result.ParentTestId))
                .ToList();
            if (matches.Count == 0)
                continue;

            folded.Add(
                matches.Count == 1 && string.Equals(matches[0].TestId, id, StringComparison.Ordinal)
                    ? matches[0]
                    : Collapse(id, display.TryGetValue(id, out var name) ? name : id, matches));
        }

        var published = new HashSet<string>(
            folded.Select(result => result.TestId),
            StringComparer.Ordinal);
        foreach (var test in discovered)
        {
            if (string.IsNullOrWhiteSpace(test.TestId))
                continue;

            var id = test.TestId.Trim();
            if (published.Contains(id))
                continue;

            var match = hostResults.FirstOrDefault(result =>
                string.Equals(result.TestId, id, StringComparison.Ordinal)
                || string.Equals(result.FullName, id, StringComparison.Ordinal)
                || string.Equals(result.TestId, test.FullName, StringComparison.Ordinal)
                || string.Equals(result.FullName, test.FullName, StringComparison.Ordinal));
            if (match is null)
                continue;

            folded.Add(
                string.Equals(match.TestId, id, StringComparison.Ordinal)
                    ? match
                    : Collapse(id, test.DisplayName, [match]));
            published.Add(id);
        }

        return folded;
    }

    private static TestingCaseResult Collapse(
        string testId,
        string displayName,
        IReadOnlyList<TestingCaseResult> matches)
    {
        var outcome = WorstOutcome(matches);
        var duration = matches.Sum(result => result.DurationMilliseconds);
        var messages = matches
            .Select(result => result.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        var stacks = matches
            .Select(result => result.StackTrace)
            .Where(stack => !string.IsNullOrWhiteSpace(stack))
            .ToList();
        var outputs = matches
            .Select(result => result.Output)
            .Where(output => !string.IsNullOrWhiteSpace(output))
            .ToList();

        return new TestingCaseResult(
            testId,
            displayName,
            outcome,
            duration,
            messages.Count == 0 ? null : string.Join(Environment.NewLine, messages),
            stacks.Count == 0 ? null : string.Join(Environment.NewLine, stacks),
            outputs.Count == 0 ? null : string.Join(Environment.NewLine, outputs),
            matches.Select(result => result.Source).FirstOrDefault(source => source is not null),
            matches.SelectMany(result => result.Traits).ToList(),
            matches.SelectMany(result => result.Attachments).ToList(),
            FullName: testId);
    }

    private static string WorstOutcome(IReadOnlyList<TestingCaseResult> matches)
    {
        foreach (var outcome in new[]
        {
            TestingOutcomes.Error,
            TestingOutcomes.Failed,
            TestingOutcomes.Cancelled,
            TestingOutcomes.Inconclusive,
            TestingOutcomes.Skipped,
        })
        {
            if (matches.Any(result => string.Equals(result.Outcome, outcome, StringComparison.Ordinal)))
                return outcome;
        }

        return TestingOutcomes.Passed;
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
        AddMethodIdentifier(properties, test.FullName, test.DisplayName, assemblyPath, test.ClassName, test.MethodName);
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
        TryGetDiscoveredIdentity(discovered, result, out var className, out var methodName);
        AddMethodIdentifier(
            properties,
            result.FullName ?? result.TestId,
            result.DisplayName,
            assemblyPath,
            className,
            methodName);

        return new TestNode
        {
            Uid = new TestNodeUid(OpaqueUid(result.TestId, result.FullName, result.DisplayName)),
            DisplayName = result.DisplayName,
            Properties = new PropertyBag(properties),
        };
    }

    private static void TryGetDiscoveredIdentity(
        IReadOnlyList<TestingDiscoveredTest>? discovered,
        TestingCaseResult result,
        out string? className,
        out string? methodName)
    {
        className = null;
        methodName = null;
        if (discovered is null || discovered.Count == 0)
            return;

        foreach (var test in discovered)
        {
            if (string.Equals(test.TestId, result.TestId, StringComparison.Ordinal)
                || string.Equals(test.FullName, result.TestId, StringComparison.Ordinal)
                || string.Equals(test.TestId, result.FullName, StringComparison.Ordinal)
                || string.Equals(test.FullName, result.FullName, StringComparison.Ordinal))
            {
                className = test.ClassName;
                methodName = test.MethodName;
                return;
            }
        }
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
        string? fullName,
        string methodName,
        string? assemblyPath,
        string? className = null,
        string? discoveredMethodName = null)
    {
        if (!TrySplitIdentity(fullName, methodName, className, discoveredMethodName, out var ns, out var typeName, out var parsedMethod))
            return;

        properties.Add(new TestMethodIdentifierProperty(
            ResolveAssemblyFullName(assemblyPath),
            ns,
            typeName,
            parsedMethod,
            methodArity: 0,
            [],
            "System.Void"));
    }

    /// <summary>
    /// Same grouping as NUnit3 MTP <c>TestMethodIdentifierBuilder</c>:
    /// last <c>.</c> not inside parentheses so fixture args stay on the type
    /// (<c>Tests(One).Test1</c> → type <c>Tests(One)</c>).
    /// </summary>
    internal static bool TrySplitIdentity(
        string? fullName,
        string displayName,
        string? className,
        string? methodName,
        out string ns,
        out string typeName,
        out string parsedMethod)
    {
        ns = string.Empty;
        typeName = string.Empty;
        parsedMethod = methodName ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(className))
        {
            SplitNamespaceAndType(className!, out ns, out typeName);
            if (string.IsNullOrWhiteSpace(parsedMethod))
                parsedMethod = StripTrailingArguments(displayName);
            return !string.IsNullOrWhiteSpace(typeName) && !string.IsNullOrWhiteSpace(parsedMethod);
        }

        var identity = fullName;
        if (string.IsNullOrWhiteSpace(identity))
            identity = displayName;
        if (string.IsNullOrWhiteSpace(identity))
            return false;

        var lastDot = LastDotAtDepthZero(identity!);
        if (lastDot < 0)
        {
            parsedMethod = StripTrailingArguments(identity!);
            typeName = parsedMethod;
            return !string.IsNullOrWhiteSpace(parsedMethod);
        }

        SplitNamespaceAndType(identity!.Substring(0, lastDot), out ns, out typeName);
        parsedMethod = StripTrailingArguments(identity.Substring(lastDot + 1));
        if (string.IsNullOrWhiteSpace(typeName))
            typeName = parsedMethod;
        return !string.IsNullOrWhiteSpace(parsedMethod);
    }

    private static void SplitNamespaceAndType(string className, out string ns, out string typeName)
    {
        var lastDot = LastDotAtDepthZero(className);
        if (lastDot < 0)
        {
            ns = string.Empty;
            typeName = className;
            return;
        }

        ns = className.Substring(0, lastDot);
        typeName = className.Substring(lastDot + 1);
    }

    private static int LastDotAtDepthZero(string value)
    {
        var depth = 0;
        var last = -1;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                case '<':
                    depth++;
                    break;
                case ')':
                case '>':
                    if (depth > 0)
                        depth--;
                    break;
                case '.' when depth == 0:
                    last = index;
                    break;
            }
        }

        return last;
    }

    private static string StripTrailingArguments(string value)
    {
        var open = -1;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '(')
            {
                if (depth == 0)
                    open = index;
                depth++;
            }
            else if (value[index] == ')' && depth > 0)
            {
                depth--;
            }
        }

        return open >= 0 && depth == 0 ? value.Substring(0, open) : value;
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
