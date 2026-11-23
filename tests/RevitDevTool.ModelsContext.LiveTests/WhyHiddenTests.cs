using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Visibility;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class WhyHiddenTests : RecordedTest
{
    private const long FederatedLink = 1362429;
    private const long LinkedDoor = 631587;
    private const string FilterName = "mc-why-hidden";
    private const int HiddenSample = 40;

    private static readonly BuiltInCategory[] VisibleCategories =
        [BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_Walls, BuiltInCategory.OST_Doors];

    private static readonly RevitVisibilityExplainer Explainer = new();

    private static Document Model => SampleViews.Active.Document;

    private static View View => SampleViews.Active.ActiveView;

    [Test]
    public void CategoryOffNamesCategory()
    {
        Record(Case("WH-01", "Category off in the active view", "Category off: <category>",
            "Turn off the element's category in Visibility/Graphics; Why hidden reads 'Category off: <category>'."), () =>
        {
            var element = VisibleElement();
            var category = element.Category!;
            using var edit = new RolledBack(Model, "Why hidden category");
            Build(() => View.SetCategoryHidden(category.Id, true), View.CanCategoryBeHidden(category.Id));
            Evidence.Step($"Turned off '{category.Name}' in '{View.Name}' in a transaction that is rolled back.");
            ExpectFirst(Explain(CurrentOf(element)), HiddenCues.Category(category.Name));
        });
    }

    [Test]
    public void HiddenElementNoFix()
    {
        Record(Case("WH-02", "Element hidden in the active view", "Hidden in view",
            "Hide one element in view (Hide in View > Elements); Why hidden reads 'Hidden in view' with no Show."), () =>
        {
            var element = VisibleElement();
            using var edit = new RolledBack(Model, "Why hidden element");
            Build(() => View.HideElements([element.Id]), element.CanBeHidden(View));
            Evidence.Step($"Hid element {element.Id} in '{View.Name}' in a transaction that is rolled back.");
            ExpectFirst(Explain(CurrentOf(element)), HiddenCues.HiddenInView);
        });
    }

    [Test]
    public void ViewFilterNamesFilter()
    {
        Record(Case("WH-03", "View filter with visibility off", "Filter: " + FilterName,
            $"Add a rule-less filter '{FilterName}' on the element's category to the view and untick its visibility; Why hidden reads 'Filter: {FilterName}'."), () =>
        {
            var element = VisibleElement();
            using var edit = new RolledBack(Model, "Why hidden filter");
            Build(() =>
            {
                var filter = ParameterFilterElement.Create(Model, FilterName, [element.Category!.Id]);
                View.AddFilter(filter.Id);
                View.SetFilterVisibility(filter.Id, false);
            }, true);
            Evidence.Step($"Added filter '{FilterName}' with visibility off to '{View.Name}' in a transaction that is rolled back.");
            ExpectFirst(Explain(CurrentOf(element)), HiddenCues.Filter(FilterName));
        });
    }

    [Test]
    [Explicit("Run alone after the MCP step scripts/models-context/user-action-activate-view.cs with ViewName set to 'L3': it checks the active plan and never switches views.")]
    public void LinkedDoorAboveCutPlane()
    {
        Record(Case("WH-04", "Linked L4 door in the L3 plan", "Above cut plane",
            "Snowdon Federated, plan L3 active: Why hidden on the Architectural door 631587 (L4) reads 'Above cut plane'."), () =>
        {
            if (View is not ViewPlan)
            {
                Assert.Inconclusive($"The active view '{View.Name}' is not a plan.");
            }

            var (instance, door) = FindLinkedDoor();
            var element = new ElementRef(DocumentRegistry.KeyOf(Model, instance), door.Id.ToLong());
            if (LinkElements.VisibleOf(Model, View, [element]).Count > 0)
            {
                Assert.Inconclusive($"Door {LinkedDoor} is visible in '{View.Name}'.");
            }

            ExpectAny(Explain(element), HiddenCauseKind.AboveCutPlane);
        });
    }

    [Test]
    [Explicit("Run alone after the MCP step scripts/models-context/user-action-activate-view.cs with ViewName set to 'L3': it checks the active plan and never switches views.")]
    public void LinkedHiddenAllHaveCause()
    {
        Record(Case("WH-09", "Hidden linked elements in the active plan", "a cause for each",
            $"Snowdon Federated, plan L3 active: Why hidden on {HiddenSample} hidden elements of each loaded link never reads '{HiddenCues.NoneFound.Text}'."), () =>
        {
            if (View is not ViewPlan)
            {
                Assert.Inconclusive($"The active view '{View.Name}' is not a plan.");
            }

            var unexplained = new List<string>();
            var checkedCount = 0;
            foreach (var instance in LoadedLinks())
            {
                var key = DocumentRegistry.KeyOf(Model, instance);
                var hidden = HiddenOf(instance);
                Evidence.Step($"{instance.Name}: {hidden.Count} hidden by the panel rule in '{View.Name}'.");
                var stride = Math.Max(1, hidden.Count / HiddenSample);
                foreach (var id in hidden.Where((_, index) => index % stride == 0).Take(HiddenSample))
                {
                    checkedCount++;
                    var result = Adapter.Run(Explainer.ExplainAsync(new ElementRef(key, id), CancellationToken.None));
                    if (!result.Found)
                    {
                        var element = instance.GetLinkDocument()!.GetElement(id.ToElementId());
                        unexplained.Add($"{instance.Name} {id} {element?.Category?.Name} {element?.GetType().Name}");
                    }
                }
            }

            if (checkedCount == 0)
            {
                Assert.Inconclusive($"No hidden linked element in '{View.Name}'.");
            }

            Evidence.Values(0, unexplained.Count);
            Assert.That(unexplained, Is.Empty, string.Join("; ", unexplained.Take(10)));
        });
    }

    [Test]
    public void SecondaryOptionNamesOption()
    {
        Record(Case("WH-05", "Element of a secondary design option", "Design option: <option>",
            "Pick an element of a secondary design option; Why hidden reads 'Design option: <option>'."), () =>
        {
            if (FindSecondaryOption() is not { } found)
            {
                Assert.Inconclusive("No model element of a secondary design option in the model or its links.");
                return;
            }

            Evidence.Step($"Element {found.Element} of option '{found.Option}'.");
            ExpectAny(Explain(found.Element), HiddenCauseKind.DesignOption);
        });
    }

    [Test]
    public void VisibleElementHasNoCause()
    {
        Record(Case("WH-06", "Element visible in the active view", HiddenCues.NoneFound.Text,
            $"Pick an element that the active view shows; Why hidden reads '{HiddenCues.NoneFound.Text}'."), () =>
        {
            var result = Explain(CurrentOf(VisibleElement()));
            Evidence.Values(HiddenCues.NoneFound.Text, Describe(result));
            Assert.That(result.Causes, Is.EqualTo(new[] { HiddenCues.NoneFound }));
        });
    }

    private static CaseItem Case(string id, string description, string expected, string step)
    {
        return new CaseItem
        {
            Id = id,
            Description = description,
            Expected = JsonSerializer.SerializeToElement(expected),
            ManualSteps = [step]
        };
    }

    private static WhyHiddenResult Explain(ElementRef element)
    {
        var result = Evidence.Time("why hidden", () => Adapter.Run(Explainer.ExplainAsync(element, CancellationToken.None)));
        Evidence.Step($"Why hidden on {element}: {Describe(result)} in {Math.Round(result.ElapsedMs, 1)} ms.");
        return result;
    }

    private static void ExpectFirst(WhyHiddenResult result, HiddenCause expected)
    {
        Evidence.Values(expected.Text, Describe(result));
        Assert.That(result.First, Is.EqualTo(expected));
    }

    private static void ExpectAny(WhyHiddenResult result, HiddenCauseKind kind)
    {
        Evidence.Values(kind.ToString(), Describe(result));
        Assert.That(result.Causes.Select(cause => cause.Kind), Does.Contain(kind));
    }

    private static string Describe(WhyHiddenResult result)
    {
        return string.Join("; ", result.Causes.Select(cause => cause.Fix == HiddenFix.None ? cause.Text : $"{cause.Text} · {cause.Fix}"));
    }

    private static void Build(Action change, bool allowed)
    {
        if (!allowed)
        {
            Assert.Inconclusive($"'{View.Name}' does not allow this change.");
        }

        try
        {
            change();
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException exception)
        {
            Assert.Inconclusive($"'{View.Name}' refused the change: {exception.Message}");
        }
    }

    private static ElementRef CurrentOf(Element element)
    {
        return new ElementRef(DocumentRegistry.KeyOf(Model), element.Id.ToLong());
    }

    private static Element VisibleElement()
    {
        var view = View;
        foreach (var category in VisibleCategories)
        {
            var element = new FilteredElementCollector(Model, view.Id)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .FirstOrDefault(candidate => candidate.Category is not null && !candidate.IsHidden(view));
            if (element is not null)
            {
                return element;
            }
        }

        Assert.Inconclusive($"'{view.Name}' shows no ducts, walls or doors of the active model.");
        return null!;
    }

    private static IEnumerable<RevitLinkInstance> LoadedLinks()
    {
        return new FilteredElementCollector(Model).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
            .Where(instance => instance.GetLinkDocument() is not null)
            .ToList();
    }

    private static List<long> HiddenOf(RevitLinkInstance instance)
    {
        var document = instance.GetLinkDocument()!;
        var reader = new ElementReader(document, []);
        foreach (var id in ElementReader.CollectAnyOption(document))
        {
            if (document.GetElement(id.ToElementId()) is { } element)
            {
                reader.Read(element);
            }
        }

        var visible = LinkElements.InView(Model, View, instance.Id) is { } collector
            ? new HashSet<long>(collector.WhereElementIsNotElementType().ToElementIds().Select(id => id.ToLong()))
            : [];
        var view = RevitViewActions.ViewInfoOf(Model, View);
        return reader.Elements
            .Select(record => new ElementRow(record.Id, 0, 0, 0, 0, record.SecondaryOption, record.Draw, record.OwnerViewId))
            .Where(row => !row.SecondaryOption && !visible.Contains(row.Id) && ViewDraws.CanDraw(view, row, false))
            .Select(row => row.Id)
            .OrderBy(id => id)
            .ToList();
    }

    private static (RevitLinkInstance Instance, Element Door) FindLinkedDoor()
    {
        var links = new FilteredElementCollector(Model).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
            .OrderBy(instance => instance.Id.ToLong() == FederatedLink ? 0 : 1);
        foreach (var instance in links)
        {
            if (instance.GetLinkDocument()?.GetElement(LinkedDoor.ToElementId()) is { } door
                && door.Category?.Id.ToLong() == (long)BuiltInCategory.OST_Doors)
            {
                return (instance, door);
            }
        }

        Assert.Inconclusive($"No loaded link holds door {LinkedDoor}; open Snowdon Federated with plan L3 active.");
        return default;
    }

    private static (ElementRef Element, string Option)? FindSecondaryOption()
    {
        var documents = new List<(DocumentKey Key, Document Document)> { (DocumentRegistry.KeyOf(Model), Model) };
        documents.AddRange(new FilteredElementCollector(Model).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
            .Where(instance => instance.GetLinkDocument() is not null)
            .Select(instance => (DocumentRegistry.KeyOf(Model, instance), instance.GetLinkDocument())));
        foreach (var (key, document) in documents)
        {
            var options = new FilteredElementCollector(document).OfClass(typeof(DesignOption)).Cast<DesignOption>()
                .Where(option => !option.IsPrimary && option.Id != DesignOption.GetActiveDesignOptionId(document));
            foreach (var option in options)
            {
                var element = new FilteredElementCollector(document)
                    .WherePasses(new ElementDesignOptionFilter(option.Id))
                    .WhereElementIsNotElementType()
                    .FirstOrDefault(candidate => candidate.Category is { CategoryType: CategoryType.Model }
                                                 && candidate.get_BoundingBox(null) is not null);
                if (element is not null)
                {
                    return (new ElementRef(key, element.Id.ToLong()), option.Name);
                }
            }
        }

        return null;
    }

    private sealed class RolledBack : IDisposable
    {
        private readonly Transaction _transaction;

        public RolledBack(Document document, string name)
        {
            _transaction = new Transaction(document, name);
            _transaction.Start();
        }

        public void Dispose()
        {
            if (_transaction.HasStarted())
            {
                _transaction.RollBack();
            }

            _transaction.Dispose();
        }
    }
}
