using DevTools.Testing.Abstractions.Contracts;
using DevTools.TestRunner.Core.Parsing;

namespace DevTools.TestRunner;

internal sealed record RunnerCommandLine(
    RunnerCommandContext Context,
    TestingSelection Selection)
{
    public string AssemblyPath => Context.AssemblyPath;
    public string FrameworkId => Context.FrameworkId;

    internal const string MixedFilterMessage = "Specify --name/--test or --filter, not both.";

    public static bool TryCreate(
        RunnerCommandContext context,
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? tests,
        string? filter,
        out RunnerCommandLine? options,
        out string? error)
    {
        options = null;
        error = null;

        var cleanedNames = Clean(names);
        var cleanedTests = Clean(tests);
        var payload = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();
        if ((cleanedNames.Count > 0 || cleanedTests.Count > 0) && payload is not null)
        {
            error = MixedFilterMessage;
            return false;
        }

        var selection = payload is not null
            ? new TestingSelection([], payload)
            : new TestingSelection(
                cleanedTests,
                ProviderPayload: null,
                Names: cleanedNames.Count == 0 ? null : cleanedNames);
        options = new RunnerCommandLine(context, selection);
        return true;
    }

    private static IReadOnlyList<string> Clean(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
