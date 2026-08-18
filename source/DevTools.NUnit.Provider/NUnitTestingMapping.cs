using System.Xml.Linq;
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

}
