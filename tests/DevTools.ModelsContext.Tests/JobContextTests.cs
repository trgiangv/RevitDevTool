using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class JobContextTests
{
    [TestMethod]
    public void JobRunsOnGivenContext()
    {
        var context = new CountingContext();
        using var fixture = PanelFixture.Create(new FakeModel(TestModel.SmallData), context: context);

        fixture.Panel.SelectedTab = fixture.Panel.SelectedTab == PanelTab.Explorer ? PanelTab.Filter : PanelTab.Explorer;

        Assert.AreEqual(1, context.Posts);
    }

    private sealed class CountingContext : SynchronizationContext
    {
        public int Posts { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Posts++;
            d(state);
        }
    }
}
