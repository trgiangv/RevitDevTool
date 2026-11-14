using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using Microsoft.Extensions.Logging;

namespace DevTools.ModelsContext.Tests;

internal sealed class PanelFixture : IDisposable
{
    private PanelFixture(FakeModel model, FakeClock clock, ModelContextViewModel panel)
    {
        Model = model;
        Clock = clock;
        Panel = panel;
    }

    public FakeModel Model { get; }

    public FakeClock Clock { get; }

    public ModelContextViewModel Panel { get; }

    public ExplorerTabViewModel Explorer => Panel.Explorer;

    public FilterTabViewModel Filter => Panel.Filter;

    public static async Task<PanelFixture> OpenAsync(
        FakeModelData? data = null,
        ScopeKind scope = ScopeKind.WholeModel,
        ILogger? logger = null)
    {
        var fixture = Create(new FakeModel(data ?? TestModel.SmallData), scope, logger);
        await fixture.Panel.InitializeAsync(CancellationToken.None);
        return fixture;
    }

    public static PanelFixture Create(FakeModel model, ScopeKind scope = ScopeKind.WholeModel, ILogger? logger = null)
    {
        var clock = new FakeClock();
        var panel = new ModelContextViewModel(new ModelContextServices(model, model, model, model, model, model, model,
            clock, clock, logger));
        panel.ContextBar.Scope = scope;
        return new PanelFixture(model, clock, panel);
    }

    public TreeRow? FindGroup(DocumentKey document, string name)
    {
        return Explorer.Rows.FirstOrDefault(row =>
            row.Kind == TreeRowKind.Group && row.Document == document && row.Text == name);
    }

    public void Settle()
    {
        Clock.Advance(TimeSpan.FromSeconds(1));
    }

    public TreeRow Group(DocumentKey document, string name)
    {
        return Explorer.Rows.First(row => row.Kind == TreeRowKind.Group && row.Document == document && row.Text == name);
    }

    public IReadOnlyList<ElementRef> Elements(DocumentKey document, int count)
    {
        return Model.Data.SourceOf(document).Elements.Take(count).Select(element => new ElementRef(document, element.Id))
            .ToList();
    }

    public void Dispose()
    {
        Panel.Dispose();
    }
}
