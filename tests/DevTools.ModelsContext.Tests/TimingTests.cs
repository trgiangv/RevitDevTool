using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class TimingTests
{
    private static readonly ElementRef[] Sent =
    [
        new(FakeModelGenerator.Current, 1),
        new(FakeModelGenerator.Mep, 2)
    ];

    [TestMethod]
    public async Task SelfCausedSelectionIsDropped()
    {
        var clock = new FakeClock();
        var guard = new SelectionGuard(clock);
        var model = new FakeModel(TestModel.SmallData);
        var handled = new List<IReadOnlyCollection<ElementRef>>();
        model.SelectionChanged += (_, selection) =>
        {
            if (!guard.ShouldDrop(selection))
            {
                handled.Add(selection);
            }
        };

        guard.RecordSent(Sent);
        await model.SelectAsync(Sent, CancellationToken.None);
        model.RaiseSelectionChanged(Sent);

        Assert.HasCount(1, handled);
    }

    [TestMethod]
    public void ExpiredGuardDropsNothing()
    {
        var clock = new FakeClock();
        var guard = new SelectionGuard(clock, TimeSpan.FromSeconds(1));

        guard.RecordSent(Sent);
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.IsFalse(guard.ShouldDrop(Sent));
    }

    [TestMethod]
    public void DebounceFiresOnce()
    {
        var clock = new FakeClock();
        var fired = 0;
        using var debouncer = new Debouncer(clock, TimeSpan.FromMilliseconds(300), () => fired++);

        for (var index = 0; index < 5; index++)
        {
            debouncer.Trigger();
            clock.Advance(TimeSpan.FromMilliseconds(100));
        }

        clock.Advance(TimeSpan.FromMilliseconds(300));

        Assert.AreEqual(1, fired);
        Assert.IsFalse(debouncer.IsPending);
    }

    [TestMethod]
    public void CancelledDebounceNeverFires()
    {
        var clock = new FakeClock();
        var fired = 0;
        using var debouncer = new Debouncer(clock, TimeSpan.FromMilliseconds(300), () => fired++);

        debouncer.Trigger();
        debouncer.Cancel();
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(0, fired);
    }
}
