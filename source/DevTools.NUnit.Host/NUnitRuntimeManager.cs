using System.Collections.Concurrent;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Runtime;
using DevTools.NUnit.Host.Loading;
using DevTools.NUnit.Host.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.NUnit.Host;

public sealed class NUnitRuntimeManager : IDisposable
{
    private const string CaseFinishedEventKind = "case.finished";

    private readonly INUnitGenerationBuilder _generationBuilder;
    private readonly INUnitRuntimeSessionFactory _sessionFactory;
    private readonly ILogger<NUnitRuntimeManager> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly Dictionary<string, ManagedSession> _sessionsByGenerationId = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ActiveRun> _activeRuns = new();
    private string? _currentGenerationId;
    private NUnitRuntimeDiagnostic? _pendingRuntimeDiagnostic;
    private bool _disposed;

    public NUnitRuntimeManager(
        INUnitGenerationBuilder generationBuilder,
        INUnitRuntimeSessionFactory sessionFactory,
        ILogger<NUnitRuntimeManager>? logger = null)
    {
        _generationBuilder = generationBuilder ?? throw new ArgumentNullException(nameof(generationBuilder));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _logger = logger ?? NullLogger<NUnitRuntimeManager>.Instance;
    }

    public bool IsOperationActive => _operationLock.CurrentCount == 0;

