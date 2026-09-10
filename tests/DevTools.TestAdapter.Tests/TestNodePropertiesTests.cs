using DevTools.TestAdapter;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.TestAdapter.Tests;

public sealed class TestNodePropertiesTests
{
    [Fact]
    public void AddCommonResultProperties_maps_passed_failed_and_skipped_states()
    {
        var passed = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(passed, CreateResult(TestOutcomes.Passed, "ok", null));
        Assert.Contains(passed, property => property is PassedTestNodeStateProperty);

        var failed = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(
            failed,
            CreateResult(TestOutcomes.Failed, "boom", "at line 1"));
        Assert.Contains(failed, property => property is FailedTestNodeStateProperty);

        var skipped = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(
            skipped,
            CreateResult(TestOutcomes.Skipped, "later", null));
        Assert.Contains(skipped, property => property is SkippedTestNodeStateProperty);
    }

    [Fact]
    public void Skipped_state_prefers_skip_reason_over_message()
    {
        var properties = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(
            properties,
            new TestCaseResult(
                "case-1",
                "Display",
                TestOutcomes.Skipped,
                0,
                Message: "message",
                StackTrace: null,
                Output: null,
                Source: null,
                Traits: [],
                Attachments: [],
                SkipReason: "requires capability"));

        var skipped = Assert.Single(properties.OfType<SkippedTestNodeStateProperty>());
        Assert.Equal("requires capability", skipped.Explanation);
    }

    [Fact]
    public void AddCommonResultProperties_adds_source_traits_output_and_attachments()
    {
        var properties = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(
            properties,
            new TestCaseResult(
                "case-1",
                "Display",
                TestOutcomes.Passed,
                12.5,
                null,
                null,
                "console",
                new TestSourceLocation("Fixture.cs", 0),
                [new TestTrait("Category", "Smoke")],
                [new TestAttachment(@"C:\temp\trace.txt", "trace")]));

        Assert.Contains(properties, property => property is TestFileLocationProperty);
        Assert.Contains(properties, property => property is TestMetadataProperty);
        Assert.Contains(properties, property => property is StandardOutputProperty);
        Assert.Contains(properties, property => property is FileArtifactProperty);
        Assert.Contains(properties, property => property is TimingProperty);
    }

    [Fact]
    public void CreateErrorNode_requires_uid_and_exception()
    {
        Assert.Throws<ArgumentException>(() =>
            TestNodeProperties.CreateErrorNode(" ", "display", new InvalidOperationException()));
        Assert.Throws<ArgumentNullException>(() =>
            TestNodeProperties.CreateErrorNode("uid", "display", null!));
    }

    static TestCaseResult CreateResult(string outcome, string? message, string? stackTrace) =>
        new(
            "case-1",
            "Display",
            outcome,
            1,
            message,
            stackTrace,
            null,
            null,
            [],
            []);
}
