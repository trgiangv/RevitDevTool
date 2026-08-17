using System.Xml.Linq;
using DevTools.NUnit.Transport.Contracts;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Provider;

public static class NUnitTestingMapping
{
    public static TestingHostOptions ToHostOptions(HostRunOptions options) =>
        new(
            options.Host,
            options.HostVersion,
            options.HostLaunch,
            options.HostTimeoutSeconds,
            options.HostLaunchTimeoutSeconds,
            options.RunnerPath,
            options.DebugParentPid);

    public static HostRunOptions ToHostRunOptions(TestingHostOptions options) =>
        new(
            options.Host,
            options.HostVersion,
            options.HostLaunch,
            options.HostTimeoutSeconds,
            options.HostLaunchTimeoutSeconds,
            options.RunnerPath,
            options.DebugParentPid);

    public static TestingSelection ToSelection(RunnerTestFilter filter)
    {
        if (filter.FullNames.Count > 0 && filter.Names.Count == 0)
            return new TestingSelection(filter.FullNames, null);

        if (filter.IsEmpty)
            return new TestingSelection([], null);

        var nodes = filter.Names.Select(name => new XElement("name", name))
            .Concat(filter.FullNames.Select(test => new XElement("test", test)))
            .ToList();
        var inner = nodes.Count == 1 ? nodes[0] : new XElement("or", nodes);
        return new TestingSelection([], new XElement("filter", inner).ToString(SaveOptions.DisableFormatting));
    }

    public static RunnerTestFilter ToRunnerFilter(TestingSelection selection)
    {
        if (selection.TestIds.Count > 0)
            return RunnerTestFilter.FromFullNames(selection.TestIds);

        var payload = selection.ProviderPayload;
        if (string.IsNullOrWhiteSpace(payload))
            return RunnerTestFilter.Empty;

        var xml = XElement.Parse(payload);
        var names = xml.Descendants("name")
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        var tests = xml.Descendants("test")
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        return new RunnerTestFilter(names, tests);
    }

    public static TestingCaseResult ToTesting(NUnitCaseResult result) =>
        new(
            result.Id,
            result.Name,
            result.Outcome,
            result.DurationMs,
            result.Message,
            result.StackTrace,
            result.Output,
            result.Source is null ? null : new TestingSourceLocation(result.Source.File, result.Source.Line),
            (result.Traits ?? Array.Empty<NUnitTrait>())
                .Select(trait => new TestingTrait(trait.Name, trait.Value))
                .ToList(),
            (result.Attachments ?? Array.Empty<NUnitAttachment>())
                .Select(attachment => new TestingAttachment(
                    attachment.Path,
                    attachment.Name,
                    attachment.ContentType,
                    attachment.Base64))
                .ToList(),
            result.ParentTestId,
            result.FullName,
            result.SkipReason);
}
