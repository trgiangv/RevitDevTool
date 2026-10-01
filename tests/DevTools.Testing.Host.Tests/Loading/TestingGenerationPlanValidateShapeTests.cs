using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests.Loading;

[TestClass]
public sealed class TestingGenerationPlanValidateShapeTests
{
    [TestMethod]
    public void ValidateShape_rejects_empty_framework_id()
    {
        var plan = new TestingGenerationPlan(
            (TestFrameworkId)42,
            @"C:\tests\sample.dll",
            [(@"C:\tests\sample.dll", "sample.dll")],
            "sample.dll");

        var exception = Assert.ThrowsExactly<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("framework ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ValidateShape_rejects_empty_files()
    {
        var plan = new TestingGenerationPlan(
            TestFrameworkId.NUnit,
            @"C:\tests\sample.dll",
            [],
            "sample.dll");

        var exception = Assert.ThrowsExactly<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("must contain files", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ValidateShape_rejects_rooted_relative_paths()
    {
        var plan = new TestingGenerationPlan(
            TestFrameworkId.NUnit,
            @"C:\tests\sample.dll",
            [(@"C:\tests\sample.dll", @"C:\evil.dll")],
            @"C:\evil.dll");

        Assert.ThrowsExactly<TestingGenerationBuildException>(() => plan.ValidateShape());
    }

    [TestMethod]
    public void ValidateShape_rejects_parent_traversal()
    {
        var plan = new TestingGenerationPlan(
            TestFrameworkId.NUnit,
            @"C:\tests\sample.dll",
            [(@"C:\tests\sample.dll", "..\\sample.dll")],
            "..\\sample.dll");

        Assert.ThrowsExactly<TestingGenerationBuildException>(() => plan.ValidateShape());
    }

    [TestMethod]
    public void ValidateShape_rejects_duplicate_paths()
    {
        var plan = new TestingGenerationPlan(
            TestFrameworkId.NUnit,
            @"C:\tests\sample.dll",
            [
                (@"C:\tests\sample.dll", @"folder\sample.dll"),
                (@"C:\tests\other.dll", @"folder\sample.dll"),
            ],
            @"folder\sample.dll");

        var exception = Assert.ThrowsExactly<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("duplicate path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ValidateShape_rejects_runtime_path_not_in_files()
    {
        var plan = new TestingGenerationPlan(
            TestFrameworkId.NUnit,
            @"C:\tests\sample.dll",
            [(@"C:\tests\sample.dll", "sample.dll")],
            "runtime.dll");

        var exception = Assert.ThrowsExactly<TestingGenerationBuildException>(() => plan.ValidateShape());

        Assert.Contains("runtime assembly path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
