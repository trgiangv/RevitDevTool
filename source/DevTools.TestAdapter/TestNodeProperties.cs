using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Extensions.Messages;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.TestAdapter;

internal static class TestNodeProperties
{
    public static void AddSource(List<IProperty> properties, TestSourceLocation? source)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.File))
            return;

        var line = Math.Max(source.Line, 1);
        properties.Add(new TestFileLocationProperty(
            source.File,
            new LinePositionSpan(new LinePosition(line, 1), new LinePosition(line, 2048))));
    }

    private static void AddTraits(List<IProperty> properties, IReadOnlyList<TestTrait>? traits)
    {
        if (traits is null)
            return;

        properties.AddRange(traits.Select(trait => new TestMetadataProperty(trait.Name, trait.Value)));
    }

    private static void AddTiming(List<IProperty> properties, double durationMilliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Max(durationMilliseconds, 0));
        var end = DateTimeOffset.UtcNow;
        properties.Add(new TimingProperty(new TimingInfo(end - duration, end, duration)));
    }

    private static void AddOutput(List<IProperty> properties, TestCaseResult result)
    {
        // PassedTestNodeStateProperty has no explanation slot; surface Assert.Pass /
        // success Message on stdout so IDE and MTP terminal show it with Console.
        var output = string.Equals(result.Outcome, TestOutcomes.Passed, StringComparison.Ordinal)
            ? TestRunTraceScope.Merge(result.Output, result.Message)
            : result.Output;

        if (string.IsNullOrWhiteSpace(output))
            return;

        properties.Add(new StandardOutputProperty(output!));
    }

    private static void AddAttachments(List<IProperty> properties, IReadOnlyList<TestAttachment>? attachments)
    {
        if (attachments is null)
            return;

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.Path))
                continue;

            properties.Add(new FileArtifactProperty(
                new FileInfo(attachment.Path!),
                displayName: attachment.Description ?? Path.GetFileName(attachment.Path),
                description: attachment.Description));
        }
    }

    private static IProperty ToStateProperty(TestCaseResult result) =>
        result.Outcome switch
        {
            "Passed" => PassedTestNodeStateProperty.CachedInstance,
            "Skipped" => new SkippedTestNodeStateProperty(result.SkipReason ?? result.Message),
            "Failed" => new FailedTestNodeStateProperty(CreateException(result)),
            _ => new ErrorTestNodeStateProperty(CreateException(result)),
        };

    public static void AddCommonResultProperties(List<IProperty> properties, TestCaseResult result)
    {
        properties.Add(ToStateProperty(result));
        AddSource(properties, result.Source);
        AddTraits(properties, result.Traits);
        AddTiming(properties, result.DurationMilliseconds);
        AddOutput(properties, result);
        AddAttachments(properties, result.Attachments);
    }

    public static TestNode CreateErrorNode(string uid, string displayName, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        ArgumentNullException.ThrowIfNull(exception);

        return new TestNode
        {
            Uid = new TestNodeUid(uid),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? uid : displayName,
            Properties = new PropertyBag(new ErrorTestNodeStateProperty(exception)),
        };
    }

    private static Exception CreateException(TestCaseResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StackTrace))
            return new InvalidOperationException(result.Message ?? result.Outcome);

        return new InvalidOperationException($"{result.Message}{Environment.NewLine}{result.StackTrace}");
    }
}
