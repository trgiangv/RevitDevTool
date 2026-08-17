using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.NUnit.Runner;
using DevTools.TestRunner.Core.Composition;
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
        services.AddSingleton<IHostSession, HostSession>();
        services.AddSingleton<IHostExecutionCoordinator, HostExecutionCoordinator>();
        services.AddSingleton<IVisualStudioAttach>(VisualStudioAttach.Instance);
        var modules = new RunnerModuleRegistry();
        modules.Register(new NUnitRunnerModule(), isDefault: true);
        services.AddSingleton(modules);
        modules.RegisterServices(services);
        var serviceProvider = services.BuildServiceProvider();

        return await modules.RunAsync(args, serviceProvider).ConfigureAwait(false);
    }
}
