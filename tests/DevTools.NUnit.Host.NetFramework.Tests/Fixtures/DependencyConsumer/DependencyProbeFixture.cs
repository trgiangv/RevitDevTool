using GenerationPrivateDependency;
using NUnit.Framework;

namespace DependencyConsumer;

[TestFixture]
public sealed class DependencyProbeFixture
{
    [Test]
    public void DependencyBehavior_IsGenerationSpecific()
    {
        var behavior = BehaviorMarker.GetValue();
        TestContext.WriteLine($"dependency-behavior={behavior}");
        Assert.That(behavior, Is.EqualTo("behavior-one"));
    }
}
