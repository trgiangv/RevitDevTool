using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Hosting.Revit;

public static class RevitLaunchServiceCollectionExtensions
{
    public static IServiceCollection AddRevitLaunch(
        this IServiceCollection services,
        Func<string, string?>? readDocumentYear = null)
    {
        services.AddHostLaunchCore();
        services.AddSingleton<IHostPathResolver, RevitPathResolver>();
        services.AddSingleton<IHostArgumentBuilder, RevitArgumentBuilder>();
        services.AddSingleton<IHostStartupDialogStrategy, RevitStartupDialogStrategy>();
        services.AddSingleton<IHostLaunchService>(sp =>
        {
            var engine = sp.GetRequiredService<HostLaunchService>();
            var resolver = HostLaunchSupport.FindSingle(
                sp.GetServices<IHostPathResolver>(),
                HostApp.Revit,
                r => r.Supports(HostApp.Revit))
                ?? throw new InvalidOperationException("Revit path resolver was not registered.");
            return new RevitFileAwareHostLaunchService(engine, resolver, readDocumentYear);
        });
        return services;
    }
}
