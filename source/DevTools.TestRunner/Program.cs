using ConsoleAppFramework;
using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.TestRunner.Commands;
using DevTools.TestRunner.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.TestRunner;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddHostLaunchCore();
        services.AddRevitLaunch(readDocumentYear: null);
        services.AddAutocadFamilyLaunch();
        services.AddSingleton<HostSession>();
        ConsoleApp.ServiceProvider = services.BuildServiceProvider();

        var app = ConsoleApp.Create();
        app.Add<TestRunnerCommands>();
        await app.RunAsync(args).ConfigureAwait(false);
        return Environment.ExitCode;
    }
}
