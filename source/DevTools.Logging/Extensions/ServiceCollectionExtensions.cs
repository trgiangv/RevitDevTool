using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevToolsFileLogging(this IServiceCollection services)
    {
        services.TryAddSingleton<FileLoggerProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>(
            sp => sp.GetRequiredService<FileLoggerProvider>()));
        return services;
    }
}
