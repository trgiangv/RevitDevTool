using DevTools.NUnit.Runner.Services;
using DevTools.TestRunner.Core.Parsing;

namespace DevTools.NUnit.Runner.Parsing;

public sealed record RunnerCommandLine(RunnerCommandContext Context, string? Filter)
{
    public string Command => Context.Command;
    public string AssemblyPath => Context.AssemblyPath;
    public string Host => Context.Host;
    public string Version => Context.Version;
    public bool HostLaunch => Context.HostLaunch;
    public int HostTimeoutSeconds => Context.HostTimeoutSeconds;
    public int HostLaunchTimeoutSeconds => Context.HostLaunchTimeoutSeconds;
    public bool Debug => Context.Debug;
    public int? DebugParentPid => Context.DebugParentPid;
    public string FrameworkId => Context.FrameworkId;

    public static bool TryCreate(
        RunnerCommandContext context,
        IReadOnlyList<string>? names,
        IReadOnlyList<string>? tests,
        string? filterXml,
        out RunnerCommandLine? options,
        out string? error)
    {
        options = null;
        error = null;

        if (!NUnitRunnerFilter.TryCompose(names, tests, filterXml, out var filter, out error))
            return false;

        options = new RunnerCommandLine(context, filter);
        return true;
    }
}
