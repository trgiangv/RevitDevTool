using DevTools.NUnit.Host.Loading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.NUnit.Host;

public static class NUnitHostingExtensions
{
    /// <summary>
    /// Registers the native NUnit runtime manager, generation builder, and bridge handler.
    /// Call from Revit/AutoCAD hosting after <c>AddExecutionServices()</c>.
    /// </summary>
    public static IServiceCollection AddNUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<INUnitGenerationBuilder>(_ =>
            new NUnitGenerationBuilder(ResolveHostRuntimeSourcePath));
#if NETFRAMEWORK
        services.TryAddSingleton<INUnitRuntimeSessionFactory, NetfxNUnitRuntimeSessionFactory>();
#else
        services.TryAddSingleton<INUnitRuntimeSessionFactory, NUnitRuntimeSessionFactory>();
#endif
        services.TryAddSingleton<NUnitRuntimeManager>();
        services.TryAddSingleton<INUnitHost, NUnitHost>();
        services.AddSingleton<IBridgeRequestHandler, NUnitRequestHandler>();
        return services;
    }

    private static NUnitRuntimeSource ResolveHostRuntimeSourcePath()
    {
        var hostDirectory = Path.GetDirectoryName(typeof(NUnitHostingExtensions).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var runtimeDirectory = Path.Combine(hostDirectory, "NUnitRuntime");

        var assemblyPath = Path.Combine(runtimeDirectory, NUnitGenerationBuilder.RuntimeAssemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidOperationException(
                $"NUnit runtime assembly not found beside the host at '{assemblyPath}'. " +
                "Deploy DevTools.NUnit.Runtime.dll with the host add-in.");
        }

        var symbolPath = Path.Combine(runtimeDirectory, NUnitGenerationBuilder.RuntimeSymbolFileName);
        var dependencies = Directory.Exists(runtimeDirectory)
            ? Directory.EnumerateFiles(runtimeDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(path, assemblyPath, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : new List<string>();

        return new NUnitRuntimeSource(
            assemblyPath,
            File.Exists(symbolPath) ? symbolPath : null,
            dependencies);
    }
}
