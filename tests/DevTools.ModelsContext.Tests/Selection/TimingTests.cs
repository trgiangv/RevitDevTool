using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Selection;

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
        var echo = new SentSelectionEcho(clock);
        var model = new FakeModel(TestModel.SmallData);
        var handled = new List<IReadOnlyCollection<ElementRef>>();
        model.SelectionChanged += (_, selection) =>
        {
            if (!echo.ShouldDrop(selection))
            {
                handled.Add(selection);
            }
        };

        echo.RecordSent(Sent);
        await model.SelectAsync(Sent, CancellationToken.None);
        model.RaiseSelectionChanged(Sent);

        Assert.HasCount(1, handled);
    }

    [TestMethod]
    public void OlderEchoIsDropped()
    {
        var echo = new SentSelectionEcho(new FakeClock());
        ElementRef[] next = [new(FakeModelGenerator.Current, 3)];

        echo.RecordSent(Sent);
        echo.RecordSent(next);

        Assert.IsTrue(echo.ShouldDrop(Sent));
        Assert.IsTrue(echo.ShouldDrop(next));
        Assert.IsFalse(echo.ShouldDrop(Sent));
    }

    [TestMethod]
    public void ExpiredEchoDropsNothing()
    {
        var clock = new FakeClock();
        var echo = new SentSelectionEcho(clock, TimeSpan.FromSeconds(1));

        echo.RecordSent(Sent);
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.IsFalse(echo.ShouldDrop(Sent));
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
