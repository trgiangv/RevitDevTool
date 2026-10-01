using System.Runtime.CompilerServices;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using ReflectionAssembly = System.Reflection.Assembly;
using SessionUid = Microsoft.Testing.Platform.TestHost.SessionUid;

namespace DevTools.TUnit.Runtime;

#pragma warning disable TPEXP

internal static class TUnitEngineHost
{
    public static IReadOnlyList<TestCaseResult> Run(
        ReflectionAssembly testAssembly,
        TestSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        if (selection.Kind == TestSelectionKind.TestIds
            && selection.TestIds.All(string.IsNullOrWhiteSpace))
            return [];

        SourceRegistrar.IsEnabled = true;
        RuntimeHelpers.RunModuleConstructor(testAssembly.ManifestModule.ModuleHandle);
        TUnitSourceCatalog.Retain(testAssembly);
        var captured = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return RunEngine(selection, cancellationToken);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(captured);
        }
    }

    private static IReadOnlyList<TestCaseResult> RunEngine(
        TestSelection selection,
        CancellationToken cancellationToken)
    {
        var bindings = TUnitEngineBindings.Instance;
        var resultDirectory = Path.Combine(Path.GetTempPath(), "DevTools.tunit");
        Directory.CreateDirectory(resultDirectory);

        var services = bindings.CreateServices(Directory.GetCurrentDirectory(), resultDirectory);
        var framework = bindings.CreateFramework(services);

        var sessionUid = new SessionUid(Guid.NewGuid().ToString("N"));
        var request = new RunTestExecutionRequest(
            bindings.CreateSessionContext(sessionUid),
            CreateFilter(selection));
        using var traceScope = new TestRunTraceScope();
        var messageBus = new TUnitEngineMessageBus(traceScope);
        var executeContext = new ExecuteRequestContext(
            request,
            messageBus,
            new TUnitEngineCompletionNotifier(),
            cancellationToken);
        var createContext = bindings.CreateCreateContext(sessionUid, cancellationToken);
        var closeContext = bindings.CreateCloseContext(sessionUid, cancellationToken);

        framework.CreateTestSessionAsync(createContext).GetAwaiter().GetResult();
        try
        {
            framework.ExecuteRequestAsync(executeContext).GetAwaiter().GetResult();
        }
        finally
        {
            framework.CloseTestSessionAsync(closeContext).GetAwaiter().GetResult();
        }

        return TUnitEngineResults.Map(messageBus.Nodes.Values, messageBus.CapturedByUid);
    }
    private static ITestExecutionFilter CreateFilter(TestSelection selection)
    {
        if (selection.Kind == TestSelectionKind.All)
            return new NopFilter();

        if (selection.Kind != TestSelectionKind.TestIds)
        {
            throw new ArgumentException(
                "TUnit runtime expects All or TestIds. Map Names/FrameworkFilter before testing/run.",
                nameof(selection));
        }

        var ids = selection.TestIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(id => new TestNodeUid(id))
            .ToArray();
        return new TestNodeUidListFilter(ids);
    }
}
