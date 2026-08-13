namespace DevTools.NUnit.Core.Contracts;

/// <summary>
/// Shared DevTools.NUnit.Runner CLI tokens and argument layout.
/// MTP, Runner, and the VSTest adapter must use this instead of duplicating flags.
/// </summary>
public static class NUnitRunnerCli
{
    public const string DiscoverCommand = "discover";
    public const string RunCommand = "run";

    public const string HostOption = "--host";
    public const string VersionOption = "--version";
    public const string NameOption = "--name";
    public const string TestOption = "--test";
    public const string FilterOption = "--filter";
    public const string HostLaunchOption = "--host-launch";
    public const string HostTimeoutOption = "--host-timeout";
    public const string HostLaunchTimeoutOption = "--host-launch-timeout";

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
        string? filterXml = null)
    {
        var args = new List<string>
        {
            command,
            assemblyPath,
            HostOption,
            host,
            VersionOption,
            hostVersion,
            HostTimeoutOption,
            hostTimeoutSeconds.ToString(),
            HostLaunchTimeoutOption,
            hostLaunchTimeoutSeconds.ToString(),
        };

        if (hostLaunch)
            args.Add(HostLaunchOption);

        AppendRepeatable(args, NameOption, names);
        AppendRepeatable(args, TestOption, tests);

        if (!string.IsNullOrWhiteSpace(filterXml))
        {
            args.Add(FilterOption);
            args.Add(filterXml!.Trim());
        }

        return args;
    }

    private static void AppendRepeatable(List<string> args, string option, IEnumerable<string>? values)
    {
        if (values is null)
            return;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            args.Add(option);
            args.Add(value.Trim());
        }
    }
}
