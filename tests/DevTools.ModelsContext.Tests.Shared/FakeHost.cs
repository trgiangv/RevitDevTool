using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using DevTools.ModelsContext.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeHost
{
    public FakeHost(FakeModel model)
    {
        Model = model;
        Actions = new FakeViewActions(model);
        HostFilters = HostFiltersOf(model);
        CustomFilters = EmptyStore();
    }

    public FakeModel Model { get; }

    public FakeViewActions Actions { get; }

    public Mock<IHostFilterCatalog> HostFilters { get; }

    public Mock<ICustomFilterStore> CustomFilters { get; }

    public ModelContextServices Services(
        IClock clock,
        IScheduler scheduler,
        ILogger? logger = null,
        IModelSource? source = null)
    {
        return new ModelContextServices(source ?? Model, Model, Model, Actions, Model, HostFilters.Object,
            CustomFilters.Object, clock, scheduler, logger);
    }

    private static Mock<IHostFilterCatalog> HostFiltersOf(FakeModel model)
    {
        var catalog = new Mock<IHostFilterCatalog>();
        catalog.Setup(host => host.GetFiltersAsync(It.IsAny<DocumentKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentKey document, CancellationToken _) =>
            [
                new HostFilterInfo(document, 1, "Commented", HostFilterKind.RuleBased, document.IsCurrentModel),
                new HostFilterInfo(document, 2, "Picked", HostFilterKind.Selection, false)
            ]);
        catalog.Setup(host => host.ReadAsync(It.IsAny<DocumentKey>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FilterDefinition([],
                FilterSet.All(new FilterRule(FakeParameters.Comments, FilterOperator.NotEmpty),
                    new UnsupportedRule("Global parameter association"))));
        catalog.Setup(host => host.EvaluateAsync(It.IsAny<DocumentKey>(), It.IsAny<long>(),
                It.IsAny<IReadOnlyCollection<long>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentKey document, long filterId, IReadOnlyCollection<long>? scope, CancellationToken _) =>
                model.SourceOf(document).Elements
                    .Take(filterId == 1 ? 7 : filterId == 2 ? 10 : 0)
                    .Select(element => element.Id)
                    .Where(id => scope is null || scope.Contains(id))
                    .ToList());
        return catalog;
    }

    private static Mock<ICustomFilterStore> EmptyStore()
    {
        IReadOnlyList<CustomFilter> saved = [];
        var store = new Mock<ICustomFilterStore>();
        store.Setup(filters => filters.Load()).Returns(() => saved);
        store.Setup(filters => filters.Save(It.IsAny<IReadOnlyList<CustomFilter>>()))
            .Callback((IReadOnlyList<CustomFilter> filters) => saved = filters.ToList());
        return store;
    }
}
