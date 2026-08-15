using ConsoleAppFramework;

namespace DevTools.NUnit.Runner;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var app = ConsoleApp.Create();
        app.Add<Commands.NUnitRunnerCommands>();
        await app.RunAsync(args).ConfigureAwait(false);
        return Environment.ExitCode;
    }
}
