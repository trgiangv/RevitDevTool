using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.NUnit.Host;

public static class NUnitHostingExtensions
{
    /// <summary>
    /// Registers the native NUnit runtime manager, generation builder, NUnit
    /// provider, and <c>nunit/*</c> bridge handler. Call from Revit/AutoCAD
    /// hosting after <c>AddExecutionServices()</c>.
    /// </summary>
    /// <remarks>
    /// <c>testing/*</c> is not registered here. Host composition must add
    /// <see cref="TestingRequestHandler"/> with legacy NUnit envelopes disabled
    /// and host-thread marshaling so <c>nunit/*</c> methods stay unique.
    /// </remarks>
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
        services.TryAddSingleton<IHostTestFrameworkProvider, NUnitHostTestFrameworkProvider>();
        services.TryAddSingleton<TestingProviderRegistry>();
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
