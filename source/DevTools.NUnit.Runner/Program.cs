using DevTools.NUnit.Runner.Commands;
using DevTools.NUnit.Runner.Parsing;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
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
            NUnitRunnerCli.DiscoverCommand => await DiscoverCommand.ExecuteAsync(command, hostSession).ConfigureAwait(false),
            NUnitRunnerCli.RunCommand => await RunCommand.ExecuteAsync(command, hostSession).ConfigureAwait(false),
            _ => RunnerCommandParser.ExitCliError
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            $"""
            DevTools.NUnit.Runner — host NUnit test controller

            Usage:
              DevTools.NUnit.Runner {NUnitRunnerCli.DiscoverCommand} <assembly> {NUnitRunnerCli.HostOption} <Revit|AutoCAD> {NUnitRunnerCli.VersionOption} <year> [{NUnitRunnerCli.NameOption} <method>] [{NUnitRunnerCli.TestOption} <fullname>]
              DevTools.NUnit.Runner {NUnitRunnerCli.RunCommand} <assembly> {NUnitRunnerCli.HostOption} <Revit|AutoCAD> {NUnitRunnerCli.VersionOption} <year> [{NUnitRunnerCli.NameOption} <method>] [{NUnitRunnerCli.TestOption} <fullname>]

            Options:
              {NUnitRunnerCli.NameOption} <method>              Repeatable NUnit test Name (method)
              {NUnitRunnerCli.TestOption} <fullname>            Repeatable NUnit FullName
              {NUnitRunnerCli.FilterOption} <xml>               Raw NUnit TestFilter XML (do not mix with {NUnitRunnerCli.NameOption}/{NUnitRunnerCli.TestOption})
              {NUnitRunnerCli.HostLaunchOption}                Always launch a new host (skip reusing existing instances)
              {NUnitRunnerCli.HostTimeoutOption} <seconds>     Pipe request timeout for discover/run (default {NUnitHostTiming.DefaultHostRequestTimeoutSeconds})
              {NUnitRunnerCli.HostLaunchTimeoutOption} <seconds>  Wait for host pipe after launch (default {NUnitHostTiming.DefaultHostLaunchTimeoutSeconds})
              {NUnitRunnerCli.VersionOption}, -v                Show version
              --help, -h                   Show this help

            Install location:
            """ + AppUtils.GetBundleContentsPath());
    }
}
