using Build.Modules;
using Build.Options;
using Build.Trains;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;

namespace Build.Pipeline;

/// <summary>
///     The modules of one run. <c>ci</c> compiles and tests, <c>pack</c> compiles and packs, <c>release</c> does all three.
///     The <c>nettest</c> pack builds its own projects, and <c>pytest</c> has no .NET compile.
/// </summary>
public static class TrainPipeline
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
        var train = run.Train;
        if (train.IsDotNet && (run.RunsTests || train != Train.NetTest)) AddCompile(services, train);
        if (run.RunsTests) AddTests(services, train);
        if (run.BuildsArtifacts) AddPack(services, train);
    }

    private static void AddCompile(IServiceCollection services, Train train)
    {
        if (train != Train.Addin)
        {
            services.AddModule<CompileTrainModule>();
            return;
        }

        services.AddModule<ResolveConfigsModule>();
        services.AddModule<CompileBaseModule>();
        services.AddModule<CompileExpandModule>();
    }

    private static void AddTests(IServiceCollection services, Train train)
    {
        if (train == Train.PyTest) services.AddModule<TestPyTestModule>();
        else services.AddModule<TestTrainModule>();
    }

    private static void AddPack(IServiceCollection services, Train train)
    {
        if (train == Train.Daemon) services.AddModule<PackDaemonModule>();
        if (train == Train.NetTest) services.AddModule<PackTestAdapterModule>();
        if (train == Train.PyTest) services.AddModule<PackPyTestModule>();
        if (train != Train.Addin) return;

        services.AddModule<CreateBundleModule>();
        services.AddModule<PackPayloadsModule>();
        services.AddModule<GenerateUpdateManifestModule>();
    }
}
