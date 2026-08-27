using ConsoleAppFramework;
using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Services;
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
        services.AddSingleton<ITestSession, TestSession>();
        services.AddSingleton<IExecutionCoordinator, ExecutionCoordinator>();
        services.AddSingleton<IDebuggerAttach>(VisualStudioAttach.Instance);
        await using var serviceProvider = services.BuildServiceProvider();

        ConsoleApp.ServiceProvider = serviceProvider;
        var app = ConsoleApp.Create();
        app.Add<RunnerCommands>();
        await app.RunAsync(args).ConfigureAwait(false);
        return Environment.ExitCode;
    }
}
