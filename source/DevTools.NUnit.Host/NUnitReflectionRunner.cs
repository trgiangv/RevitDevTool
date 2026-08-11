using System.Diagnostics;
using System.Reflection;
using System.Text;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Host.Logging;
using DevTools.Utilities.AssemblyLoading;
using Microsoft.Extensions.Logging;
using MsLogger = Microsoft.Extensions.Logging.ILogger;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.NUnit.Host;

/// <summary>
/// In-host NUnit runner: stamp-keyed shadow-copy probe + <c>LoadFile</c> + attribute reflection.
/// Does not use <c>NUnit.Engine</c>. Probe loads go through <see cref="DirectoryAssemblyLoad"/>
/// so build output is not locked and rebuilds pick up new IL.
/// </summary>
public sealed class NUnitReflectionRunner
{
    private const string NUnitFrameworkName = "nunit.framework";

    private readonly NUnitAssemblyLoader _assemblyLoader;
    private readonly MsLogger? _logger;
    private readonly object _cancelLock = new();
    private readonly Dictionary<Guid, CancellationFlag> _activeRuns = new();

    public NUnitReflectionRunner(
        NUnitAssemblyLoader assemblyLoader,
        MsLogger? logger = null)
    {
        _assemblyLoader = assemblyLoader;
        _logger = logger;
    }

    public NUnitDiscoverResponse Discover(string assemblyPath, string? filter)
    {
        var fullPath = _assemblyLoader.ResolveAssemblyPath(assemblyPath);
        _assemblyLoader.EnsureLoadable(fullPath);

        return _assemblyLoader.ExecuteWithHostResolve(_ =>
        {
            var assembly = LoadTestAssembly(fullPath);
            var cases = EnumerateCases(assembly, fullPath)
                .Where(test => MatchesFilter(test.FullName, filter))
                .Select(test => (NUnitDiscoveredTest)test)
                .ToList();
            return new NUnitDiscoverResponse(cases);
        }, fullPath);
    }

    public NUnitRunResponse Run(
        Guid runId,
        string assemblyPath,
        string? filter,
        Action<NUnitProgressEvent> publish)
    {
        var fullPath = _assemblyLoader.ResolveAssemblyPath(assemblyPath);
        _assemblyLoader.EnsureLoadable(fullPath);

        var cancel = new CancellationFlag();
        lock (_cancelLock)
            _activeRuns[runId] = cancel;

        try
        {
            return _assemblyLoader.ExecuteWithHostResolve(
                _ => ExecuteRun(runId, fullPath, filter, publish, cancel),
                fullPath);
        }
        finally
        {
            lock (_cancelLock)
                _activeRuns.Remove(runId);
        }
    }

    private NUnitRunResponse ExecuteRun(
        Guid runId,
        string fullPath,
        string? filter,
        Action<NUnitProgressEvent> publish,
        CancellationFlag cancel)
    {
        using var loggingScope = _logger is null ? null : new NUnitRunLoggingScope(_logger);
        var tracker = loggingScope?.Tracker;

        var cases = EnumerateCases(LoadTestAssembly(fullPath), fullPath)
            .Where(test => MatchesFilter(test.FullName, filter))
            .ToList();

        var results = new List<NUnitCaseResult>(cases.Count);
        foreach (var group in cases.GroupBy(c => c.FixtureType))
            results.AddRange(RunFixtureGroup(runId, group, publish, cancel, tracker));

        return new NUnitRunResponse(runId, BuildSummary(results), results);
    }

    private List<NUnitCaseResult> RunFixtureGroup(
        Guid runId,
        IGrouping<Type, DiscoveredCase> group,
        Action<NUnitProgressEvent> publish,
        CancellationFlag cancel,
        NUnitRunOutputTracker? tracker)
    {
        if (cancel.IsRequested)
            return group.Select(test => PublishCancelled(runId, test, publish, tracker)).ToList();

        if (!TryCreateFixture(group.Key, out var fixture, out var setupError))
        {
            return group.Select(test => PublishResult(
                runId,
                test,
                NUnitOutcomes.Error,
                0,
                setupError!.Message,
                setupError.StackTrace,
                null,
                publish)).ToList();
        }

        try
        {
            return RunFixtureCases(runId, fixture, group, publish, cancel, tracker);
        }
        finally
        {
            DisposeFixture(fixture, group.Key);
        }
    }

    private bool TryCreateFixture(Type fixtureType, out object? fixture, out Exception? error)
    {
        fixture = null;
        error = null;
        try
        {
            fixture = Activator.CreateInstance(fixtureType);
            InvokeLifecycle(fixture, fixtureType, NUnitAttributeNames.OneTimeSetUp);
            return true;
        }
        catch (Exception ex)
        {
            error = Unwrap(ex);
            return false;
        }
    }