    public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        _operationLock.Wait();
        try
        {
            ThrowIfDisposed();
            var session = AcquireSession(request.AssemblyPath);
            session.BeginRequest();
            NUnitDiscoverResponse response;
            try
            {
                var discoverRequest = new NUnitDiscoverRequest(session.ShadowAssemblyPath, request.Filter);
                response = session.Session.Discover(discoverRequest);
            }
            finally
            {
                session.EndRequest();
                // Release before enrich so unload diagnostics attach to this response,
                // not the next discover/run.
                ReleaseObsoleteSessions();
            }

            return EnrichDiscoverResponse(response, session);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public NUnitRunResponse Run(
        NUnitRunRequest request,
        Action<NUnitProgressEvent> publish,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (publish is null)
            throw new ArgumentNullException(nameof(publish));

        _operationLock.Wait(cancellationToken);
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var session = AcquireSession(request.AssemblyPath);
            session.BeginRequest();
            RegisterActiveRun(request.RunId, session);
            NUnitRunResponse response;
            try
            {
                using var loggingScope = new NUnitRunLoggingScope(_logger, redirectConsole: false);
                var sink = new ProtocolEventSink(request.RunId, publish, _logger);
                var runRequest = new NUnitRunRequest(request.RunId, session.ShadowAssemblyPath, request.Filter);
                // Honor client disconnect / adapter cancel so the host executor is not left busy.
                response = session.Session.Run(runRequest, sink, cancellationToken);
            }
            finally
            {
                UnregisterActiveRun(request.RunId);
                session.EndRequest();
                // Release after logging scope disposal and before enrich so unload
                // diagnostics attach to this response, not the next discover/run.
                ReleaseObsoleteSessions();
            }

            return EnrichRunResponse(response, session);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Cancel(Guid runId)
    {
        INUnitRuntimeSession? session = null;
        lock (_stateLock)
        {
            if (_activeRuns.TryGetValue(runId, out var activeRun))
                session = activeRun.Session.Session;
        }

        session?.Cancel(runId);
    }

    public void Dispose()
    {
        List<ActiveRun> activeRuns;
        lock (_stateLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            activeRuns = _activeRuns.Values.ToList();
        }

        foreach (var activeRun in activeRuns)
        {
            try
            {
                activeRun.Session.Session.Cancel(activeRun.RunId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to cancel NUnit run {RunId} while disposing the runtime manager",
                    activeRun.RunId);
            }
        }

        _operationLock.Wait();
        try
        {
            List<ManagedSession> sessions;
            lock (_stateLock)
            {
                sessions = _sessionsByGenerationId.Values.ToList();
                _sessionsByGenerationId.Clear();
                _activeRuns.Clear();
                _currentGenerationId = null;
            }

            foreach (var session in sessions)
                session.DisposeSession();

#if NETFRAMEWORK
            if (_sessionFactory is IDisposable disposableFactory)
                disposableFactory.Dispose();
#endif
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private ManagedSession AcquireSession(string assemblyPath)
    {
        var sourceAssemblyPath = NUnitAssemblyLoader.ResolveAssemblyPath(assemblyPath);
        NUnitAssemblyLoader.EnsureLoadable(sourceAssemblyPath);

        NUnitGenerationManifest manifest;
        try
        {
            manifest = _generationBuilder.Build(sourceAssemblyPath);
        }
        catch (NUnitGenerationBuildException ex)
        {
            throw MapGenerationBuildException(sourceAssemblyPath, ex);
        }
        catch (NUnitGenerationLoadException ex)
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                sourceAssemblyPath,
                ex.Message,
                ex.ToString()));
        }

        if (!string.Equals(
                Path.GetFullPath(manifest.SourceAssemblyPath),
                sourceAssemblyPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                sourceAssemblyPath,
                "Generation source assembly path does not match the requested assembly path.",
                $"Requested: {sourceAssemblyPath}; manifest: {manifest.SourceAssemblyPath}"));
        }

        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NUnitRuntimeManager));

            if (_sessionsByGenerationId.TryGetValue(manifest.GenerationId, out var existing))
            {
                SetCurrentSession(existing);
                return existing;
            }

            var created = CreateManagedSession(manifest);
            _sessionsByGenerationId[manifest.GenerationId] = created;
            SetCurrentSession(created);
            return created;
        }
    }

    private void SetCurrentSession(ManagedSession session)
    {
        foreach (var tracked in _sessionsByGenerationId.Values)
            tracked.IsCurrent = ReferenceEquals(tracked, session);

        _currentGenerationId = session.GenerationId;
    }

    private ManagedSession CreateManagedSession(NUnitGenerationManifest manifest)
    {
        try
        {
            var session = _sessionFactory.Create(manifest);
            return new ManagedSession(manifest, session, _logger);
        }
        catch (NUnitGenerationLoadException ex)
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                manifest.SourceAssemblyPath,
                ex.Message,
                ex.ToString()));
        }
        catch (Exception ex) when (ex is not NUnitAssemblyLoadException)
        {
            throw new NUnitAssemblyLoadException(NUnitAssemblyPreflightResult.Failed(
                manifest.SourceAssemblyPath,
                $"Failed to create NUnit runtime session: {ex.Message}",
                ex.ToString()));
        }
    }

    private void RegisterActiveRun(Guid runId, ManagedSession session)
    {
        lock (_stateLock)
            _activeRuns[runId] = new ActiveRun(runId, session);
    }

    private void UnregisterActiveRun(Guid runId)
    {
        lock (_stateLock)
            _activeRuns.Remove(runId);
    }

    private void ReleaseObsoleteSessions()
    {
        List<ManagedSession> obsoleteSessions;

        lock (_stateLock)
        {
            obsoleteSessions = _sessionsByGenerationId.Values
                .Where(session =>
                    session is { IsCurrent: false, ActiveRequestCount: 0 }
                    && !session.GenerationId.Equals(_currentGenerationId, StringComparison.Ordinal))
                .ToList();
        }

        foreach (var obsolete in obsoleteSessions)
        {
            if (!obsolete.TryMarkDisposing())
                continue;

            var diagnostic = obsolete.DisposeSession();
            if (diagnostic is not null)
                _pendingRuntimeDiagnostic = diagnostic;

            lock (_stateLock)
            {
                if (_sessionsByGenerationId.TryGetValue(obsolete.GenerationId, out var tracked)
                    && ReferenceEquals(tracked, obsolete))
                    _sessionsByGenerationId.Remove(obsolete.GenerationId);
            }
        }
    }

    private NUnitDiscoverResponse EnrichDiscoverResponse(
        NUnitDiscoverResponse response,
        ManagedSession session)
    {
        var diagnostic = ConsumePendingDiagnostic();
        var generationId = string.IsNullOrWhiteSpace(response.GenerationId)
            ? session.GenerationId
            : response.GenerationId;

        if (!string.Equals(generationId, session.GenerationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Discover response generation ID '{generationId}' does not match session '{session.GenerationId}'.");
        }

        return new NUnitDiscoverResponse(
            response.Cases,
            generationId,
            diagnostic ?? response.RuntimeDiagnostic);
    }

    private NUnitRunResponse EnrichRunResponse(NUnitRunResponse response, ManagedSession session)
    {
        var diagnostic = ConsumePendingDiagnostic();
        var generationId = string.IsNullOrWhiteSpace(response.GenerationId)
            ? session.GenerationId
            : response.GenerationId;

        if (!string.Equals(generationId, session.GenerationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Run response generation ID '{generationId}' does not match session '{session.GenerationId}'.");
        }

        return new NUnitRunResponse(
            response.RunId,
            response.Summary,
            response.Cases,
            generationId,
            diagnostic ?? response.RuntimeDiagnostic);
    }

    private NUnitRuntimeDiagnostic? ConsumePendingDiagnostic()
    {
        var diagnostic = _pendingRuntimeDiagnostic;
        _pendingRuntimeDiagnostic = null;
        return diagnostic;
    }

    private static NUnitAssemblyLoadException MapGenerationBuildException(
        string assemblyPath,
        NUnitGenerationBuildException ex) =>
        new(NUnitAssemblyPreflightResult.Failed(
            assemblyPath,
            ex.Message,
            ex.ToString()));

    private void ThrowIfDisposed()
    {
        lock (_stateLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NUnitRuntimeManager));
        }
    }

    private sealed class ActiveRun
    {
        internal ActiveRun(Guid runId, ManagedSession session)
        {
            RunId = runId;
            Session = session;
        }

        internal Guid RunId { get; }

        internal ManagedSession Session { get; }
    }

    private sealed class ManagedSession
    {
        private readonly ILogger _logger;
        private int _activeRequestCount;
        private int _disposing;

        internal ManagedSession(
            NUnitGenerationManifest manifest,
            INUnitRuntimeSession session,
            ILogger logger)
        {
            Manifest = manifest;
            Session = session;
            _logger = logger;
        }

        private NUnitGenerationManifest Manifest { get; }

        internal INUnitRuntimeSession Session { get; }

        internal string GenerationId => Manifest.GenerationId;

        internal string ShadowAssemblyPath => Manifest.ShadowAssemblyPath;

        internal bool IsCurrent { get; set; }

        internal int ActiveRequestCount => _activeRequestCount;

        internal void BeginRequest() => Interlocked.Increment(ref _activeRequestCount);

        internal void EndRequest() => Interlocked.Decrement(ref _activeRequestCount);

        internal bool TryMarkDisposing() =>
            Interlocked.CompareExchange(ref _disposing, 1, 0) == 0 && _activeRequestCount == 0;

        internal NUnitRuntimeDiagnostic? DisposeSession()
        {
            IsCurrent = false;
            NUnitRuntimeDiagnostic? diagnostic = null;

            try
            {
                Session.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose NUnit runtime session for generation {GenerationId}", GenerationId);
            }

#if DEBUG && NET
            if (Session is NUnitRuntimeSessionHandle sessionHandle)
            {
                diagnostic = sessionHandle.VerifyUnload();
                if (string.Equals(diagnostic.Code, NUnitRuntimeUnloadVerifier.RetainedCode, StringComparison.Ordinal))
                    _logger.LogInformation(
                        "Generation {GenerationId} ALC retained after session disposal: {Message}",
                        GenerationId,
                        diagnostic.Message);
            }
#elif DEBUG && NETFRAMEWORK
            if (Session is NetfxNUnitSessionHandle netfxHandle)
            {
                diagnostic = netfxHandle.CreateRetainedDiagnostic();
                _logger.LogInformation(
                    "Generation {GenerationId} retained in AppDomain after session disposal: {Message}",
                    GenerationId,
                    diagnostic.Message);
            }
#endif

            return diagnostic;
        }
    }

    private sealed class ProtocolEventSink : INUnitRuntimeEventSink
    {
        private readonly Guid _runId;
        private readonly Action<NUnitProgressEvent> _publish;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, byte> _publishedTerminalCaseIds =
            new(StringComparer.Ordinal);

        internal ProtocolEventSink(
            Guid runId,
            Action<NUnitProgressEvent> publish,
            ILogger logger)
        {
            _runId = runId;
            _publish = publish;
            _logger = logger;
        }

        public void Publish(NUnitRuntimeEvent runtimeEvent)
        {
            if (runtimeEvent.RunId != _runId)
                return;

            if (!string.Equals(runtimeEvent.Kind, CaseFinishedEventKind, StringComparison.Ordinal))
                return;

            if (runtimeEvent.Case is null)
                return;

            if (!_publishedTerminalCaseIds.TryAdd(runtimeEvent.Case.Id, 0))
                return;

            var result = runtimeEvent.Case;
            if (!string.IsNullOrWhiteSpace(result.Output))
                _logger.LogInformation("[NUnit:{TestName}] {Output}", result.Name, result.Output!.TrimEnd());

            _publish(new NUnitProgressEvent(_runId, result));
        }
    }
}
