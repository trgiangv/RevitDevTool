using DevTools.NUnit.Runner.Commands;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Core;
using DevTools.NUnit.Runner.Services;
using DevTools.Utilities;
using DevTools.Utilities.Hosting;

namespace DevTools.NUnit.Runner;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!RunnerCommandParser.TryParse(args, out var command, out var error))
        {
            switch (error)
            {
                case "help":
                    PrintHelp();
                    return RunnerCommandParser.ExitOk;
                case "version":
                    Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0");
                    return RunnerCommandParser.ExitOk;
            }

            await Console.Error.WriteLineAsync(error ?? "Invalid command line.").ConfigureAwait(false);
            PrintHelp();
            return RunnerCommandParser.ExitCliError;
        }

        var hostSession = new HostSession(new HostLaunchService());
        return command!.Command switch
        {
            "discover" => await DiscoverCommand.ExecuteAsync(command, hostSession).ConfigureAwait(false),
            "run" => await RunCommand.ExecuteAsync(command, hostSession).ConfigureAwait(false),
            _ => RunnerCommandParser.ExitCliError
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            $"""
            DevTools.NUnit.Runner — host NUnit test controller

            Usage:
              DevTools.NUnit.Runner discover <assembly> --host <Revit|AutoCAD> --version <year> [--filter <nunit-framework-filter-xml>]
              DevTools.NUnit.Runner run <assembly> --host <Revit|AutoCAD> --version <year> [--filter <nunit-framework-filter-xml>]

            Options:
              --host-launch                Always launch a new host (skip reusing existing instances)
              --host-timeout <seconds>     Pipe request timeout for discover/run (default {NUnitHostTiming.DefaultHostRequestTimeoutSeconds})
              --host-launch-timeout <seconds>  Wait for host pipe after launch (default {NUnitHostTiming.DefaultHostLaunchTimeoutSeconds})
              --version, -v                Show version
              --help, -h                   Show this help

            Note:
              Host-process debugging (--debug) is deferred in this experimental release.

            Install location:
            """ + AppUtils.GetBundleContentsPath());
    }
}
