using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Hosting.Acad;

public static class AcadLaunchServiceCollectionExtensions
{
    public static IServiceCollection AddAutocadFamilyLaunch(this IServiceCollection services)
    {
        services.AddSingleton<IHostPathResolver, AcadPathResolver>();
        services.AddSingleton<IHostArgumentBuilder, AcadArgumentBuilder>();
        services.AddSingleton<IHostStartupDialogStrategy, AcadStartupDialogStrategy>();
        return services;
    }
}
