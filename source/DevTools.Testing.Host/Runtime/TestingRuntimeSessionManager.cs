using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Runtime;

public interface ITestingRuntimeSessionFactory
{
    ITestingRuntimeSession Create(TestingGenerationManifest generation);
}

public sealed record TestingGenerationRetirementDiagnostic(
    string GenerationId,
    string Code,
    string Message);

public interface ITestingRuntimeSessionRetirementDiagnostics
{
    TestingGenerationRetirementDiagnostic? GetRetirementDiagnostic();
}

public sealed class NullTestingRuntimeEventSink : ITestingRuntimeEventSink
{
    public static NullTestingRuntimeEventSink Instance { get; } = new();
    private NullTestingRuntimeEventSink() { }
    public void Publish(TestingRuntimeEvent testingEvent) { }
}

public sealed class TestingRuntimeSessionManager : IDisposable
{
    private readonly TestingGenerationStore _generations;
    private readonly ITestingGenerationPolicy _policy;
    private readonly ITestingRuntimeSessionFactory _factory;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly Dictionary<string, ManagedSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ManagedSession> _activeRuns = new();
    private readonly List<TestingGenerationRetirementDiagnostic> _retainedDiagnostics = [];
    private TestingGenerationRetirementDiagnostic? _pendingRetirementDiagnostic;
    private bool _disposed;

    internal Action? AfterDisposedCheckBeforeRegistration { get; set; }

    public TestingRuntimeSessionManager(
        TestingGenerationStore generations,
        ITestingGenerationPolicy policy,
        ITestingRuntimeSessionFactory factory)
    {
        _generations = generations ?? throw new ArgumentNullException(nameof(generations));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public string? CurrentGenerationId { get; private set; }
    public bool IsOperationActive => _operationLock.CurrentCount == 0;
    public int RetainedGenerationCount { get { lock (_stateLock) return _retainedDiagnostics.Count; } }
    public IReadOnlyList<TestingGenerationRetirementDiagnostic> RetainedGenerationDiagnostics
    {
        get { lock (_stateLock) return _retainedDiagnostics.ToList(); }
    }

    public TestingRunResponse Run(TestingRunRequest request, ITestingRuntimeEventSink eventSink, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (eventSink is null)
            throw new ArgumentNullException(nameof(eventSink));
        _operationLock.Wait(cancellationToken);
        try
        {
            var session = AcquireAndRegister(request.Assembly.Path, request.RunId);
            TestingRunResponse response;
            try
            {
                response = session.Session.Run(request, eventSink, cancellationToken);
            }
            finally
            {
                lock (_stateLock) _activeRuns.Remove(request.RunId);
                RetireObsolete();
            }

            return EnrichWithRetirementDiagnostic(response);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Cancel(Guid runId)
    {
        ManagedSession? session;
        lock (_stateLock) _activeRuns.TryGetValue(runId, out session);
        session?.Session.Cancel(runId);
    }

    private ManagedSession AcquireAndRegister(string assemblyPath, Guid runId)
    {
        var manifest = _generations.Build(_policy, assemblyPath);
        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TestingRuntimeSessionManager));

            ManagedSession session;
            if (_sessions.TryGetValue(manifest.GenerationId, out var existing))
            {
                session = existing;
            }
            else
            {
                session = new ManagedSession(manifest, _factory.Create(manifest));
                _sessions.Add(manifest.GenerationId, session);
            }

            SetCurrent(session);
            AfterDisposedCheckBeforeRegistration?.Invoke();
            _activeRuns[runId] = session;
            return session;
        }
    }

    private void SetCurrent(ManagedSession current)
    {
        foreach (var session in _sessions.Values) session.IsCurrent = ReferenceEquals(session, current);
        CurrentGenerationId = current.Manifest.GenerationId;
    }

    private void RetireObsolete()
    {
        List<ManagedSession> obsolete;
        lock (_stateLock)
            obsolete = _sessions.Values.Where(session => !session.IsCurrent && !_activeRuns.Values.Contains(session)).ToList();

        foreach (var session in obsolete)
        {
            Retire(session);
            lock (_stateLock) _sessions.Remove(session.Manifest.GenerationId);
        }
    }

    public void Dispose()
    {
        List<ActiveRun> activeRuns;
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            activeRuns = _activeRuns
                .Select(item => new ActiveRun(item.Key, item.Value))
                .ToList();
        }

        foreach (var activeRun in activeRuns)
            activeRun.Session.Session.Cancel(activeRun.RunId);

        _operationLock.Wait();
        try
        {
            List<ManagedSession> sessions;
            lock (_stateLock)
            {
                sessions = _sessions.Values.ToList();
                _sessions.Clear();
                _activeRuns.Clear();
                CurrentGenerationId = null;
            }

            foreach (var session in sessions)
                Retire(session);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private sealed class ManagedSession(TestingGenerationManifest manifest, ITestingRuntimeSession session)
    {
        internal TestingGenerationManifest Manifest { get; } = manifest;
        internal ITestingRuntimeSession Session { get; } = session;
        internal bool IsCurrent { get; set; }
    }

    private void Retire(ManagedSession session)
    {
        session.Session.Dispose();
        if (session.Session is ITestingRuntimeSessionRetirementDiagnostics diagnostics
            && diagnostics.GetRetirementDiagnostic() is { } diagnostic)
        {
            lock (_stateLock)
            {
                _retainedDiagnostics.Add(diagnostic);
                _pendingRetirementDiagnostic = diagnostic;
            }
        }
    }

    private TestingRunResponse EnrichWithRetirementDiagnostic(TestingRunResponse response)
    {
        TestingGenerationRetirementDiagnostic? diagnostic;
        lock (_stateLock)
        {
            diagnostic = _pendingRetirementDiagnostic;
            _pendingRetirementDiagnostic = null;
        }

        return diagnostic is null || response.DiagnosticCode is not null
            ? response
            : response with { DiagnosticCode = diagnostic.Code, DiagnosticMessage = diagnostic.Message };
    }

    private sealed class ActiveRun(Guid runId, ManagedSession session)
    {
        internal Guid RunId { get; } = runId;
        internal ManagedSession Session { get; } = session;
    }
}
