using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public static class TestingRunnerCli
{
    public const string RunCommand = "run";
    public const string FrameworkOption = "--framework";
    public const string HostOption = "--host";
    public const string HostVersionOption = "--host-version";
    public const string ForceLaunchOption = "--force-launch";
    public const string PerTestTimeoutOption = "--per-test-timeout";
    public const string LaunchTimeoutOption = "--launch-timeout";
    public const string DebugParentPidOption = "--debug-parent-pid";
    public const string TestOption = "--test";
    public const string NameOption = "--name";
    public const string FilterOption = "--filter";

    /// <summary>
    /// Builds TestRunner CLI args. <c>PerTestTimeout</c> / <c>LaunchTimeout</c>
    /// from the consumer csproj are forwarded as <c>--per-test-timeout</c> /
    /// <c>--launch-timeout</c> for the in-host pipe. <c>PerTestTimeout</c> is
    /// per test; the adapter scales it by the run's test count before this
    /// method sees <see cref="TestingHostOptions.PerTestTimeoutSeconds"/>.
    /// The adapter <c>WaitForExit</c> budget is computed separately by
    /// <see cref="TestingHostTiming"/>.
    /// </summary>
    public static List<string> BuildRunArguments(
        TestingRunRequest request,
        TestingHostOptions hostOptions)
    {
        var args = new List<string>
        {
            RunCommand,
            FrameworkOption,
            request.FrameworkId,
            request.Assembly.Path,
            HostOption,
            hostOptions.HostName,
            HostVersionOption,
            hostOptions.HostVersion,
            PerTestTimeoutOption,
            hostOptions.PerTestTimeoutSeconds.ToString(),
            LaunchTimeoutOption,
            hostOptions.LaunchTimeoutSeconds.ToString(),
        };

        if (hostOptions.ForceLaunch)
            args.Add(ForceLaunchOption);

        if (hostOptions.DebugParentPid is > 0)
        {
            args.Add(DebugParentPidOption);
            args.Add(hostOptions.DebugParentPid.Value.ToString());
        }

        var selection = request.Selection;
        var testIds = (selection?.TestIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (testIds.Count > 0)
        {
            args.Add(TestOption);
            args.Add(System.Text.Json.JsonSerializer.Serialize(testIds));
        }

        var names = (selection?.Names ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (names.Count > 0)
        {
            args.Add(NameOption);
            args.Add(System.Text.Json.JsonSerializer.Serialize(names));
        }

        var payload = selection?.ProviderPayload;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            args.Add(FilterOption);
            args.Add(payload!.Trim());
        }

        return args;
    }
}
