using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Host.Loading;

public interface ITestingGenerationPolicy
{
    TestFrameworkId FrameworkId { get; }

    TestingGenerationPlan CreatePlan(string testAssemblyPath);

    void ValidatePublished(TestingGenerationManifest manifest);
}
