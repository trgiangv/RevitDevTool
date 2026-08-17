using Microsoft.Extensions.DependencyInjection;

namespace DevTools.TestRunner.Core.Composition;

/// <summary>Explicit command-provider contract used by the executable composition root.</summary>
public interface IRunnerCommandModule
{
    string FrameworkId { get; }
    void RegisterServices(IServiceCollection services);
    Task<int> RunAsync(string[] args, IServiceProvider services);
}
