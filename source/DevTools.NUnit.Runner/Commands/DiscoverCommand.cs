using System.Text.Json;
using System.Xml.Linq;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Runner.Services;
using DevTools.NUnit.Transport;

namespace DevTools.NUnit.Runner.Commands;

/// <summary>
/// Local PE discovery. Must not locate, launch, or talk to an Autodesk host.
/// In-host NUnit explore happens inside <c>nunit/run</c> (<c>EnsureLoaded</c>), not this command.
/// </summary>
public static class DiscoverCommand
{
    public static async Task<int> ExecuteAsync(
        RunnerCommandLine options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(options.AssemblyPath))
        {
            await Console.Error.WriteLineAsync($"Assembly not found: {options.AssemblyPath}").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        if (!NUnitRunnerFilter.TryNormalize(options.Filter, out var filter, out var filterError))
        {
            await Console.Error.WriteLineAsync(filterError).ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        SplitNameAndTestFilter(filter, out var names, out var fullNames);
        var cases = NUnitMetadataDiscoverer.Filter(
            NUnitMetadataDiscoverer.Discover(options.AssemblyPath),
            names,
            fullNames);

        Console.WriteLine(JsonSerializer.Serialize(
            new NUnitDiscoverResponse(cases),
            NUnitJsonContext.Default.NUnitDiscoverResponse));
        return RunnerExitCode.Ok;
    }

    internal static void SplitNameAndTestFilter(
        string? filterXml,
        out IReadOnlyList<string> names,
        out IReadOnlyList<string> fullNames)
    {
        names = [];
        fullNames = [];
        if (string.IsNullOrWhiteSpace(filterXml))
            return;

        try
        {
            var root = XElement.Parse(filterXml);
            var nameValues = root.Descendants("name")
                .Select(element => element.Value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var testValues = root.Descendants("test")
                .Select(element => element.Value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            names = nameValues;
            fullNames = testValues;
        }
        catch (System.Xml.XmlException)
        {
            // Leave empty → unfiltered PE list.
        }
    }
}
