using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.MSTest;

/// <summary>
/// MSTest file names and versions. Planning uses <c>TestingGenerationSpec</c>.
/// </summary>
public sealed class MSTestGenerationPolicy(Func<RuntimeSource> runtimeSourceProvider) : ITestingGenerationPolicy
{
    private const string TestFrameworkFileName = "MSTest.TestFramework.dll";
    private const string TestAdapterFileName = "MSTest.TestAdapter.dll";
    private const string PlatformServicesFileName = "MSTestAdapter.PlatformServices.dll";
    internal const string PlatformAssemblyFileName = "Microsoft.Testing.Platform.dll";
    public const string RuntimeFolderName = "MSTestRuntime";
    public const string RuntimeAssemblyFileName = "DevTools.MSTest.Runtime.dll";
    internal const string RuntimeSessionTypeName = "DevTools.MSTest.Runtime.MSTestRuntimeSession";
    internal static readonly Version ExpectedMSTestAssemblyVersion = new(4, 4, 1, 0);
    internal static readonly Version ExpectedMtpAssemblyVersion = new(2, 4, 1, 0);

    internal static readonly TestingGenerationSpec Spec = new(
        TestFrameworkId.MSTest,
        "MSTest",
        TestFrameworkFileName,
        RuntimeAssemblyFileName,
        RuntimeSymbolFileName: null,
        [
            new GenerationPin(TestFrameworkFileName, ExpectedMSTestAssemblyVersion),
            new GenerationPin(TestAdapterFileName, ExpectedMSTestAssemblyVersion),
            new GenerationPin(PlatformServicesFileName, ExpectedMSTestAssemblyVersion),
            new GenerationPin(PlatformAssemblyFileName, ExpectedMtpAssemblyVersion),
        ],
        RuntimeOwnsDependencyClosure: false);

    private readonly Func<RuntimeSource> _runtimeSourceProvider = runtimeSourceProvider ?? throw new ArgumentNullException(nameof(runtimeSourceProvider));

    public TestFrameworkId FrameworkId => Spec.FrameworkId;

    public TestingGenerationPlan CreatePlan(string testAssemblyPath) =>
        Spec.CreatePlan(_runtimeSourceProvider, testAssemblyPath);

    public void ValidatePublished(TestingGenerationManifest manifest) =>
        Spec.ValidatePublished(manifest);
}
