using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.MSTest;

public sealed class MSTestGenerationPolicy : ITestingGenerationPolicy
{
    public TestFrameworkId FrameworkId => TestFrameworkId.MSTest;

    public TestingGenerationPlan CreatePlan(string testAssemblyPath) =>
        OutputGeneration.CreatePlan(FrameworkId, testAssemblyPath);

    public void ValidatePublished(TestingGenerationManifest manifest) =>
        OutputGeneration.ValidatePublished(FrameworkId, manifest);
}