    private List<NUnitCaseResult> RunFixtureCases(
        Guid runId,
        object? fixture,
        IEnumerable<DiscoveredCase> cases,
        Action<NUnitProgressEvent> publish,
        CancellationFlag cancel,
        NUnitRunOutputTracker? tracker)
    {
        var results = new List<NUnitCaseResult>();
        foreach (var test in cases)
        {
            results.Add(cancel.IsRequested
                ? PublishCancelled(runId, test, publish, tracker)
                : RunSingle(runId, fixture, test, publish, tracker));
        }

        return results;
    }

    private void DisposeFixture(object? fixture, Type fixtureType)
    {
        try
        {
            InvokeLifecycle(fixture, fixtureType, NUnitAttributeNames.OneTimeTearDown);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "OneTimeTearDown failed for {Fixture}", fixtureType.FullName);
        }

        if (fixture is IDisposable disposable)
            disposable.Dispose();
    }

    public void Cancel(Guid runId)
    {
        lock (_cancelLock)
        {
            if (_activeRuns.TryGetValue(runId, out var flag))
                flag.Request();
        }
    }

    private Assembly LoadTestAssembly(string fullPath)
    {
        ValidateNUnitFrameworkPin(fullPath);

        try
        {
            return DirectoryAssemblyLoad.LoadPath(fullPath);
        }
        catch (Exception ex)
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                fullPath,
                $"Failed to load test assembly: {ex.Message}",
                ex.ToString()));
        }
    }

    private static void ValidateNUnitFrameworkPin(string testAssemblyPath)
    {
        var directory = Path.GetDirectoryName(testAssemblyPath)
            ?? throw new InvalidOperationException("Test assembly directory is unavailable.");
        var frameworkPath = Path.Combine(directory, NUnitFrameworkName + ".dll");
        if (!File.Exists(frameworkPath))
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                testAssemblyPath,
                $"Missing {NUnitFrameworkName}.dll beside the test assembly. Copy the NUnit framework next to the test output."));
        }

        try
        {
            _ = AssemblyName.GetAssemblyName(frameworkPath);
        }
        catch (Exception ex)
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                testAssemblyPath,
                $"Failed to read {NUnitFrameworkName}.dll: {ex.Message}",
                ex.ToString()));
        }

        // Eager shadow LoadFile so resolve prefers the test-dir framework before the test binds,
        // without locking the build-output framework DLL.
        try
        {
            _ = DirectoryAssemblyLoad.LoadPath(frameworkPath);
        }
        catch (Exception ex)
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                testAssemblyPath,
                $"Failed to load {NUnitFrameworkName}.dll from the test directory: {ex.Message}",
                ex.ToString()));
        }
    }

    private static void EnsureReferencedFrameworkMatchesDisk(Assembly testAssembly, string testAssemblyPath)
    {
        var directory = Path.GetDirectoryName(testAssemblyPath)!;
        var frameworkPath = Path.Combine(directory, NUnitFrameworkName + ".dll");
        var diskVersion = AssemblyName.GetAssemblyName(frameworkPath).Version!;
        var reference = testAssembly.GetReferencedAssemblies()
            .FirstOrDefault(name => string.Equals(name.Name, NUnitFrameworkName, StringComparison.OrdinalIgnoreCase));
        if (reference?.Version is null)
            return;

        if (reference.Version.Major != diskVersion.Major || reference.Version.Minor != diskVersion.Minor)
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                testAssemblyPath,
                $"'{NUnitFrameworkName}' reference {reference.Version.ToString(3)} does not match " +
                $"test-directory {diskVersion.ToString(3)}. Align the test project's NUnit package with the copied DLL."));
        }
    }

    private IReadOnlyList<DiscoveredCase> EnumerateCases(Assembly assembly, string testAssemblyPath)
    {
        // Shadow LoadFile Location is under temp — pin against the known source path.
        EnsureReferencedFrameworkMatchesDisk(assembly, testAssemblyPath);

        var cases = new List<DiscoveredCase>();
        foreach (var type in SafeGetTypes(assembly))
        {
            if (!TryGetTestMethods(type, out var methods))
                continue;

            foreach (var method in methods)
                cases.AddRange(EnumerateMethodCases(type, method));
        }

        return cases;
    }

    private static bool TryGetTestMethods(Type type, out List<MethodInfo> methods)
    {
        methods = new List<MethodInfo>();
        if (!type.IsClass || type.IsAbstract)
            return false;

        methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(NUnitAttributeNames.IsTestMethod)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

        return methods.Count > 0;
    }

    private static IEnumerable<DiscoveredCase> EnumerateMethodCases(Type type, MethodInfo method)
    {
        if (method.GetParameters().Length > 0
            && !NUnitAttributeNames.HasAttribute(method, NUnitAttributeNames.TestCase))
        {
            // Parameterized without TestCase is out of v1 scope.
            yield break;
        }

        var testCases = NUnitAttributeNames.GetTestCaseArguments(method).ToList();
        if (testCases.Count == 0)
            testCases.Add(Array.Empty<object?>());

        var index = 0;
        foreach (var args in testCases)
        {
            var name = args.Length == 0
                ? method.Name
                : $"{method.Name}({string.Join(", ", args.Select(FormatArg))})";
            var fullName = $"{type.FullName}.{name}";
            yield return new DiscoveredCase($"{fullName}#{index++}", name, fullName, type, method, args);
        }
    }

    private NUnitCaseResult RunSingle(
        Guid runId,
        object? fixture,
        DiscoveredCase test,
        Action<NUnitProgressEvent> publish,
        NUnitRunOutputTracker? tracker)
    {
        tracker?.BeginTest(test.Id, test.Name);
        var sw = Stopwatch.StartNew();
        string outcome;
        string? message = null;
        string? stack = null;
        string? captured = null;

        // When a logging scope is active, Console is already routed to Trace → tracker.
        // Only capture Console locally when there is no tracker (avoid stealing SetOut).
        TextWriter? originalOut = null;
        TextWriter? originalErr = null;
        StringWriter? writer = null;
        var captureConsole = tracker is null;
        if (captureConsole)
        {
            originalOut = Console.Out;
            originalErr = Console.Error;
            writer = new StringWriter();
            Console.SetOut(writer);
            Console.SetError(writer);
        }

        try
        {
            InvokeLifecycle(fixture, test.FixtureType, NUnitAttributeNames.SetUp);
            try
            {
                test.Method.Invoke(fixture, test.Arguments.Length == 0 ? null : test.Arguments);
                outcome = NUnitOutcomes.Passed;
            }
            finally
            {
                InvokeLifecycle(fixture, test.FixtureType, NUnitAttributeNames.TearDown);
            }
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            (outcome, message, stack) = MapException(tie.InnerException);
        }
        catch (Exception ex)
        {
            (outcome, message, stack) = MapException(ex);
        }
        finally
        {
            if (captureConsole)
            {
                Console.SetOut(originalOut!);
                Console.SetError(originalErr!);
                captured = writer!.ToString();
                writer.Dispose();
            }

            sw.Stop();
        }

        var traceOutput = tracker?.Complete(test.Id);
        var merged = NUnitOutputMerger.Merge(
            string.IsNullOrWhiteSpace(captured) ? null : captured,
            traceOutput);

        return PublishResult(runId, test, outcome, sw.Elapsed.TotalMilliseconds, message, stack, merged, publish);
    }

    private static (string Outcome, string? Message, string? Stack) MapException(Exception ex)
    {
        var typeName = ex.GetType().FullName ?? ex.GetType().Name;
        if (typeName.EndsWith(".SuccessException", StringComparison.Ordinal)
            || string.Equals(ex.GetType().Name, "SuccessException", StringComparison.Ordinal))
            return (NUnitOutcomes.Passed, null, null);

        if (typeName.EndsWith(".IgnoreException", StringComparison.Ordinal)
            || typeName.EndsWith(".SkipException", StringComparison.Ordinal))
            return (NUnitOutcomes.Skipped, ex.Message, ex.StackTrace);

        if (typeName.EndsWith(".InconclusiveException", StringComparison.Ordinal))
            return (NUnitOutcomes.Inconclusive, ex.Message, ex.StackTrace);

        if (typeName.EndsWith(".AssertionException", StringComparison.Ordinal)
            || typeName.EndsWith(".MultipleAssertException", StringComparison.Ordinal))
            return (NUnitOutcomes.Failed, ex.Message, ex.StackTrace);

        return (NUnitOutcomes.Error, ex.Message, ex.StackTrace);
    }

    private static void InvokeLifecycle(object? instance, Type type, string attributeName)
    {
        if (instance is null)
            return;

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!NUnitAttributeNames.HasAttribute(method, attributeName))
                continue;
            if (method.GetParameters().Length != 0)
                continue;

            method.Invoke(instance, null);
        }
    }

    private NUnitCaseResult PublishCancelled(
        Guid runId,
        DiscoveredCase test,
        Action<NUnitProgressEvent> publish,
        NUnitRunOutputTracker? tracker) =>
        PublishResult(runId, test, NUnitOutcomes.Cancelled, 0, "Cancelled", null, tracker?.Complete(test.Id), publish);

    private NUnitCaseResult PublishResult(
        Guid runId,
        DiscoveredCase test,
        string outcome,
        double durationMs,
        string? message,
        string? stackTrace,
        string? output,
        Action<NUnitProgressEvent> publish)
    {
        var result = new NUnitCaseResult(
            test.Id,
            test.Name,
            outcome,
            durationMs,
            message,
            stackTrace,
            output);

        if (!string.IsNullOrWhiteSpace(output))
            _logger?.LogInformation("[NUnit:{TestName}] {Output}", test.Name, output!.TrimEnd());

        publish(new NUnitProgressEvent(runId, result));
        return result;
    }

    private static NUnitRunSummary BuildSummary(IReadOnlyList<NUnitCaseResult> cases)
    {
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var inconclusive = 0;
        var errors = 0;
        var cancelled = 0;

        foreach (var testCase in cases)
        {
            switch (testCase.Outcome)
            {
                case NUnitOutcomes.Passed: passed++; break;
                case NUnitOutcomes.Failed: failed++; break;
                case NUnitOutcomes.Skipped: skipped++; break;
                case NUnitOutcomes.Inconclusive: inconclusive++; break;
                case NUnitOutcomes.Cancelled: cancelled++; break;
                default: errors++; break;
            }
        }

        return new NUnitRunSummary(passed, failed, skipped, inconclusive, errors, cancelled);
    }

    private static bool MatchesFilter(string fullName, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        // Adapter BuildFilter emits: test == 'A' | test == 'B'
        foreach (var raw in filter!.Split('|'))
        {
            var clause = raw.Trim();
            if (clause.Length == 0)
                continue;

            if (MatchesFilterClause(fullName, clause))
                return true;
        }

        return false;
    }

    private static bool MatchesFilterClause(string fullName, string clause)
    {
        var needle = clause.Trim();
        if (needle.Length == 0)
            return false;

        const string testEq = "test ==";
        const string testEqTight = "test==";
        const string namePrefix = "name=~";

        if (needle.StartsWith(testEq, StringComparison.OrdinalIgnoreCase)
            || needle.StartsWith(testEqTight, StringComparison.OrdinalIgnoreCase))
        {
            var literal = ExtractQuotedLiteral(needle);
            if (literal is null)
                return false;

            return string.Equals(fullName, literal, StringComparison.OrdinalIgnoreCase)
                   || fullName.StartsWith(literal + "(", StringComparison.OrdinalIgnoreCase);
        }

        if (needle.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
        {
            needle = needle.Substring(namePrefix.Length).Trim().Trim('\'', '"');
        }

        return fullName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string? ExtractQuotedLiteral(string clause)
    {
        var start = clause.IndexOf('\'');
        var end = clause.LastIndexOf('\'');
        if (start < 0 || end <= start)
        {
            start = clause.IndexOf('"');
            end = clause.LastIndexOf('"');
            if (start < 0 || end <= start)
                return null;
        }

        return clause.Substring(start + 1, end - start - 1).Replace("''", "'");
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static string FormatArg(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        _ => Convert.ToString(value) ?? "?",
    };

    private static Exception Unwrap(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    private sealed class CancellationFlag
    {
        private int _requested;
        public bool IsRequested => Volatile.Read(ref _requested) != 0;
        public void Request() => Interlocked.Exchange(ref _requested, 1);
    }

    private sealed record DiscoveredCase(
        string Id,
        string Name,
        string FullName,
        Type FixtureType,
        MethodInfo Method,
        object?[] Arguments)
    {
        public static implicit operator NUnitDiscoveredTest(DiscoveredCase c) =>
            new(c.Id, c.Name, c.FullName);
    }
}

internal static class NUnitAttributeNames
{
    public const string Test = "TestAttribute";
    public const string TestCase = "TestCaseAttribute";
    public const string TestFixture = "TestFixtureAttribute";
    public const string SetUp = "SetUpAttribute";
    public const string TearDown = "TearDownAttribute";
    public const string OneTimeSetUp = "OneTimeSetUpAttribute";
    public const string OneTimeTearDown = "OneTimeTearDownAttribute";

    public static bool IsTestMethod(MethodInfo method) =>
        HasAttribute(method, Test) || HasAttribute(method, TestCase);

    public static bool HasAttribute(MemberInfo member, string attributeTypeName) =>
        member.GetCustomAttributes(inherit: true)
            .Any(attribute => string.Equals(attribute.GetType().Name, attributeTypeName, StringComparison.Ordinal));

    public static IEnumerable<object?[]> GetTestCaseArguments(MethodInfo method)
    {
        foreach (var attribute in method.GetCustomAttributes(inherit: true))
        {
            if (!string.Equals(attribute.GetType().Name, TestCase, StringComparison.Ordinal))
                continue;

            var argumentsProperty = attribute.GetType().GetProperty("Arguments");
            if (argumentsProperty?.GetValue(attribute) is object?[] args)
                yield return args;
            else
                yield return Array.Empty<object?>();
        }
    }
}
