using DevTools.TestAdapter;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.TestAdapter.Tests;

[TestClass]
public sealed class TestNodePropertiesTests
{
    [TestMethod]
    public void AddCommonResultProperties_maps_passed_failed_and_skipped_states()
    {
        var passed = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(passed, CreateResult(TestOutcomes.Passed, "ok", null));
        Assert.IsTrue(passed.Any(property => property is PassedTestNodeStateProperty));

        var failed = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(
            failed,
            CreateResult(TestOutcomes.Failed, "boom", "at line 1"));
        Assert.IsTrue(failed.Any(property => property is FailedTestNodeStateProperty));

        var skipped = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(
            skipped,
            CreateResult(TestOutcomes.Skipped, "later", null));
        Assert.IsTrue(skipped.Any(property => property is SkippedTestNodeStateProperty));
    }

    [TestMethod]
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

        var skipped = Assert.ContainsSingle(properties.OfType<SkippedTestNodeStateProperty>());
        Assert.AreEqual("requires capability", skipped.Explanation);
    }

    [TestMethod]
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

        Assert.IsTrue(properties.Any(property => property is TestFileLocationProperty));
        Assert.IsTrue(properties.Any(property => property is TestMetadataProperty));
        Assert.IsTrue(properties.Any(property => property is StandardOutputProperty));
        Assert.IsTrue(properties.Any(property => property is FileArtifactProperty));
        Assert.IsTrue(properties.Any(property => property is TimingProperty));
    }

    [TestMethod]
    public void CreateErrorNode_requires_uid_and_exception()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            TestNodeProperties.CreateErrorNode(" ", "display", new InvalidOperationException()));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
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
