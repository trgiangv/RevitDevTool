using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Panel;

[TestClass]
public sealed class JobContextTests
{
    [TestMethod]
    public void JobRunsOnSchedulerContext()
    {
        using var fixture = PanelFixture.Create(new FakeModel(TestModel.SmallData));

        fixture.Panel.SelectedTab = fixture.Panel.SelectedTab == PanelTab.Explorer ? PanelTab.Filter : PanelTab.Explorer;

        Assert.AreEqual(1, fixture.Clock.Context.Posts);
    }
}
