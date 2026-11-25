using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
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
            ExpectFirst(Explain(CurrentOf(element)), HiddenReasons.Category(category.Name));
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
            ExpectFirst(Explain(CurrentOf(element)), HiddenReasons.InView);
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
            ExpectFirst(Explain(CurrentOf(element)), HiddenReasons.Filter(FilterName));
        });
    }

    [Test]
    [Explicit("Run alone after the MCP step scripts/models-context/user-action-activate-view.cs with ViewName set to 'L3': it checks the active plan and never switches views.")]
    public void LinkedDoorAboveCutPlane()
    {
        Record(Case("WH-04", "Linked L4 door in the L3 plan", "Above view range",
            "Snowdon HVAC, plan L3 active: Why hidden on the Architectural door 631587 (L4, its bottom on the top clip) reads 'Above view range'."), () =>
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

            ExpectAny(Explain(element), HiddenCause.AboveRange);
        });
    }

    [Test]
    [Explicit("Run alone after the MCP step scripts/models-context/user-action-activate-view.cs with ViewName set to 'L3': it checks the active plan and never switches views.")]
    public void LinkedHiddenAllHaveCause()
    {
        Record(Case("WH-09", "Hidden linked elements in the active plan", "a cause for each",
            $"Snowdon Federated, plan L3 active: Why hidden on {HiddenSample} hidden elements of each loaded link never reads '{HiddenReasons.None.Text}'."), () =>
        {
            if (View is not ViewPlan)
            {
                Assert.Inconclusive($"The active view '{View.Name}' is not a plan.");
            }

            var unexplained = new List<string>();
            var causes = new Dictionary<HiddenCause, int>();
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
                    causes[result.First.Cause] = causes.GetValueOrDefault(result.First.Cause) + 1;
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

            Evidence.Step("First cause of the sampled elements: "
                          + string.Join(", ", causes.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}")) + ".");
            Evidence.Values(0, unexplained.Count);
            Assert.That(unexplained, Is.Empty, string.Join("; ", unexplained.Take(10)));
        });
    }

    [Test]
    [Explicit("Run alone after the MCP step scripts/models-context/user-action-activate-view.cs with ViewName set to 'L3': it checks the active plan and never switches views.")]
    public void TouchingViewDepthNamesRange()
    {
        Record(Case("WH-11", "Element whose top touches the view depth", "Below view depth",
            "Plan L3 active: set the view depth and bottom to the top of a visible duct (cut and top above it); Why hidden reads 'Below view depth'."), () =>
        {
            if (View is not ViewPlan { GenLevel: { } level } plan)
            {
                Assert.Inconclusive($"The active view '{View.Name}' is not a plan.");
                return;
            }

            var element = VisibleElement();
            var top = element.get_BoundingBox(null)!.Max.Z - level.ProjectElevation;
            using var edit = new RolledBack(Model, "Why hidden view depth");
            Build(() =>
            {
                plan.ViewTemplateId = ElementId.InvalidElementId;
                var range = plan.GetViewRange();
                foreach (var (plane, offset) in new[]
                         {
                             (PlanViewPlane.TopClipPlane, top + 3), (PlanViewPlane.CutPlane, top + 2),
                             (PlanViewPlane.BottomClipPlane, top), (PlanViewPlane.ViewDepthPlane, top)
                         })
                {
                    range.SetLevelId(plane, level.Id);
                    range.SetOffset(plane, offset);
                }

                plan.SetViewRange(range);
                Model.Regenerate();
            }, true);
            Evidence.Step($"Set the view depth of '{View.Name}' to the top of element {element.Id} in a transaction that is rolled back.");
            ExpectFirst(Explain(CurrentOf(element)), HiddenReasons.BelowViewDepth);
        });
    }

    [Test]
    public void RailingPartNamesRailing()
    {
        Record(Case("WH-12", "Baluster or support that the view collector leaves out while its railing is drawn", "Shown (in the panel's view ids)",
            $"Pick balusters and supports of drawn railings that the view collector leaves out; the panel counts each as shown (in its Active view ids, so not in +N hidden and no Why hidden), and Why hidden, if asked, reads no fallback and once '{HiddenReasons.WithRailing.Text}'."), () =>
        {
            var parts = FindRailingParts();
            if (parts.Count == 0)
            {
                Assert.Inconclusive($"No baluster or support of a drawn railing is left out of '{View.Name}'.");
            }

            var source = new Adapter().Source;
            var missing = parts.GroupBy(part => part.Document)
                .SelectMany(group =>
                {
                    var ids = Adapter.Run(source.CollectScopeAsync(group.Key, ScopeKind.ActiveView, CancellationToken.None));
                    return group.Where(part => !ids.Contains(part.Id));
                })
                .ToList();
            var results = parts.Select(Explain).ToList();
            Evidence.Values("shown: " + parts.Count, $"shown: {parts.Count - missing.Count}; " + string.Join(" | ", results.Select(Describe)));
            Assert.That(missing, Is.Empty, "Railing parts the panel does not count as shown.");
            Assert.That(results.Select(result => result.First),
                Has.None.EqualTo(HiddenReasons.None).And.None.EqualTo(HiddenReasons.NotDrawnHere));
            Assert.That(results.Select(result => result.First), Has.Some.EqualTo(HiddenReasons.WithRailing));
        });
    }

    [Test]
    public void DrawnDemolishedHasNoCause()
    {
        Record(Case("WH-13", "Demolished element that the view still draws (phase filter Show All)", HiddenReasons.None.Text,
            "Set the view's phase filter to Show All and demolish a visible element in the view's phase; Revit still draws it and Why hidden reads no cause."), () =>
        {
            var element = VisibleElement();
            if (!element.HasPhases()
                || View.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId() is not { } phase
                || new FilteredElementCollector(Model).OfClass(typeof(PhaseFilter)).FirstOrDefault(filter => filter.Name == "Show All") is not { } showAll)
            {
                Assert.Inconclusive($"'{View.Name}' has no phase or the model has no 'Show All' phase filter.");
                return;
            }

            using var edit = new RolledBack(Model, "Why hidden drawn demolished");
            Build(() =>
            {
                View.ViewTemplateId = ElementId.InvalidElementId;
                View.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER).Set(showAll.Id);
                element.DemolishedPhaseId = phase;
                Model.Regenerate();
            }, true);
            if (!new FilteredElementCollector(Model, View.Id).WherePasses(new ElementIdSetFilter([element.Id])).Any())
            {
                Assert.Inconclusive($"'{View.Name}' does not draw the demolished element {element.Id} under Show All.");
            }

            Evidence.Step($"Demolished element {element.Id} under 'Show All' in '{View.Name}' in a transaction that is rolled back.");
            var result = Explain(CurrentOf(element));
            Evidence.Values(HiddenReasons.None.Text, Describe(result));
            Assert.That(result.Reasons, Is.EqualTo(new[] { HiddenReasons.None }));
        });
    }

    [Test]
    [Explicit("Run alone on a copy of Snowdon Architectural with {3D} active (MCP open_document, then user-action-activate-view.cs with ViewName set to '{3D}'): that view hides links it never draws.")]
    public void HiddenLinkNamesLink()
    {
        Record(Case("WH-14", "Element of a link that the active view hides and never drew", HiddenReasons.LinkInstance.Text,
            $"Snowdon Architectural copy, {{3D}} active: Why hidden on an element of each link hidden in the view reads '{HiddenReasons.LinkInstance.Text}' and Revit keeps running."), () =>
        {
            var hidden = LoadedLinks().Where(instance => instance.IsHidden(View)).ToList();
            if (hidden.Count == 0)
            {
                Assert.Inconclusive($"'{View.Name}' hides no loaded link.");
            }

            foreach (var instance in hidden)
            {
                var element = new FilteredElementCollector(instance.GetLinkDocument()!).OfCategory(BuiltInCategory.OST_Walls)
                    .WhereElementIsNotElementType().FirstElement();
                if (element is null)
                {
                    continue;
                }

                Evidence.Step($"{instance.Name} is hidden in '{View.Name}'.");
                ExpectFirst(Explain(new ElementRef(DocumentRegistry.KeyOf(Model, instance), element.Id.ToLong())), HiddenReasons.LinkInstance);
            }
        });
    }

    [Test]
    [Explicit("Run alone with a section view active (MCP user-action-activate-view.cs with ViewName set to the section, e.g. 'Longitudinal Section' on a copy of Snowdon Architectural): it checks the active section and never switches views.")]
    public void SectionDepthNamesSide()
    {
        Record(Case("WH-15", "Elements behind the section line and beyond the far clip", "Behind the section line / Beyond far clip",
            "Section active: Why hidden on a model element whose box is behind the section line reads 'Behind the section line'; with far clip on, one beyond the far clip reads 'Beyond far clip'."), () =>
        {
            if (View is not ViewSection || View.ViewType != ViewType.Section)
            {
                Assert.Inconclusive($"The active view '{View.Name}' is not a section.");
            }

            var box = View.CropBox;
            var inverse = box.Transform.Inverse;
            var drawn = new HashSet<ElementId>(new FilteredElementCollector(Model, View.Id).ToElementIds());
            var candidates = new FilteredElementCollector(Model).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType()
                .Where(wall => !drawn.Contains(wall.Id) && wall.get_BoundingBox(null) is not null)
                .Select(wall => (Wall: wall, Z: Local(wall.get_BoundingBox(null)!, inverse)))
                .ToList();
            var behind = candidates.FirstOrDefault(pair => pair.Z.Min >= box.Max.Z + 1).Wall;
            if (behind is null)
            {
                Assert.Inconclusive($"No wall lies behind the section line of '{View.Name}'.");
                return;
            }

            ExpectFirst(Explain(CurrentOf(behind)), HiddenReasons.BehindSectionLine);
            if (View.get_Parameter(BuiltInParameter.VIEWER_BOUND_FAR_CLIPPING)?.AsInteger() is not (null or 0)
                && candidates.FirstOrDefault(pair => pair.Z.Max <= box.Min.Z - 1).Wall is { } beyond)
            {
                ExpectFirst(Explain(CurrentOf(beyond)), HiddenReasons.BeyondFarClip);
            }
        });
    }

    private static (double Min, double Max) Local(BoundingBoxXYZ box, Transform inverse)
    {
        var z = new[] { box.Min.X, box.Max.X }
            .SelectMany(x => new[] { box.Min.Y, box.Max.Y }.SelectMany(y => new[] { box.Min.Z, box.Max.Z }
                .Select(depth => inverse.OfPoint(box.Transform.OfPoint(new XYZ(x, y, depth))).Z)))
            .ToList();
        return (z.Min(), z.Max());
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
            ExpectAny(Explain(found.Element), HiddenCause.DesignOption);
        });
    }

    [Test]
    public void VisibleElementHasNoCause()
    {
        Record(Case("WH-06", "Element visible in the active view", HiddenReasons.None.Text,
            $"Pick an element that the active view shows; Why hidden reads '{HiddenReasons.None.Text}'."), () =>
        {
            var result = Explain(CurrentOf(VisibleElement()));
            Evidence.Values(HiddenReasons.None.Text, Describe(result));
            Assert.That(result.Reasons, Is.EqualTo(new[] { HiddenReasons.None }));
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

    private static WhyHidden Explain(ElementRef element)
    {
        var result = Evidence.Time("why hidden", () => Adapter.Run(Explainer.ExplainAsync(element, CancellationToken.None)));
        Evidence.Step($"Why hidden on {element}: {Describe(result)} in {Math.Round(result.ElapsedMs, 1)} ms.");
        return result;
    }

    private static void ExpectFirst(WhyHidden result, HiddenReason expected)
    {
        Evidence.Values(expected.Text, Describe(result));
        Assert.That(result.First, Is.EqualTo(expected));
    }

    private static void ExpectAny(WhyHidden result, HiddenCause cause)
    {
        Evidence.Values(cause.ToString(), Describe(result));
        Assert.That(result.Reasons.Select(reason => reason.Cause), Does.Contain(cause));
    }

    private static string Describe(WhyHidden result)
    {
        return string.Join("; ", result.Reasons.Select(reason => reason.CanShow ? $"{reason.Text} · Show" : reason.Text));
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
        var view = CurrentView.InfoOf(Model, View);
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

    private static List<ElementRef> FindRailingParts()
    {
        const int Wanted = 10;
        var found = new List<ElementRef>();
        var parts = new ElementMulticategoryFilter([BuiltInCategory.OST_StairsRailingBaluster, BuiltInCategory.OST_RailingSupport]);
        foreach (var instance in LoadedLinks())
        {
            if (LinkElements.InView(Model, View, instance.Id) is not { } collector)
            {
                continue;
            }

            var drawn = new HashSet<ElementId>(collector.ToElementIds());
            var document = instance.GetLinkDocument()!;
            var railings = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_StairsRailing)
                .WhereElementIsNotElementType()
                .Where(railing => drawn.Contains(railing.Id));
            foreach (var railing in railings)
            {
                var part = railing.GetDependentElements(parts).Where(id => !drawn.Contains(id)).Select(document.GetElement)
                    .FirstOrDefault(candidate => RailingParts.RailingOf(candidate) is { } parent && drawn.Contains(parent.Id));
                if (part is null)
                {
                    continue;
                }

                Evidence.Step($"{instance.Name}: {part.Category!.Name} {part.Id} is left out of '{View.Name}'; its railing is drawn.");
                found.Add(new ElementRef(DocumentRegistry.KeyOf(Model, instance), part.Id.ToLong()));
                if (found.Count == Wanted)
                {
                    return found;
                }
            }
        }

        return found;
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
