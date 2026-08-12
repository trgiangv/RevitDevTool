using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[Collection(nameof(AcceptanceFixtureCollection))]
public sealed class NUnitRuntimeQualityGateTests
{
    [Fact]
    public void Filter_RejectsNonXmlText()
    {
        using var session = FixtureTestHarness.CreateSession();

        var exception = Assert.Throws<ArgumentException>(() =>
            session.Discover(new NUnitDiscoverRequest(
                FixtureTestHarness.FixtureAssemblyPath,
                "cat == 'AcceptanceCategory'")));

        Assert.Contains("TestFilter.FromXml", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Discover_RejectsMismatchedAssemblyPath()
    {
        using var session = FixtureTestHarness.CreateSession();

        var exception = Assert.Throws<ArgumentException>(() =>
            session.Discover(new NUnitDiscoverRequest("C:\\missing\\other.dll", null)));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_RejectsMismatchedAssemblyPath()
    {
        using var session = FixtureTestHarness.CreateSession();

        var exception = Assert.Throws<ArgumentException>(() =>
            session.Run(
                new NUnitRunRequest(Guid.NewGuid(), "C:\\missing\\other.dll", null),
                new RecordingEventSink(),
                CancellationToken.None));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestIds_AreStableAcrossFreshSessions()
    {
        IReadOnlyDictionary<string, string> DiscoverIds()
        {
            using var session = FixtureTestHarness.CreateSession();
            var response = session.Discover(new NUnitDiscoverRequest(FixtureTestHarness.FixtureAssemblyPath, null));
            return response.Cases.ToDictionary(test => test.FullName, test => test.Id, StringComparer.Ordinal);
        }

        var first = DiscoverIds();
        var second = DiscoverIds();

        Assert.Equal(first.Count, second.Count);
        foreach (var pair in first)
        {
            Assert.True(second.TryGetValue(pair.Key, out var otherId));
            Assert.Equal(pair.Value, otherId);
        }
    }

    [Fact]
    public void DuplicateDisplayNames_ReceiveDistinctStableIdsAcrossFreshSessions()
    {
        IReadOnlyList<NUnitDiscoveredTest> DiscoverDuplicateCases()
        {
            using var session = DedicatedTestFixturesHarness.CreateSession();
            var response = session.Discover(new NUnitDiscoverRequest(
                DedicatedTestFixturesHarness.AssemblyPath,
                DedicatedTestFixturesHarness.DuplicateNameFilter));
            return response.Cases;
        }

        var first = DiscoverDuplicateCases();
        var second = DiscoverDuplicateCases();

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Equal(first[0].FullName, first[1].FullName);
        Assert.NotEqual(first[0].Id, first[1].Id);
        Assert.Equal(first.Select(test => test.Id), second.Select(test => test.Id));
    }

    [Fact]
    public void RunResultIds_MatchDiscoveryIds()
    {
        FixtureTestHarness.ResetAcceptanceLog();
        using var session = FixtureTestHarness.CreateSession();
        var discovered = session.Discover(new NUnitDiscoverRequest(FixtureTestHarness.FixtureAssemblyPath, null));
        var discoveryById = discovered.Cases.ToDictionary(test => test.Id, StringComparer.Ordinal);

        var response = session.Run(
            new NUnitRunRequest(Guid.NewGuid(), FixtureTestHarness.FixtureAssemblyPath, null),
            new RecordingEventSink(),
            CancellationToken.None);

        var discoveryIds = discoveryById.Keys.ToHashSet(StringComparer.Ordinal);
        var runIds = response.Cases.Select(test => test.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(discoveryIds, runIds);

        foreach (var result in response.Cases)
        {
            var discoveredCase = discoveryById[result.Id];
            Assert.Equal(discoveredCase.Name, result.Name);
        }
    }

    [Fact]
    public void DuplicateDisplayNames_RunIdsMatchDiscoveryIds()
    {
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var discovered = session.Discover(new NUnitDiscoverRequest(
            DedicatedTestFixturesHarness.AssemblyPath,
            DedicatedTestFixturesHarness.DuplicateNameFilter));
        var discoveryById = discovered.Cases.ToDictionary(test => test.Id, StringComparer.Ordinal);

        var response = session.Run(
            new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.DuplicateNameFilter),
            new RecordingEventSink(),
            CancellationToken.None);

        Assert.Equal(2, response.Cases.Count);
        Assert.Equal(discoveryById.Keys.ToHashSet(StringComparer.Ordinal), response.Cases.Select(test => test.Id).ToHashSet(StringComparer.Ordinal));
        Assert.All(response.Cases, result => Assert.Equal(NUnitOutcomes.Passed, result.Outcome));
    }

    [Fact]
    public void Discover_ResolvesSourceForParameterizedAndGenericCases()
    {
        using var session = FixtureTestHarness.CreateSession();
        var response = session.Discover(new NUnitDiscoverRequest(FixtureTestHarness.FixtureAssemblyPath, null));

        var parameterized = Assert.Single(
            response.Cases,
            test => test.FullName == "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(3).FixtureSource_ValueIsPreserved");
        Assert.NotNull(parameterized.Source);
        Assert.Contains("ParameterizedFixture.cs", parameterized.Source!.File, StringComparison.OrdinalIgnoreCase);

        var generic = Assert.Single(
            response.Cases,
            test => test.FullName == "DevTools.NUnit.Runtime.Fixtures.GenericFixture<Int32>.GenericFixture_UsesRequestedType");
        Assert.NotNull(generic.Source);
        Assert.Contains("ParameterizedFixture.cs", generic.Source!.File, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_PublishesOutputAttachmentAndCaseFinishedEvents()
    {
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var sink = new RecordingEventSink();

        var response = session.Run(
            new NUnitRunRequest(Guid.NewGuid(), DedicatedTestFixturesHarness.AssemblyPath, DedicatedTestFixturesHarness.AttachmentFilter),
            sink,
            CancellationToken.None);

        var result = Assert.Single(response.Cases);
        Assert.Equal(NUnitOutcomes.Passed, result.Outcome);
        Assert.Contains("acceptance-warning-text", result.Message, StringComparison.Ordinal);

        var attachment = Assert.Single(result.Attachments!);
        Assert.Equal("acceptance-attachment", attachment.Name);
        Assert.True(File.Exists(attachment.Path));

        Assert.Contains(
            sink.Events,
            runtimeEvent => runtimeEvent.Kind == NUnitRuntimeEventKinds.CaseOutput
                && runtimeEvent.Message!.Contains("attachment-output-marker", StringComparison.Ordinal));

        var attachmentEvents = sink.Events
            .Where(runtimeEvent => runtimeEvent.Kind == NUnitRuntimeEventKinds.Attachment)
            .ToList();
        Assert.Single(attachmentEvents);
        Assert.Equal("acceptance-attachment", attachmentEvents[0].Attachment!.Name);

        var finishedIndex = sink.Events.FindIndex(runtimeEvent => runtimeEvent.Kind == NUnitRuntimeEventKinds.CaseFinished);
        var attachmentIndex = sink.Events.FindIndex(runtimeEvent => runtimeEvent.Kind == NUnitRuntimeEventKinds.Attachment);
        Assert.True(attachmentIndex >= 0);
        Assert.True(finishedIndex > attachmentIndex);
    }
}
