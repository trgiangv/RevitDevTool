using DevTools.Utilities.AssemblyLoading;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Hosting.Revit;

public static class RevitInProcessServiceCollectionExtensions
{
    public static IServiceCollection AddRevitInProcess(this IServiceCollection services)
    {
        var policy = new RevitSharedAssemblyPolicy();
        services.AddSingleton<IHostSharedAssemblyPolicy>(policy);
        HostSharedAssemblies.Use(policy);
        return services;
    }
}
