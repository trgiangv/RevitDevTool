using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using Microsoft.Extensions.Logging;
using Moq;

namespace DevTools.ModelsContext.Tests;

internal sealed class PanelFixture : IDisposable
{
    private readonly FakeHost _host;

    private PanelFixture(FakeHost host, FakeClock clock, ModelContextViewModel panel)
    {
        _host = host;
        Clock = clock;
        Panel = panel;
    }

    public FakeModel Model => _host.Model;

    public FakeViewActions Actions => _host.Actions;

    public Mock<IHostFilterCatalog> HostFilters => _host.HostFilters;

    public Mock<ICustomFilterStore> CustomFilters => _host.CustomFilters;

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
        var host = new FakeHost(model);
        var clock = new FakeClock();
        var panel = new ModelContextViewModel(host.Services(clock, clock, logger));
        panel.ContextBar.Scope = scope;
        return new PanelFixture(host, clock, panel);
    }

    public TreeRow? FindGroup(DocumentKey document, string name)
    {
        return Explorer.Rows.SelectMany(row => row.Subgroups is { } subgroups ? subgroups.Prepend(row) : [row])
            .FirstOrDefault(row => row.Kind == TreeRowKind.Group && row.Document == document && row.Text == name);
    }

    public void Settle()
    {
        Clock.Advance(TimeSpan.FromSeconds(1));
    }

    public TreeRow Group(DocumentKey document, string name)
    {
        return Explorer.Rows.SelectMany(row => row.Subgroups is { } subgroups ? subgroups.Prepend(row) : [row])
            .First(row => row.Kind == TreeRowKind.Group && row.Document == document && row.Text == name);
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
