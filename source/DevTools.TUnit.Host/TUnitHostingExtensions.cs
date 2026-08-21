using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host.Loading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.TUnit.Host;

public static class TUnitHostingExtensions
{
    public static IServiceCollection AddTUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TUnitGenerationPolicy>(_ =>
            new TUnitGenerationPolicy(ResolveHostRuntimeSource));
        services.TryAddSingleton<TUnitRuntimeSessionFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostTestFrameworkProvider, TUnitHostTestFrameworkProvider>());
        return services;
    }

    private static TUnitRuntimeSource ResolveHostRuntimeSource()
    {
        var hostDirectory = Path.GetDirectoryName(typeof(TUnitHostingExtensions).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var runtimeDirectory = Path.Combine(hostDirectory, "TUnitRuntime");
        var runtimeAssembly = Path.Combine(runtimeDirectory, TUnitGenerationPolicy.RuntimeAssemblyFileName);
        if (!File.Exists(runtimeAssembly))
        {
            throw new InvalidOperationException(
                $"TUnit runtime assembly not found beside the host at '{runtimeAssembly}'.");
        }

        return new TUnitRuntimeSource(
            runtimeAssembly,
            Directory.EnumerateFiles(runtimeDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(path, runtimeAssembly, StringComparison.OrdinalIgnoreCase))
                .ToList());
    }
}
