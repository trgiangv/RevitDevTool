using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
namespace DevTools.Testing.Host.NUnit;

/// <summary>
/// NUnit file names and versions. Planning uses <c>TestingGenerationSpec</c>.
/// </summary>
public sealed class NUnitGenerationPolicy(Func<RuntimeSource> runtimeSourceProvider) : ITestingGenerationPolicy
{
    public const string FrameworkAssemblyFileName = "nunit.framework.dll";
    public const string RuntimeFolderName = "NUnitRuntime";
    public const string RuntimeAssemblyFileName = "DevTools.NUnit.Runtime.dll";
    public const string RuntimeSymbolFileName = "DevTools.NUnit.Runtime.pdb";

    internal const string ExpectedNUnitFileVersion = "5.0.0.0";
    internal const string ExpectedNUnitPackageVersion = "5.0.0";

    internal static readonly TestingGenerationSpec Spec = new(
        TestFrameworkId.NUnit,
        "NUnit",
        FrameworkAssemblyFileName,
        RuntimeAssemblyFileName,
        RuntimeSymbolFileName,
        [
            new GenerationPin(
                FrameworkAssemblyFileName,
                FileVersion: ExpectedNUnitFileVersion,
                PackageVersion: ExpectedNUnitPackageVersion),
        ],
        RuntimeOwnsDependencyClosure: true);

    private readonly Func<RuntimeSource> _runtimeSourceProvider = runtimeSourceProvider ?? throw new ArgumentNullException(nameof(runtimeSourceProvider));

    public TestFrameworkId FrameworkId => Spec.FrameworkId;

    public TestingGenerationPlan CreatePlan(string testAssemblyPath) =>
        Spec.CreatePlan(_runtimeSourceProvider, testAssemblyPath);

    public void ValidatePublished(TestingGenerationManifest manifest) =>
        Spec.ValidatePublished(manifest);

    internal static string GetFrameworkAssemblyPath(TestingGenerationManifest manifest) =>
        TestingGenerationPins.GetNamedAssembly(manifest, FrameworkAssemblyFileName);
}
