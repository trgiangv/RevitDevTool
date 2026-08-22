using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests.Loading;

public sealed class TestingGenerationPlanValidateShapeTests
{
    [Fact]
    public void ValidateShape_rejects_empty_framework_id()
    {
        var plan = new TestingGenerationPlan(
            "",
            @"C:\tests\sample.dll",
            [new TestingGenerationFile(@"C:\tests\sample.dll", "sample.dll", TestingGenerationFileKind.Managed)],
            "sample.dll");

        var exception = Assert.Throws<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("framework ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateShape_rejects_empty_files()
    {
        var plan = new TestingGenerationPlan(
            "provider.example",
            @"C:\tests\sample.dll",
            [],
            "sample.dll");

        var exception = Assert.Throws<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("must contain files", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateShape_rejects_rooted_relative_paths()
    {
        var plan = new TestingGenerationPlan(
            "provider.example",
            @"C:\tests\sample.dll",
            [new TestingGenerationFile(@"C:\tests\sample.dll", @"C:\evil.dll", TestingGenerationFileKind.Managed)],
            @"C:\evil.dll");

        Assert.Throws<TestingGenerationBuildException>(() => plan.ValidateShape());
    }

    [Fact]
    public void ValidateShape_rejects_parent_traversal()
    {
        var plan = new TestingGenerationPlan(
            "provider.example",
            @"C:\tests\sample.dll",
            [new TestingGenerationFile(@"C:\tests\sample.dll", "..\\sample.dll", TestingGenerationFileKind.Managed)],
            "..\\sample.dll");

        Assert.Throws<TestingGenerationBuildException>(() => plan.ValidateShape());
    }

    [Fact]
    public void ValidateShape_rejects_duplicate_normalized_paths()
    {
        var plan = new TestingGenerationPlan(
            "provider.example",
            @"C:\tests\sample.dll",
            [
                new TestingGenerationFile(@"C:\tests\sample.dll", "folder\\sample.dll", TestingGenerationFileKind.Managed),
                new TestingGenerationFile(@"C:\tests\other.dll", "folder/sample.dll", TestingGenerationFileKind.Managed),
            ],
            "folder\\sample.dll");

        var exception = Assert.Throws<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("duplicate path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateShape_rejects_runtime_path_not_in_files()
    {
        var plan = new TestingGenerationPlan(
            "provider.example",
            @"C:\tests\sample.dll",
            [new TestingGenerationFile(@"C:\tests\sample.dll", "sample.dll", TestingGenerationFileKind.Managed)],
            "runtime.dll");

        var exception = Assert.Throws<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("runtime assembly path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
