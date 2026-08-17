using System.Text.Json;

namespace DevTools.NUnit.Core.Contracts;

/// <summary>
/// Shared DevTools.TestRunner CLI tokens and argument layout.
/// MTP, Runner, and the VSTest adapter must use this instead of duplicating flags.
/// </summary>
public static class NUnitRunnerCli
{
    public const string DiscoverCommand = "discover";
    public const string RunCommand = "run";

    public const string HostOption = "--host";
    public const string HostVersionOption = "--host-version";
    public const string NameOption = "--name";
    public const string TestOption = "--test";
    public const string FilterOption = "--filter";
    public const string HostLaunchOption = "--host-launch";
    public const string HostTimeoutOption = "--host-timeout";
    public const string HostLaunchTimeoutOption = "--host-launch-timeout";
    public const string DebugOption = "--debug";
    public const string DebugParentPidOption = "--debug-parent-pid";

    public static List<string> BuildArguments(
        string command,
        string assemblyPath,
        string host,
        string hostVersion,
        int hostTimeoutSeconds,
        int hostLaunchTimeoutSeconds,
        bool hostLaunch,
        IEnumerable<string>? names = null,
        IEnumerable<string>? tests = null,
        string? filterXml = null,
        int? debugParentPid = null)
    {
        var args = new List<string>
        {
            command,
            assemblyPath,
            HostOption,
            host,
            HostVersionOption,
            hostVersion,
            HostTimeoutOption,
            hostTimeoutSeconds.ToString(),
            HostLaunchTimeoutOption,
            hostLaunchTimeoutSeconds.ToString(),
        };

        if (hostLaunch)
            args.Add(HostLaunchOption);

        AppendJsonArray(args, NameOption, names);
        AppendJsonArray(args, TestOption, tests);

        if (!string.IsNullOrWhiteSpace(filterXml))
        {
            args.Add(FilterOption);
            args.Add(filterXml!.Trim());
        }

        if (debugParentPid is > 0)
        {
            args.Add(DebugParentPidOption);
            args.Add(debugParentPid.Value.ToString());
        }

        return args;
    }

    private static void AppendJsonArray(List<string> args, string option, IEnumerable<string>? values)
    {
        if (values is null)
            return;

        var cleaned = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        if (cleaned.Count == 0)
            return;

        args.Add(option);
        args.Add(JsonSerializer.Serialize(cleaned));
    }
}
