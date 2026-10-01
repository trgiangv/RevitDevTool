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

    internal static TestNode ToDiscoveredNode(TestDiscoveredTest test, string? assemblyPath = null)
    {
        var properties = new List<IProperty> { DiscoveredTestNodeStateProperty.CachedInstance };
        AddMethodIdentifier(properties, test, assemblyPath);
        TestNodeProperties.AddSource(properties, test.Source);
        return new TestNode
        {
            Uid = new TestNodeUid(OpaqueUid(test.TestId, test.FullName, test.DisplayName)),
            DisplayName = test.DisplayName,
            Properties = new PropertyBag(properties),
        };
    }

    internal static TestNode ToResultNode(
        TestCaseResult result,
        string? assemblyPath = null,
        IReadOnlyList<TestDiscoveredTest>? discovered = null)
    {
        var properties = new List<IProperty>();
        TestNodeProperties.AddCommonResultProperties(properties, result);
        AddMethodIdentifier(properties, FindDiscovered(discovered, result), assemblyPath);

        return new TestNode
        {
            Uid = new TestNodeUid(OpaqueUid(result.TestId, result.FullName, result.DisplayName)),
            DisplayName = result.DisplayName,
            Properties = new PropertyBag(properties),
        };
    }

    private static TestDiscoveredTest? FindDiscovered(
        IReadOnlyList<TestDiscoveredTest>? discovered,
        TestCaseResult result)
    {
        if (discovered is null || discovered.Count == 0)
            return null;

        return discovered.FirstOrDefault(test =>
            string.Equals(test.TestId, result.TestId, StringComparison.Ordinal));
    }

    private static string OpaqueUid(string id, string? fullName, string name)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return id;
        if (!string.IsNullOrWhiteSpace(fullName))
            return fullName!;
        return name;
    }

    private static void AddMethodIdentifier(
        List<IProperty> properties,
        TestDiscoveredTest? test,
        string? assemblyPath)
    {
        if (test is null)
            return;
        if (string.IsNullOrWhiteSpace(test.TypeName) || string.IsNullOrWhiteSpace(test.MethodName))
            return;

        properties.Add(new TestMethodIdentifierProperty(
            ResolveAssemblyFullName(assemblyPath),
            test.Namespace ?? string.Empty,
            test.TypeName!,
            test.MethodName!,
            test.MethodArity,
            ToParameterTypes(test.ParameterTypeFullNames),
            string.IsNullOrWhiteSpace(test.ReturnTypeFullName) ? "System.Void" : test.ReturnTypeFullName!));
    }

    private static string[] ToParameterTypes(IReadOnlyList<string>? types)
    {
        if (types is null || types.Count == 0)
            return [];
        if (types is string[] array)
            return array;
        return types.ToArray();
    }

    private static string ResolveAssemblyFullName(string? assemblyPath)
    {
        if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
        {
            try
            {
                return System.Reflection.AssemblyName.GetAssemblyName(assemblyPath!).FullName;
            }
            catch
            {
                // Fall through to the file name.
            }
        }

        if (!string.IsNullOrWhiteSpace(assemblyPath))
            return Path.GetFileNameWithoutExtension(assemblyPath);

        return System.Reflection.Assembly.GetEntryAssembly()?.GetName().FullName ?? string.Empty;
    }
}
