using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Hosting.Acad;

public static class AcadLaunchServiceCollectionExtensions
{
    public static IServiceCollection AddAcadLaunch(this IServiceCollection services)
    {
        services.AddHostLaunchCore();
        services.AddSingleton<IHostPathResolver, AcadPathResolver>();
        services.AddSingleton<IHostArgumentBuilder, AcadArgumentBuilder>();
        services.AddSingleton<IHostStartupDialogSpec, AcadStartupDialogSpec>();
        return services;
    }
}
