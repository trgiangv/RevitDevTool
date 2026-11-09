using Build.Modules;
using Build.Options;
using Build.Products;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;

namespace Build.Pipeline;

/// <summary>
///     The modules of each stage of one run. The add-in pack bundles what the compile left in <see cref="AddinHosts"/>;
///     the other packs build what they ship themselves. <c>pytest</c> has no .NET compile.
/// </summary>
public static class ProductPipeline
{
    public static async Task RunAsync(BuildRun run)
    {
        var builder = ModularPipelines.Pipeline.CreateBuilder();
        builder.Configuration.AddJsonFile("appsettings.json");
        builder.Configuration.AddUserSecrets<BuildRun>();
        builder.Configuration.AddEnvironmentVariables();

        builder.Services.AddOptions<BundleOptions>().Bind(builder.Configuration.GetSection("Bundle"));
        builder.Services.AddOptions<UpdateOptions>().Bind(builder.Configuration.GetSection("Update"));
        builder.Services.AddSingleton(run);
        AddModules(builder.Services, run);

        await builder.Build().RunAsync();
    }

    private static void AddModules(IServiceCollection services, BuildRun run)
    {
        var product = run.Product;
        if (run.Has(Stage.Compile) && product.IsDotNet) AddCompile(services, product);
        if (run.Has(Stage.Test)) AddTests(services, product);
        if (run.Has(Stage.Pack)) AddPack(services, product);
    }

    private static void AddCompile(IServiceCollection services, Product product)
    {
        if (product != Product.Addin)
        {
            services.AddModule<CompileProductModule>();
            return;
        }

        services.AddModule<ResolveConfigsModule>();
        services.AddModule<CompileBaseModule>();
        services.AddModule<CompileExpandModule>();
    }

    private static void AddTests(IServiceCollection services, Product product)
    {
        if (product == Product.PyTest) services.AddModule<TestPyTestModule>();
        else services.AddModule<TestProductModule>();
    }

    private static void AddPack(IServiceCollection services, Product product)
    {
        if (product == Product.Daemon) services.AddModule<PackDaemonModule>();
        if (product == Product.NetTest) services.AddModule<PackTestAdapterModule>();
        if (product == Product.PyTest) services.AddModule<PackPyTestModule>();
        if (product != Product.Addin) return;

        services.AddModule<CreateBundleModule>();
        services.AddModule<PackPayloadsModule>();
        services.AddModule<GenerateUpdateManifestModule>();
    }
}
