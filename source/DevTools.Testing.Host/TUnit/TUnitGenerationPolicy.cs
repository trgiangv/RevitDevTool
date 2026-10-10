using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.TUnit;

/// <summary>
/// TUnit file names and versions. Planning uses <c>TestingGenerationSpec</c>.
/// </summary>
public sealed class TUnitGenerationPolicy(Func<RuntimeSource> runtimeSourceProvider) : ITestingGenerationPolicy
{
    private const string FrameworkAssemblyFileName = "TUnit.Core.dll";
    internal const string PlatformAssemblyFileName = "Microsoft.Testing.Platform.dll";
    public const string RuntimeFolderName = "TUnitRuntime";
    public const string RuntimeAssemblyFileName = "DevTools.TUnit.Runtime.dll";
    internal const string RuntimeSessionTypeName = "DevTools.TUnit.Runtime.TUnitRuntimeSession";
    internal static readonly Version ExpectedTUnitAssemblyVersion = new(1, 73, 19, 0);
    internal static readonly Version ExpectedMtpAssemblyVersion = new(2, 5, 1, 0);

    internal static readonly TestingGenerationSpec Spec = new(
        TestFrameworkId.TUnit,
        "TUnit",
        FrameworkAssemblyFileName,
        RuntimeAssemblyFileName,
        RuntimeSymbolFileName: null,
        [
            new GenerationPin(FrameworkAssemblyFileName, ExpectedTUnitAssemblyVersion),
            new GenerationPin(PlatformAssemblyFileName, ExpectedMtpAssemblyVersion, AllowMany: true),
        ],
        RuntimeOwnsDependencyClosure: false);

    private readonly Func<RuntimeSource> _runtimeSourceProvider = runtimeSourceProvider ?? throw new ArgumentNullException(nameof(runtimeSourceProvider));

    public TestFrameworkId FrameworkId => Spec.FrameworkId;

    public TestingGenerationPlan CreatePlan(string testAssemblyPath) =>
        Spec.CreatePlan(_runtimeSourceProvider, testAssemblyPath);

    public void ValidatePublished(TestingGenerationManifest manifest) =>
        Spec.ValidatePublished(manifest);
}
