using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.NUnit.Host;

public static class NUnitHostingExtensions
{
    /// <summary>
    /// Registers the native NUnit runtime manager, generation builder, NUnit
    /// provider, and neutral <c>testing/*</c> bridge handler. Call from Revit/AutoCAD
    /// hosting after <c>AddExecutionServices()</c>.
    /// </summary>
    /// <remarks>
    /// <c>testing/*</c> is registered separately by <see cref="AddGenericTestingHostServices"/>.
    /// </remarks>
    public static IServiceCollection AddNUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<NUnitGenerationBuilder>(_ =>
            new NUnitGenerationBuilder(ResolveHostRuntimeSourcePath));
        services.TryAddSingleton<INUnitGenerationBuilder>(sp => sp.GetRequiredService<NUnitGenerationBuilder>());
#if NETFRAMEWORK
        services.TryAddSingleton<ITestingRuntimeSessionFactory, NetfxNUnitRuntimeSessionFactory>();
#else
        services.TryAddSingleton<ITestingRuntimeSessionFactory, NUnitRuntimeSessionFactory>();
#endif
        services.TryAddSingleton(sp => sp.GetRequiredService<NUnitGenerationBuilder>().Store);
        services.TryAddSingleton(sp => sp.GetRequiredService<NUnitGenerationBuilder>().Policy);
        services.TryAddSingleton<TestingRuntimeSessionManager>();
        services.TryAddSingleton<IHostTestFrameworkProvider, NUnitHostTestFrameworkProvider>();
        services.TryAddSingleton<TestingProviderRegistry>();
        return services;
    }

    /// <summary>
    /// Registers the single MTP-focused <c>testing/*</c> protocol.
    /// Call after <see cref="AddNUnitHostServices"/> so the NUnit provider exists.
    /// </summary>
    public static IServiceCollection AddGenericTestingHostServices(this IServiceCollection services)
    {
        services.AddSingleton<IBridgeRequestHandler, MarshaledTestingRequestHandler>();
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
