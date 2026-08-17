using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Results;
using DevTools.NUnit.Transport.Runtime;
using DevTools.NUnit.Host.Loading;
using DevTools.NUnit.Host.Tests.Loading;
using DevTools.NUnit.Host.Tests.TestSupport;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitHostTestFrameworkProviderTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyOptions =
        new Dictionary<string, string>();

    [Fact]
    public void FrameworkId_is_nunit()
    {
        using var workspace = new TempWorkspace();
        var provider = CreateProvider(workspace, new FakeNUnitRuntimeSessionFactory());
        Assert.Equal(NUnitFramework.Id, provider.FrameworkId);
    }

    [Fact]
    public void Run_matches_host_path_for_fake_session_fields()
    {
        using var workspace = new TempWorkspace();
        var assemblyPath = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "fake-parity");
        var factory = new FakeNUnitRuntimeSessionFactory();
        factory.CreateImpl = generation =>
        {
            var session = new FakeNUnitRuntimeSession(generation.GenerationId, generation.ShadowAssemblyPath)
            {
                RunImpl = (request, sink) =>
                {
                    var result = new NUnitCaseResult(
                        "authoritative-id",
                        "Display_Name",
                        NUnitOutcomes.Passed,
                        3,
                        "ok",
                        null,
                        "captured-output",
                        Traits: [new NUnitTrait("Category", "AcceptanceCategory")],
                        Source: new NUnitSourceLocation("FullSemanticsFixture.cs", 10),
                        Attachments: [new NUnitAttachment("note", null, @"C:\tmp\note.txt", null)]);
                    sink.Publish(new NUnitRuntimeEvent(
                        request.RunId,
                        "case.finished",
                        result,
                        null,
                        null));
                    return new NUnitRunResponse(
                        request.RunId,
                        new NUnitRunSummary(1, 0, 0, 0, 0, 0),
                        [result],
                        generation.GenerationId);
                },
            };
            return session;
        };

        var host = CreateHost(factory, workspace.GenerationsRoot);
        var provider = new NUnitHostTestFrameworkProvider(host);
        var runId = Guid.NewGuid();
        var hostCases = new List<NUnitProgressEvent>();
        var hostResponse = host.Run(
            new NUnitRunRequest(runId, assemblyPath, null),
            hostCases.Add,
            TestContext.Current.CancellationToken);

        var sink = new RecordingTestingSink();
        var testingResponse = provider.Run(
            CreateRunRequest(runId, assemblyPath, new TestingSelection([], null)),
            sink,
            TestContext.Current.CancellationToken);

        Assert.Equal(hostResponse.GenerationId, testingResponse.GenerationId);
        Assert.Equal(hostResponse.Cases.Count, testingResponse.Results.Count);
        Assert.Equal(hostResponse.Cases[0].Id, testingResponse.Results[0].TestId);
        Assert.NotEqual(hostResponse.Cases[0].Name, testingResponse.Results[0].TestId);
        Assert.Equal(hostResponse.Cases[0].Name, testingResponse.Results[0].DisplayName);
        Assert.Equal(hostResponse.Cases[0].Outcome, testingResponse.Results[0].Outcome);
        Assert.Equal(hostResponse.Cases[0].Output, testingResponse.Results[0].Output);
        Assert.Equal(hostResponse.Cases[0].Source!.File, testingResponse.Results[0].Source!.File);
        Assert.Equal(
            hostResponse.Cases[0].Traits![0].Value,
            testingResponse.Results[0].Traits[0].Value);
        Assert.Equal(hostResponse.Cases[0].Attachments![0].Path, testingResponse.Results[0].Attachments[0].Path);
        Assert.Equal(hostResponse.Cases[0].Attachments![0].ContentType, testingResponse.Results[0].Attachments[0].ContentType);
        Assert.Equal(hostCases[0].Case.Id, Assert.Single(sink.Events).Case!.TestId);
        Assert.Equal(TestingCancellationState.None, testingResponse.CancellationState);
    }

    [Fact]
    public void Run_forwards_opaque_test_ids_as_nunit_filter_xml()
    {
        using var workspace = new TempWorkspace();
        var assemblyPath = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "filter-ids");
        string? seenFilter = null;
        var factory = new FakeNUnitRuntimeSessionFactory();
        factory.CreateImpl = generation => new FakeNUnitRuntimeSession(generation.GenerationId, generation.ShadowAssemblyPath)
        {
            RunImpl = (request, _) =>
            {
                seenFilter = request.Filter;
                return new NUnitRunResponse(
                    request.RunId,
                    new NUnitRunSummary(0, 0, 0, 0, 0, 0),
                    [],
                    generation.GenerationId);
            },
        };

        var provider = CreateProvider(workspace, factory);
        const string testId = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        provider.Run(
            CreateRunRequest(Guid.NewGuid(), assemblyPath, new TestingSelection([testId], null)),
            new RecordingTestingSink(),
            TestContext.Current.CancellationToken);

        Assert.Equal($"<filter><test>{testId}</test></filter>", seenFilter);
    }

    [Fact]
    public void Cancel_reaches_host()
    {
        var cancelled = Guid.Empty;
        var provider = new NUnitHostTestFrameworkProvider(new CancelCapturingHost(runId => cancelled = runId));
        var runId = Guid.NewGuid();

        Assert.True(provider.Cancel(runId));
        Assert.Equal(runId, cancelled);
    }

    [Fact]
    public void Focused_fixture_run_preserves_id_outcome_traits_and_generation()
    {
        using var workspace = new TempWorkspace();
        var assemblyPath = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "focused-provider");
        var provider = CreateProvider(workspace, new NUnitRuntimeSessionFactory());
        const string fullName = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";

        var sink = new RecordingTestingSink();
        var response = provider.Run(
            CreateRunRequest(Guid.NewGuid(), assemblyPath, new TestingSelection([fullName], null)),
            sink,
            TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Results);
        Assert.Equal(NUnitOutcomes.Passed, result.Outcome);
        Assert.Equal("PlainTest_Passes", result.DisplayName);
        Assert.NotEqual(result.DisplayName, result.TestId);
        Assert.False(string.IsNullOrWhiteSpace(response.GenerationId));
        Assert.Equal(result.TestId, Assert.Single(sink.Events).Case!.TestId);

        var categoryResponse = provider.Run(
            CreateRunRequest(
                Guid.NewGuid(),
                assemblyPath,
                new TestingSelection([], "<filter><cat>AcceptanceCategory</cat></filter>")),
            new RecordingTestingSink(),
            TestContext.Current.CancellationToken);

        var category = Assert.Single(categoryResponse.Results);
        Assert.Equal("CategoryAndProperty_AreAttached", category.DisplayName);
        Assert.Equal(NUnitOutcomes.Passed, category.Outcome);
        Assert.Contains(category.Traits, trait => trait is { Name: "Category", Value: "AcceptanceCategory" });
        Assert.Contains(category.Traits, trait => trait is { Name: "AcceptanceKey", Value: "AcceptanceValue" });
        Assert.Contains("FullSemanticsFixture.cs", category.Source!.File, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(response.GenerationId, categoryResponse.GenerationId);
    }

    private static NUnitHostTestFrameworkProvider CreateProvider(
        TempWorkspace workspace,
        INUnitRuntimeSessionFactory factory) =>
        new(CreateHost(factory, workspace.GenerationsRoot));

    private static NUnitHost CreateHost(INUnitRuntimeSessionFactory sessionFactory, string generationsRoot) =>
        new(
            new NUnitRuntimeManager(
                NUnitRuntimeTestEnvironment.CreateBuilder(generationsRoot),
                sessionFactory,
                NullLogger<NUnitRuntimeManager>.Instance),
            NullLogger<NUnitHost>.Instance);

    private static TestingRunRequest CreateRunRequest(
        Guid runId,
        string assemblyPath,
        TestingSelection selection) =>
        new(
            TestingProtocol.CurrentVersion,
            runId,
            NUnitFramework.Id,
            new TestingAssemblyReference(assemblyPath, "net10.0-windows", null),
            selection,
            EmptyOptions);

    private sealed class RecordingTestingSink : ITestingEventSink
    {
        public List<TestingEvent> Events { get; } = [];

        public void Publish(TestingEvent testingEvent) => Events.Add(testingEvent);
    }

    private sealed class CancelCapturingHost(Action<Guid> onCancel) : INUnitHost
    {
        public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request) =>
            new(Array.Empty<NUnitDiscoveredTest>());

        public NUnitRunResponse Run(
            NUnitRunRequest request,
            Action<NUnitProgressEvent> publish,
            CancellationToken cancellationToken = default) =>
            new(request.RunId, new NUnitRunSummary(0, 0, 0, 0, 0, 0), Array.Empty<NUnitCaseResult>());

        public void Cancel(Guid runId) => onCancel(runId);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "DevTools",
                "NUnit",
                "ProviderTests",
                Guid.NewGuid().ToString("N"));
            GenerationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string GenerationsRoot { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
                if (Directory.Exists(GenerationsRoot))
                    Directory.Delete(GenerationsRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp workspaces.
            }
        }
    }
}
