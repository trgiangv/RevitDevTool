namespace DevTools.TUnit.SampleTests;

// Scope: [Before]/[After] hooks at test and class scope.

public sealed class HookTests
{
    static int ClassRuns;
    bool _ready;

    [Before(HookType.Class)]
    public static void Mark_class_started() => ClassRuns++;

    [Before(HookType.Test)]
    public void Mark_ready() => _ready = true;

    [After(HookType.Test)]
    public void Clear_ready() => _ready = false;

    [Test]
    public async Task Before_test_hook_ran()
    {
        await Assert.That(_ready).IsTrue();
    }

    [Test]
    public async Task Class_hook_ran_at_least_once()
    {
        await Assert.That(ClassRuns).IsGreaterThan(0);
    }
}
