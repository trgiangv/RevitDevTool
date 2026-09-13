using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.Testing.Host.Tests.Loading;

[TestClass]
public sealed class TestingGenerationStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Build_uses_a_deterministic_content_generation_id()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var text = workspace.Write("content/readme.txt", "same");
        var plan = workspace.Plan(assembly, [
            new TestingGenerationFile(text, "content/readme.txt", TestingGenerationFileKind.Other),
            new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed),
        ]);

        var first = workspace.Store.Build(new FixedPolicy(plan), assembly);
        var second = workspace.Store.Build(new FixedPolicy(plan with { Files = plan.Files.Reverse().ToList() }), assembly);

        Assert.AreEqual(first.GenerationId, second.GenerationId);
    }

    [TestMethod]
    public void Build_retries_when_a_source_changes_during_snapshot()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var changing = workspace.Write("content/changing.txt", "before");
        var plan = workspace.Plan(assembly, [
            new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed),
            new TestingGenerationFile(changing, "content/changing.txt", TestingGenerationFileKind.Other),
        ]);
        var changed = false;
        workspace.Store.AfterFileCopied = source =>
        {
            if (!changed && string.Equals(source, changing, StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(changing, "after");
                changed = true;
            }
        };

        var manifest = workspace.Store.Build(new FixedPolicy(plan), assembly);

        Assert.IsTrue(changed);
        Assert.AreEqual("after", File.ReadAllText(Path.Combine(manifest.ShadowDirectory, "content", "changing.txt")));
    }

    [TestMethod]
    public void Build_retries_when_a_same_length_source_change_preserves_its_timestamp()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var changing = workspace.Write("content/changing.txt", "before");
        var originalTimestamp = File.GetLastWriteTimeUtc(changing);
        var plan = workspace.Plan(assembly, [
            new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed),
            new TestingGenerationFile(changing, "content/changing.txt", TestingGenerationFileKind.Other),
        ]);
        var changed = false;
        workspace.Store.AfterFileCopied = source =>
        {
            if (!changed && string.Equals(source, changing, StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(changing, "after!");
                File.SetLastWriteTimeUtc(changing, originalTimestamp);
                changed = true;
            }
        };

        var manifest = workspace.Store.Build(new FixedPolicy(plan), assembly);

        Assert.IsTrue(changed);
        Assert.AreEqual("after!", File.ReadAllText(Path.Combine(manifest.ShadowDirectory, "content", "changing.txt")));
    }

    [TestMethod]
    public void Build_rejects_a_corrupt_published_generation()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var plan = workspace.Plan(assembly, [new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed)]);
        var policy = new FixedPolicy(plan);
        var published = workspace.Store.Build(policy, assembly);
        File.AppendAllText(published.ShadowAssemblyPath, "corrupt");

        Assert.ThrowsExactly<TestingGenerationCorruptionException>(() => workspace.Store.Build(policy, assembly));
    }

    [TestMethod]
    public void Build_rejects_an_incomplete_existing_generation_directory()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var plan = workspace.Plan(assembly, [new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed)]);
        var generationId = TestingGenerationContentHash.ComputeGenerationId([
            ("sample.dll", assembly),
        ]);
        Directory.CreateDirectory(Path.Combine(workspace.GenerationsRoot, generationId));

        var exception = Assert.ThrowsExactly<TestingGenerationBuildException>(() => workspace.Store.Build(new FixedPolicy(plan), assembly));

        Assert.Contains("complete published generation", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Build_publishes_only_complete_generations_when_concurrent_callers_share_content()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var extra = workspace.Write("content/data.bin", "data");
        var plan = workspace.Plan(assembly, [
            new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed),
            new TestingGenerationFile(extra, "content/data.bin", TestingGenerationFileKind.Other),
        ]);

        var manifests = Enumerable.Range(0, 8)
            .AsParallel()
            .Select(_ => workspace.Store.Build(new FixedPolicy(plan), assembly))
            .ToList();

        var manifest = Assert.ContainsSingle(manifests.DistinctBy(item => item.GenerationId));
        Assert.IsTrue(File.Exists(Path.Combine(manifest.ShadowDirectory, ".generation-complete")));
        Assert.IsFalse(Directory.EnumerateDirectories(workspace.GenerationsRoot)
            .Any(path => Path.GetFileName(path).StartsWith(".staging.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CopyDirectory_publishes_nested_files_when_move_is_unavailable()
    {
        using var workspace = new GenerationWorkspace();
        var staging = Path.Combine(workspace.GenerationsRoot, ".staging.copy");
        var shadow = Path.Combine(workspace.GenerationsRoot, "copied");
        Directory.CreateDirectory(Path.Combine(staging, "nested"));
        Directory.CreateDirectory(Path.Combine(staging, "empty"));
        File.WriteAllText(Path.Combine(staging, "nested", "payload.txt"), "ok");
        File.WriteAllText(Path.Combine(staging, TestingGenerationPaths.GenerationCompleteMarkerFileName), string.Empty);

        TestingGenerationSnapshot.CopyDirectory(staging, shadow);

        Assert.AreEqual("ok", File.ReadAllText(Path.Combine(shadow, "nested", "payload.txt")));
        Assert.IsTrue(Directory.Exists(Path.Combine(shadow, "empty")));
        Assert.IsTrue(File.Exists(Path.Combine(shadow, TestingGenerationPaths.GenerationCompleteMarkerFileName)));
    }

    [TestMethod]
    public void Publish_copies_when_directory_move_fails()
    {
        using var workspace = new GenerationWorkspace();
        var staging = Path.Combine(workspace.GenerationsRoot, ".staging.move-fail");
        var shadow = Path.Combine(workspace.GenerationsRoot, "copied-from-lock");
        Directory.CreateDirectory(staging);
        var payload = Path.Combine(staging, "payload.txt");
        File.WriteAllText(payload, "ok");
        var generationId = TestingGenerationSnapshot.ComputeGenerationId(
            staging,
            TestingGenerationSnapshot.ReadContentRelativePaths(staging));

        using (var stream = new FileStream(payload, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            TestingGenerationSnapshot.Publish(staging, shadow, generationId);
            GC.KeepAlive(stream);
        }

        Assert.IsTrue(Directory.Exists(shadow));
        Assert.AreEqual("ok", File.ReadAllText(Path.Combine(shadow, "payload.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(shadow, TestingGenerationPaths.GenerationCompleteMarkerFileName)));
    }

    [TestMethod]
    public void Build_indexes_each_declared_file_kind_without_filename_policy()
    {
        using var workspace = new GenerationWorkspace();
        var managed = workspace.CopyManaged("provider/module.bin");
        var native = workspace.Write("native/asset.bin", "native");
        var symbols = workspace.Write("symbols/debug.bin", "symbols");
        var other = workspace.Write("data/config.bin", "other");
        var plan = workspace.Plan(managed, [
            new TestingGenerationFile(managed, "provider/module.bin", TestingGenerationFileKind.Managed),
            new TestingGenerationFile(native, "native/asset.bin", TestingGenerationFileKind.Native),
            new TestingGenerationFile(symbols, "symbols/debug.bin", TestingGenerationFileKind.Symbols),
            new TestingGenerationFile(other, "data/config.bin", TestingGenerationFileKind.Other),
        ]);

        var manifest = workspace.Store.Build(new FixedPolicy(plan), managed);

        Assert.ContainsSingle(manifest.ManagedAssemblies);
        Assert.ContainsSingle(manifest.NativeAssets);
        Assert.ContainsSingle(manifest.SymbolFiles);
        Assert.ContainsSingle(manifest.OtherFiles);
    }

    [TestMethod]
    public void Runtime_manager_retires_an_obsolete_session_after_the_current_generation_changes()
    {
        using var workspace = new GenerationWorkspace();
        var first = workspace.CopyManaged("first.dll");
        var second = workspace.CopyManaged("second.dll");
        var policy = new MappingPolicy(first, workspace.Plan(first, [new TestingGenerationFile(first, "first.dll", TestingGenerationFileKind.Managed)]),
            second, workspace.Plan(second, [new TestingGenerationFile(second, "second.dll", TestingGenerationFileKind.Managed)]));
        var factory = new RecordingSessionFactory();
        using var manager = new TestingRuntimeSessionManager(workspace.Store, policy, factory);

        manager.Run(Request(first), NullTestingRuntimeEventSink.Instance, TestContext.CancellationToken);
        manager.Run(Request(second), NullTestingRuntimeEventSink.Instance, TestContext.CancellationToken);

        Assert.IsTrue(factory.Sessions.Single(session => session.GenerationId != manager.CurrentGenerationId).Disposed);
        Assert.AreEqual(0, manager.RetainedGenerationCount);
    }

    [TestMethod]
    public async Task Runtime_manager_forwards_cancel_to_the_active_neutral_session()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var policy = new FixedPolicy(workspace.Plan(assembly, [new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed)]));
        var factory = new RecordingSessionFactory(blockRuns: true);
        using var manager = new TestingRuntimeSessionManager(workspace.Store, policy, factory);
        var request = Request(assembly);

        var run = Task.Run(() => manager.Run(request, NullTestingRuntimeEventSink.Instance), TestContext.CancellationToken);
        Assert.IsTrue(factory.RunStarted.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        manager.Cancel(request.RunId);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.IsTrue(factory.Sessions.Single().Cancelled);
    }

    [TestMethod]
    public async Task Cancel_returns_false_when_the_session_is_already_disposed()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var policy = new FixedPolicy(workspace.Plan(assembly, [new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed)]));
        var factory = new RecordingSessionFactory(blockRuns: true, throwOnCancel: true);
        using var manager = new TestingRuntimeSessionManager(workspace.Store, policy, factory);
        var request = Request(assembly);

        var run = Task.Run(() => manager.Run(request, NullTestingRuntimeEventSink.Instance), TestContext.CancellationToken);
        Assert.IsTrue(factory.RunStarted.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        Assert.IsFalse(manager.Cancel(request.RunId));
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Runtime_manager_dispose_cancels_and_waits_for_an_active_run_before_retiring_its_session()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var policy = new FixedPolicy(workspace.Plan(assembly, [new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed)]));
        var factory = new RecordingSessionFactory(blockUntilReleasedAfterCancel: true, retainOnDispose: true);
        var manager = new TestingRuntimeSessionManager(workspace.Store, policy, factory);
        var request = Request(assembly);

        var run = Task.Run(() => manager.Run(request, NullTestingRuntimeEventSink.Instance), TestContext.CancellationToken);
        Assert.IsTrue(factory.RunStarted.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        var disposing = Task.Run(manager.Dispose, TestContext.CancellationToken);
        Assert.IsTrue(factory.CancelObserved.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        Assert.IsFalse(factory.Sessions.Single().Disposed);
        Assert.IsFalse(disposing.IsCompleted);

        factory.AllowRunToFinish.Set();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        await disposing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.IsTrue(factory.Sessions.Single().Disposed);
        Assert.AreEqual("generation.retained", Assert.ContainsSingle(manager.RetainedGenerationDiagnostics).Code);
    }

    [TestMethod]
    public async Task Runtime_manager_dispose_does_not_miss_a_run_paused_before_active_registration()
    {
        using var workspace = new GenerationWorkspace();
        var assembly = workspace.CopyManaged("sample.dll");
        var policy = new FixedPolicy(workspace.Plan(assembly, [new TestingGenerationFile(assembly, "sample.dll", TestingGenerationFileKind.Managed)]));
        var factory = new RecordingSessionFactory(blockUntilReleasedAfterCancel: true, retainOnDispose: true);
        var manager = new TestingRuntimeSessionManager(workspace.Store, policy, factory);
        var request = Request(assembly);
        using var registrationGateReached = new ManualResetEventSlim();
        using var allowRegistration = new ManualResetEventSlim();
        manager.AfterDisposedCheckBeforeRegistration = () =>
        {
            registrationGateReached.Set();
            allowRegistration.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        };

        var run = Task.Run(() => manager.Run(request, NullTestingRuntimeEventSink.Instance), TestContext.CancellationToken);
        Assert.IsTrue(registrationGateReached.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        var disposing = Task.Run(manager.Dispose, TestContext.CancellationToken);
        Assert.IsFalse(disposing.IsCompleted);

        allowRegistration.Set();
        Assert.IsTrue(factory.CancelObserved.Wait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        Assert.IsFalse(factory.Sessions.Single().Disposed);
        factory.AllowRunToFinish.Set();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        await disposing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        Assert.IsTrue(factory.Sessions.Single().Disposed);
        Assert.AreEqual("generation.retained", Assert.ContainsSingle(manager.RetainedGenerationDiagnostics).Code);
    }

    [TestMethod]
    public void Runtime_manager_exposes_provider_retained_generation_diagnostics()
    {
        using var workspace = new GenerationWorkspace();
        var first = workspace.CopyManaged("first.dll");
        var second = workspace.CopyManaged("second.dll");
        var policy = new MappingPolicy(first, workspace.Plan(first, [new TestingGenerationFile(first, "first.dll", TestingGenerationFileKind.Managed)]),
            second, workspace.Plan(second, [new TestingGenerationFile(second, "second.dll", TestingGenerationFileKind.Managed)]));
        var factory = new RecordingSessionFactory(retainOnDispose: true);
        using var manager = new TestingRuntimeSessionManager(workspace.Store, policy, factory);

        manager.Run(Request(first), NullTestingRuntimeEventSink.Instance, TestContext.CancellationToken);
        manager.Run(Request(second), NullTestingRuntimeEventSink.Instance, TestContext.CancellationToken);

        var diagnostic = Assert.ContainsSingle(manager.RetainedGenerationDiagnostics);
        Assert.AreEqual("generation.retained", diagnostic.Code);
    }

    private static TestRunRequest Request(string path) => new(
        1, Guid.NewGuid(), TestFrameworkId.NUnit, new TestAssemblyReference(path),
        TestSelection.All);

    private sealed class FixedPolicy(TestingGenerationPlan plan) : ITestingGenerationPolicy
    {
        public TestingGenerationPlan CreatePlan(string testAssemblyPath) => plan;
        public void ValidatePublished(TestingGenerationManifest manifest) { }
    }

    private sealed class MappingPolicy(string firstPath, TestingGenerationPlan first, string secondPath, TestingGenerationPlan second) : ITestingGenerationPolicy
    {
        public TestingGenerationPlan CreatePlan(string testAssemblyPath)
        {
            if (string.Equals(Path.GetFullPath(testAssemblyPath), Path.GetFullPath(firstPath), StringComparison.OrdinalIgnoreCase))
                return first;
            if (string.Equals(Path.GetFullPath(testAssemblyPath), Path.GetFullPath(secondPath), StringComparison.OrdinalIgnoreCase))
                return second;
            throw new InvalidOperationException("Unexpected test assembly.");
        }
        public void ValidatePublished(TestingGenerationManifest manifest) { }
    }

    private sealed class RecordingSessionFactory(bool blockRuns = false, bool blockUntilReleasedAfterCancel = false, bool retainOnDispose = false, bool throwOnCancel = false) : ITestingRuntimeSessionFactory
    {
        public List<RecordingSession> Sessions { get; } = [];
        public ManualResetEventSlim RunStarted { get; } = new();
        public ManualResetEventSlim CancelObserved { get; } = new();
        public ManualResetEventSlim AllowRunToFinish { get; } = new();

        public ITestingRuntimeSession Create(TestingGenerationManifest generation)
        {
            var session = new RecordingSession(generation.GenerationId, blockRuns, blockUntilReleasedAfterCancel, retainOnDispose, throwOnCancel, RunStarted, CancelObserved, AllowRunToFinish);
            Sessions.Add(session);
            return session;
        }
    }

    private sealed class RecordingSession(string generationId, bool blockRuns, bool blockUntilReleasedAfterCancel, bool retainOnDispose, bool throwOnCancel, ManualResetEventSlim runStarted, ManualResetEventSlim cancelObserved, ManualResetEventSlim allowRunToFinish) : ITestingRuntimeSession, ITestingRuntimeSessionRetirementDiagnostics
    {
        private readonly ManualResetEventSlim _cancelled = new();
        public string GenerationId { get; } = generationId;
        public bool Cancelled { get; private set; }
        public bool Disposed { get; private set; }
        public TestRunResponse Run(TestRunRequest request, ITestingRuntimeEventSink eventSink, CancellationToken cancellationToken)
        {
            runStarted.Set();
            if (blockRuns)
                _cancelled.Wait(TimeSpan.FromSeconds(5), cancellationToken);
            if (blockUntilReleasedAfterCancel)
            {
                _cancelled.Wait(TimeSpan.FromSeconds(5), cancellationToken);
                allowRunToFinish.Wait(TimeSpan.FromSeconds(5), cancellationToken);
            }
            return new TestRunResponse(request.RunId, request.FrameworkId, GenerationId, [], TestCancellationState.None, null, null);
        }
        public void Cancel(Guid runId)
        {
            Cancelled = true;
            cancelObserved.Set();
            _cancelled.Set();
            if (throwOnCancel)
                throw new ObjectDisposedException(nameof(RecordingSession));
        }
        public void Dispose() => Disposed = true;
        public TestingGenerationRetirementDiagnostic? GetRetirementDiagnostic() => retainOnDispose
            ? new TestingGenerationRetirementDiagnostic(GenerationId, "generation.retained", "retained by provider") : null;
    }

    private sealed class GenerationWorkspace : IDisposable
    {
        public GenerationWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "DevTools.Testing.Host.Tests", Guid.NewGuid().ToString("N"));
            GenerationsRoot = Path.Combine(Root, "generations");
            Directory.CreateDirectory(Root);
            Store = new TestingGenerationStore(GenerationsRoot);
        }
        public string Root { get; }
        public string GenerationsRoot { get; }
        public TestingGenerationStore Store { get; }
        public string CopyManaged(string relativePath)
        {
            var destination = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(typeof(TestingGenerationStoreTests).Assembly.Location, destination, true);
            return destination;
        }
        public string Write(string relativePath, string contents)
        {
            var destination = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, contents);
            return destination;
        }
        public TestingGenerationPlan Plan(string assembly, IReadOnlyList<TestingGenerationFile> files) =>
            new(TestFrameworkId.NUnit, assembly, files, files[0].RelativePath);
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
