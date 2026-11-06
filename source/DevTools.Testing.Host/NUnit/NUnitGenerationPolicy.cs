using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.NUnit;

public sealed class NUnitGenerationPolicy : ITestingGenerationPolicy
{
    public TestFrameworkId FrameworkId => TestFrameworkId.NUnit;

    public TestingGenerationPlan CreatePlan(string testAssemblyPath) =>
        OutputGeneration.CreatePlan(FrameworkId, testAssemblyPath);

    public void ValidatePublished(TestingGenerationManifest manifest) =>
        OutputGeneration.ValidatePublished(FrameworkId, manifest);
}
