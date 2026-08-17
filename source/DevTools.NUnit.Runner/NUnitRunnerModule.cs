using ConsoleAppFramework;
using DevTools.NUnit.Runner.Commands;
using DevTools.TestRunner.Core.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.NUnit.Runner;

/// <summary>Explicit NUnit command-provider registration for the TestRunner executable.</summary>
public sealed class NUnitRunnerModule : IRunnerCommandModule
{
    public const string FrameworkIdentifier = "nunit";

    public string FrameworkId => FrameworkIdentifier;

    public void RegisterServices(IServiceCollection services)
    {
        // Commands depend only on the core host-session abstraction.
    }

    public async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        ConsoleApp.ServiceProvider = services;
        var app = ConsoleApp.Create();
        app.Add<NUnitRunnerCommands>();
        await app.RunAsync(args).ConfigureAwait(false);
        return Environment.ExitCode;
    }
}
