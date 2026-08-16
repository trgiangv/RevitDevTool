using DevTools.Utilities.AssemblyLoading;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Hosting.Acad;

public static class AcadInProcessServiceCollectionExtensions
{
    public static IServiceCollection AddAutocadInProcess(this IServiceCollection services)
    {
        var policy = new AcadSharedAssemblyPolicy();
        services.AddSingleton<IHostSharedAssemblyPolicy>(policy);
        HostSharedAssemblies.Use(policy);
        return services;
    }
}
