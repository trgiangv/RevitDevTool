using Build.Modules;
using Build.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;

var builder = Pipeline.CreateBuilder();

builder.Configuration.AddJsonFile("appsettings.json");
builder.Configuration.AddUserSecrets<Program>();
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddOptions<BuildOptions>().Bind(builder.Configuration.GetSection("Build"));
builder.Services.AddOptions<BundleOptions>().Bind(builder.Configuration.GetSection("Bundle"));
builder.Services.AddOptions<PublishOptions>().Bind(builder.Configuration.GetSection("Publish"));
builder.Services.AddOptions<UpdateOptions>().Bind(builder.Configuration.GetSection("Update"));

if (args.Length == 0)
{
    builder.Services.AddModule<CompileBaseModule>();
    builder.Services.AddModule<CompileExpandModule>();
    builder.Services.AddModule<CompileDaemonModule>();
    builder.Services.AddModule<CompileTestRunnerModule>();
}

if (args.Contains("test"))
{
    builder.Services.AddModule<TestProjectModule>();
}

if (args.Contains("pack"))
{
    SolutionCompile.DeployAddin = false;
    builder.Services.AddModule<CreateBundleModule>();
    builder.Services.AddModule<PackPayloadsModule>();
    builder.Services.AddModule<GenerateUpdateManifestModule>();
    builder.Services.AddModule<PackDaemonModule>();
    builder.Services.AddModule<SignUpdateFeedsModule>();
    builder.Services.AddModule<VerifyReleaseModule>();
}

if (args.Contains("publish"))
{
    builder.Services.AddModule<PublishGithubModule>();
}

if (args.Contains("adapter"))
{
    builder.Services.AddModule<PackTestAdapterModule>();
}

await builder.Build().RunAsync();

