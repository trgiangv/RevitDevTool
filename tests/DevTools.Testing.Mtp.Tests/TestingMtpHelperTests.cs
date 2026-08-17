using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Mtp;
using DevTools.Testing.Transport;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.Testing.Mtp.Tests;

public sealed class TestingMtpHelperTests
{
    [Fact]
    public void CreateErrorNode_sets_error_state()
    {
        var node = TestingMtpSession.CreateErrorNode("uid", "display", new InvalidOperationException("boom"));
        Assert.Equal("uid", node.Uid.Value);
        Assert.Equal("display", node.DisplayName);
        Assert.NotNull(node.Properties.SingleOrDefault<ErrorTestNodeStateProperty>());
    }

    [Fact]
    public void Runner_session_forwards_framework_id()
    {
        var transport = new RecordingTransport();
        using var session = new TestingRunnerSession(transport);
        var request = new TestingRunRequest(
            TestingProtocol.CurrentVersion,
            Guid.NewGuid(),
            TestingFrameworkIds.NUnit,
            new TestingAssemblyReference(@"C:\tests\a.dll", null, null),
            new TestingSelection(["id-1"], null),
            new Dictionary<string, string>());

        session.Run(request, new TestingHostOptions("Revit", "2025", false, 60, 180, @"C:\Runner.exe"));

        Assert.Equal(TestingFrameworkIds.NUnit, transport.LastRequest!.FrameworkId);
        Assert.Equal(["id-1"], transport.LastRequest.Selection.TestIds.ToArray());
    }

    [Fact]
    public void Node_properties_map_source_output_and_traits()
    {
        var result = new TestingCaseResult(
            "id",
            "Case",
            "Passed",
            12,
            null,
            null,
            "stdout-marker",
            new TestingSourceLocation("Case.cs", 8),
            [new TestingTrait("Category", "A")],
            []);
        var properties = new List<IProperty>();
        TestingNodeProperties.AddCommonResultProperties(properties, result);

        Assert.Contains(properties, property => property is PassedTestNodeStateProperty);
        Assert.Contains(properties, property => property is StandardOutputProperty);
        Assert.Contains(properties, property => property is TestFileLocationProperty);
        Assert.Contains(properties, property => property is TestMetadataProperty);
        Assert.Contains(properties, property => property is TimingProperty);
    }

    sealed class RecordingTransport : ITestRunnerTransport
    {
        public TestingRunRequest? LastRequest { get; private set; }

        public TestingRunResponse Run(
            TestingRunRequest request,
            TestingHostOptions hostOptions,
            Action<TestingCaseResult> onResult)
        {
            LastRequest = request;
            return new TestingRunResponse(
                request.RunId,
                request.FrameworkId,
                null,
                [],
                TestingCancellationState.None,
                null,
                null);
        }

        public void Cancel(Guid runId)
        {
        }

        public void Dispose()
        {
        }
    }
}
