using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.TUnit;

public sealed class TUnitGenerationPolicy : ITestingGenerationPolicy
{
    public TestFrameworkId FrameworkId => TestFrameworkId.TUnit;

    public TestingGenerationPlan CreatePlan(string testAssemblyPath) =>
        OutputGeneration.CreatePlan(FrameworkId, testAssemblyPath);

    public void ValidatePublished(TestingGenerationManifest manifest) =>
        OutputGeneration.ValidatePublished(FrameworkId, manifest);
}
