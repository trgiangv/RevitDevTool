using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.TestAdapter;

internal static class TestNodeProperties
{
    public static void AddSource(List<IProperty> properties, TestingSourceLocation? source)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.File))
            return;

        var line = Math.Max(source.Line, 1);
        properties.Add(new TestFileLocationProperty(
            source.File,
            new LinePositionSpan(new LinePosition(line, 1), new LinePosition(line, 1))));
    }

    public static void AddTraits(List<IProperty> properties, IReadOnlyList<TestingTrait>? traits)
    {
        if (traits is null)
            return;

        foreach (var trait in traits)
            properties.Add(new TestMetadataProperty(trait.Name, trait.Value));
    }

    public static void AddTiming(List<IProperty> properties, double durationMilliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Max(durationMilliseconds, 0));
        var end = DateTimeOffset.UtcNow;
        properties.Add(new TimingProperty(new TimingInfo(end - duration, end, duration)));
    }

    public static void AddOutput(List<IProperty> properties, string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        properties.Add(new StandardOutputProperty(output!));
    }

    public static void AddAttachments(List<IProperty> properties, IReadOnlyList<TestingAttachment>? attachments)
    {
        if (attachments is null)
            return;

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.Path))
                continue;

            properties.Add(new FileArtifactProperty(
                new FileInfo(attachment.Path),
                displayName: attachment.Description ?? Path.GetFileName(attachment.Path),
                description: attachment.Description));
        }
    }

    public static IProperty ToStateProperty(TestingCaseResult result) =>
        result.Outcome switch
        {
            "Passed" => PassedTestNodeStateProperty.CachedInstance,
            "Skipped" => new SkippedTestNodeStateProperty(result.Message),
            "Failed" => new FailedTestNodeStateProperty(CreateException(result)),
            _ => new ErrorTestNodeStateProperty(CreateException(result)),
        };

    public static void AddCommonResultProperties(List<IProperty> properties, TestingCaseResult result)
    {
        properties.Add(ToStateProperty(result));
        AddSource(properties, result.Source);
        AddTraits(properties, result.Traits);
        AddTiming(properties, result.DurationMilliseconds);
        AddOutput(properties, result.Output);
        AddAttachments(properties, result.Attachments);
    }

    public static TestNode CreateErrorNode(string uid, string displayName, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(uid))
            throw new ArgumentException("Error node uid is required.", nameof(uid));
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));

        return new TestNode
        {
            Uid = new TestNodeUid(uid),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? uid : displayName,
            Properties = new PropertyBag(new ErrorTestNodeStateProperty(exception)),
        };
    }

    static Exception CreateException(TestingCaseResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StackTrace))
            return new InvalidOperationException(result.Message ?? result.Outcome);

        return new InvalidOperationException($"{result.Message}{Environment.NewLine}{result.StackTrace}");
    }
}
