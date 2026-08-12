using DevTools.NUnit.Core;

namespace DevTools.NUnit.Runner.Parsing;

public sealed record RunnerCommandLine(
    string Command,
    string AssemblyPath,
    string Host,
    string Version,
    string? Filter,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds);

public static class RunnerCommandParser
{
    public const int ExitOk = 0;
    public const int ExitTestFailure = 1;
    public const int ExitCliError = 2;
    public const int ExitNoHost = 3;
    public const int ExitHostTimeout = 4;

    public const int DefaultHostTimeoutSeconds = NUnitHostTiming.DefaultHostRequestTimeoutSeconds;
    public const int DefaultHostLaunchTimeoutSeconds = NUnitHostTiming.DefaultHostLaunchTimeoutSeconds;

    public static bool TryParse(string[] args, out RunnerCommandLine? command, out string? error)
    {
        command = null;
        error = null;

        if (TryParseMetaCommand(args, out error))
            return false;

        if (!TryParseCommandAndAssembly(args, out var commandName, out var assemblyPath, out error))
            return false;

        if (!TryParseOptions(args, out var options, out error))
            return false;

        if (!TryValidateOptions(commandName!, options, out error))
            return false;

        command = new RunnerCommandLine(
            commandName!,
            Path.GetFullPath(assemblyPath!),
            options.Host!.Trim(),
            options.Version!.Trim(),
            options.Filter,
            options.HostLaunch,
            options.HostTimeoutSeconds,
            options.HostLaunchTimeoutSeconds);
        return true;
    }

    private static bool TryParseMetaCommand(string[] args, out string? error)
    {
        error = null;
        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            error = "help";
            return true;
        }

        if (args is ["--version"] or ["-v"])
        {
            error = "version";
            return true;
        }

        return false;
    }

    private static bool TryParseCommandAndAssembly(
        string[] args,
        out string? commandName,
        out string? assemblyPath,
        out string? error)
    {
        commandName = null;
        assemblyPath = null;
        error = null;

        if (args.Length == 0)
        {
            error = "Command is required.";
            return false;
        }

        commandName = args[0];
        if (commandName is not ("discover" or "run"))
        {
            error = $"Unknown command '{commandName}'.";
            return false;
        }

        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            error = "Assembly path is required.";
            return false;
        }

        assemblyPath = args[1];
        return true;
    }

    private static bool TryParseOptions(string[] args, out ParsedOptions options, out string? error)
    {
        options = new ParsedOptions();
        error = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (!TryApplyOption(args, ref i, options, out error))
                return false;
        }

        return true;
    }

    private static bool TryApplyOption(string[] args, ref int index, ParsedOptions options, out string? error)
    {
        error = null;
        var token = args[index];
        switch (token)
        {
            case "--host":
                return RequireValue(args, ref index, "--host", out options.Host, out error);
            case "--version":
                return RequireValue(args, ref index, "--version", out options.Version, out error);
            case "--filter":
                return RequireValue(args, ref index, "--filter", out options.Filter, out error);
            case "--host-launch":
                options.HostLaunch = true;
                return true;
            case "--host-timeout":
                return RequirePositiveInt(args, ref index, "--host-timeout", out options.HostTimeoutSeconds, out error);
            case "--host-launch-timeout":
                return RequirePositiveInt(
                    args,
                    ref index,
                    "--host-launch-timeout",
                    out options.HostLaunchTimeoutSeconds,
                    out error);
            case "--debug":
                error = "Host-process debugging is not supported in this experimental release.";
                return false;
            default:
                error = $"Unknown option '{token}'.";
                return false;
        }
    }

    private static bool TryValidateOptions(string commandName, ParsedOptions options, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            error = "--host is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            error = "--version is required.";
            return false;
        }

        return true;
    }

    private static bool RequireValue(
        string[] args,
        ref int index,
        string option,
        out string? value,
        out string? error)
    {
        error = null;
        if (TryReadValue(args, ref index, out value))
            return true;

        error = $"{option} requires a value.";
        return false;
    }

    private static bool RequirePositiveInt(
        string[] args,
        ref int index,
        string option,
        out int value,
        out string? error)
    {
        error = null;
        value = 0;
        if (TryReadPositiveInt(args, ref index, out value))
            return true;

        error = $"{option} requires a positive number of seconds.";
        return false;
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        value = null;
        if (index + 1 >= args.Length)
            return false;

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadPositiveInt(string[] args, ref int index, out int value)
    {
        value = 0;
        if (!TryReadValue(args, ref index, out var text))
            return false;

        return int.TryParse(text, out value) && value > 0;
    }

    private sealed class ParsedOptions
    {
        public string? Host;
        public string? Version;
        public string? Filter;
        public bool HostLaunch;
        public int HostTimeoutSeconds = DefaultHostTimeoutSeconds;
        public int HostLaunchTimeoutSeconds = DefaultHostLaunchTimeoutSeconds;
    }
}
