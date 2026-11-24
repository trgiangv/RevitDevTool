using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Visibility;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class CategoryActionTests : RecordedTest
{
    private RevitViewActions _actions = null!;

    private static CategoryActionSetup Data => Areas.CategoryActions.Setup;

    private static Document Host => SampleModel.Host;

    private static View View => SampleViews.Active.ActiveView;

    private static long Parent => Cases.Category(Data.Parent);

    private static long Child => Cases.Category(Data.Child);

    private static long Walls => Cases.Category(Data.HostViewCategory);

    [OneTimeSetUp]
    public void Open()
    {
        _actions = new RevitViewActions();
    }

    [Test]
    public void SubcategoryCollected()
    {
        var item = Case("CA-01");
        Record(item, () =>
        {
            var site = SampleModel.Site;
            var oracle = (Parent: Oracle.Candidates(site.Document, [Parent]).Count(), Child: Oracle.Candidates(site.Document, [Child]).Count());
            var snapshot = Evidence.Time("panel", () => Adapter.Run(new Adapter().Source.BuildSnapshotAsync(site.Source, null, CancellationToken.None)));
            var child = snapshot.Groups[snapshot.GroupIndexOf(Child)];
            var panel = (Parent: CountOf(snapshot, Parent), Child: CountOf(snapshot, Child));
            Evidence.Step($"Built the snapshot of {site.Document.Title} and counted the Toposolid and Sub-divisions groups.");
            Evidence.Values($"{oracle.Parent} + {oracle.Child}", $"{panel.Parent} + {panel.Child}, child '{snapshot.Names[child.NameIndex]}' under {child.ParentId}");

            Assert.That(oracle, Is.EqualTo((item.Expected.GetProperty("parent").GetInt32(), item.Expected.GetProperty("child").GetInt32())), "oracle");
            Assert.That(panel, Is.EqualTo(oracle), "panel");
            Assert.That(child.ParentId, Is.EqualTo(Parent), "parent id");
            Assert.That(snapshot.Names[child.NameIndex], Is.EqualTo(Category.GetCategory(site.Document, Child.ToElementId())!.Name));
        });
    }

    [Test]
    public void NoisySubcategoriesLeftOut()
    {
        Record(Case("CA-10"), () =>
        {
            long[] noisy = [(long)BuiltInCategory.OST_Site, (long)BuiltInCategory.OST_Lines, (long)BuiltInCategory.OST_Topography];
            var found = new List<string>();
            foreach (var document in new[] { SampleModel.Current, SampleModel.Site })
            {
                var snapshot = Evidence.Time("panel", () => Adapter.Run(new Adapter().Source.BuildSnapshotAsync(document.Source, null, CancellationToken.None)));
                found.AddRange(snapshot.Groups.Where(group => noisy.Contains(group.ParentId))
                    .Select(group => $"{document.Label}: {snapshot.Names[group.NameIndex]}"));
            }

            Evidence.Step("Built the snapshots of the Current model and the Site link and listed groups under Site, Lines or Topography.");
            Evidence.Values("none", found.Count == 0 ? "none" : string.Join(", ", found));

            Assert.That(found, Is.Empty);
        });
    }

    [Test]
    public void BoxHideReachesLinks()
    {
        Record(Case("CA-02"), () =>
        {
            RequireThreeD();
            var name = NameOf(Walls);
            var before = LinkCategoryCounts.Read(Host, View);
            Shot("before");
            Execute(VisibilityAction.Hide, null, Walls);
            var after = LinkCategoryCounts.Read(Host, View);
            var properties = IsProperties(View);
            Shot("after");
            Reset();
            Shot("reset");
            var reset = LinkCategoryCounts.Read(Host, View);
            Evidence.Values($"{name} {before.Of(name):N0} -> 0, then {before}", $"{name} {after.Of(name):N0}, properties {properties}, then {reset}");

            Assert.That(before.Of(name), Is.GreaterThan(0), "The view shows no wall.");
            Assert.That(after.Of(name), Is.Zero, "Walls stayed in a view collector.");
            Assert.That(properties, Is.True, "The hide did not use Temporary View Properties.");
            Assert.That(reset.SameAs(before), Is.True, "Reset did not restore the counts.");
        });
    }

    [Test]
    public void ParentHideCoversChild()
    {
        var item = Case("CA-03");
        Record(item, () =>
        {
            RequireThreeD();
            var site = SampleModel.Site;
            var before = InView(site, Parent) + InView(site, Child);
            Shot("before");
            Execute(VisibilityAction.Hide, null, Parent, Child);
            var after = InView(site, Parent) + InView(site, Child);
            Shot("after");
            Reset();
            Shot("reset");
            var reset = InView(site, Parent) + InView(site, Child);
            Evidence.Values(item.ExpectedText, $"before {before}, after {after}, reset {reset}");

            Assert.That((before, after), Is.EqualTo((item.Expected.GetProperty("before").GetInt32(), item.Expected.GetProperty("after").GetInt32())));
            Assert.That(reset, Is.EqualTo(before), "Reset did not bring the Site elements back.");
        });
    }

    [Test]
    public void HiddenParentHidesChild()
    {
        var item = Case("CA-04");
        Record(item, () =>
        {
            RequireThreeD();
            var site = SampleModel.Site;
            Execute(VisibilityAction.Hide, null, Parent);
            var childHidden = View.GetCategoryHidden(Child.ToElementId());
            var child = InView(site, Child);
            Reset();
            Evidence.Step("Hid Toposolid only (category hide inside Temporary View Properties); Sub-divisions stayed on in the view's own settings.");
            Evidence.Values(item.ExpectedText, $"child {child}, child category hidden {childHidden}");

            Assert.That(childHidden, Is.False, "The child category itself was hidden.");
            Assert.That(child, Is.EqualTo(item.Expected.GetProperty("child").GetInt32()));
        });
    }

    [Test]
    public void BoxColorIsCategoryOverride()
    {
        Record(Case("CA-05"), () =>
        {
            RequireThreeD();
            Shot("before");
            Execute(VisibilityAction.Color, Data.Color, Walls);
            var colored = Matches(View.GetCategoryOverrides(Walls.ToElementId()), Data.Color);
            var properties = IsProperties(View);
            Shot("after");
            Reset();
            Shot("reset");
            var left = Matches(View.GetCategoryOverrides(Walls.ToElementId()), Data.Color);
            Evidence.Values($"Walls {Data.Color}, properties on; none after Reset", $"colored {colored}, properties {properties}; after Reset {left}");

            Assert.That((colored, properties), Is.EqualTo((true, true)));
            Assert.That(left, Is.False, "Reset left the category color.");
        });
    }

    [Test]
    public void KeepLeavesCategoryHide()
    {
        Record(Case("CA-06"), () =>
        {
            RequireThreeD();
            var name = NameOf(Walls);
            var before = LinkCategoryCounts.Read(Host, View);
            Execute(VisibilityAction.Hide, null, Walls);
            Adapter.Run(_actions.KeepAsync(null, CancellationToken.None));
            var kept = LinkCategoryCounts.Read(Host, View);
            var properties = IsProperties(View);
            Reset();
            var reset = LinkCategoryCounts.Read(Host, View);
            Evidence.Values($"after Keep: properties on, {name} 0; after Reset {before}", $"after Keep: properties {properties}, {name} {kept.Of(name)}; after Reset {reset}");

            Assert.That((properties, kept.Of(name)), Is.EqualTo((true, 0)));
            Assert.That(reset.SameAs(before), Is.True, "Reset did not restore the counts.");
        });
    }

    [Test]
    public void OwnSettingsLinkNotice()
    {
        Record(Case("CA-07"), () =>
        {
            RequireThreeD();
#if REVIT2024_OR_GREATER
            var link = SampleModel.Architectural.Key.LinkInstanceId.ToElementId();
            var selection = SampleViews.Active.Selection.GetElementIds();
            using var group = new TransactionGroup(Host, "Model Context live: link Custom");
            group.Start();
            try
            {
                using (var transaction = new Transaction(Host, "Link Custom"))
                {
                    transaction.Start();
                    View.SetLinkOverrides(link, new RevitLinkGraphicsSettings { LinkVisibilityType = LinkVisibility.Custom });
                    transaction.Commit();
                }

                Evidence.Step("Set the Architectural link to Custom in the active view, inside a transaction group that is rolled back.");
                var outcome = Execute(VisibilityAction.Hide, null, Walls);
                Reset();
                Evidence.Values(VisibilityText.LinksKeepOwnSettings, outcome.Notice ?? "(none)");

                Assert.That(outcome.Notice, Is.EqualTo(VisibilityText.LinksKeepOwnSettings));
            }
            finally
            {
                group.RollBack();
                SampleViews.Active.Selection.SetElementIds(selection);
                Evidence.Step("Rolled back the group and put back the selection the rollback cleared.");
            }
#else
            Assert.Inconclusive("Link display overrides need Revit 2024 or later.");
#endif
        });
    }

    [Test]
    public void ParentLinkFilterHasBoth()
    {
        Record(Case("CA-08"), () =>
        {
            RequireThreeD();
            Shot("before");
            Execute(VisibilityAction.Color, Data.Color, SiteFilter([Parent, Child]));
            var filters = PanelFilters();
            var categories = filters.Count == 1 ? filters[0].GetCategories().Select(id => id.ToLong()).OrderBy(id => id).ToList() : [];
            Shot("after");
            Reset();
            var left = PanelFilters().Count;
            Evidence.Values($"one filter with {Parent}, {Child}; none after Reset", $"{filters.Count} filter(s) with {string.Join(", ", categories)}; {left} after Reset");

            Assert.That(categories, Is.EquivalentTo(new[] { Parent, Child }));
            Assert.That(left, Is.Zero, "Reset left a Model Context view filter.");
        });
    }

    [Test]
    public void LatestFilterOnTop()
    {
        Record(Case("CA-09"), () =>
        {
            RequireThreeD();
            Execute(VisibilityAction.Color, Data.ChildColor, SiteFilter([Child]));
            Execute(VisibilityAction.Color, Data.Color, SiteFilter([Parent, Child]));
            var afterParent = TopCategoryCount();
            Execute(VisibilityAction.Color, Data.ChildColor, SiteFilter([Child]));
            var afterChild = TopCategoryCount();
            Shot("after");
            Reset();
            var left = PanelFilters().Count;
            Evidence.Values("top after parent: 2 categories; after child again: 1; none after Reset",
                $"top after parent: {afterParent}; after child again: {afterChild}; {left} after Reset");

            Assert.That((afterParent, afterChild), Is.EqualTo((2, 1)));
            Assert.That(left, Is.Zero, "Reset left a Model Context view filter.");
        });
    }

    private static CaseItem Case(string id)
    {
        return Areas.Find(Areas.CategoryActions.Cases, id);
    }

    private static void RequireThreeD()
    {
        if (View.Name != CaseFiles.Facts.ThreeDViewName)
        {
            Assert.Inconclusive($"Make '{CaseFiles.Facts.ThreeDViewName}' the active view in a run of its own first (Gotcha 21).");
        }
    }

    private PlanOutcome Execute(VisibilityAction action, RgbData? color, params long[] categories)
    {
        var steps = categories.Select(id => (PlanStep)new CategoryStep(id, NameOf(id))).ToList();
        return Execute(action, color, steps);
    }

    private PlanOutcome Execute(VisibilityAction action, RgbData? color, LinkFilterStep step)
    {
        return Execute(action, color, [step]);
    }

    private PlanOutcome Execute(VisibilityAction action, RgbData? color, List<PlanStep> steps)
    {
        var plan = new VisibilityPlan(action, color is null ? null : new RgbColor(color.R, color.G, color.B), steps, null);
        var outcome = Evidence.Time("panel", () => Adapter.Run(_actions.ExecuteAsync(plan, CancellationToken.None)));
        Evidence.Step($"Sent {action} on {steps.Count} step(s) to RevitViewActions.ExecuteAsync.");
        return outcome;
    }

    private void Reset()
    {
        Adapter.Run(_actions.ResetAsync(null, CancellationToken.None));
        Evidence.Step("Reset: RevitViewActions.ResetAsync.");
    }

    private static LinkFilterStep SiteFilter(IReadOnlyList<long> categories)
    {
        var site = SampleModel.Site;
        var ids = Oracle.Candidates(site.Document, categories).Select(element => element.Id.ToLong()).OrderBy(id => id).ToList();
        return new LinkFilterStep(site.Key, ids, new FilterDefinition([.. categories.OrderBy(id => id)], FilterSet.All()));
    }

    private static List<ParameterFilterElement> PanelFilters()
    {
        return View.GetOrderedFilters()
            .Select(id => Host.GetElement(id))
            .OfType<ParameterFilterElement>()
            .Where(filter => LinkViewFilters.IsPanelName(filter.Name))
            .ToList();
    }

    private static int TopCategoryCount()
    {
        return PanelFilters().FirstOrDefault()?.GetCategories().Count ?? 0;
    }

    private static int InView(SampleDocument link, long category)
    {
        var collector = LinkElements.InView(Host, View, link.Key.LinkInstanceId.ToElementId());
        return collector is null
            ? 0
            : collector.WhereElementIsNotElementType().Count(element => element.Category?.Id.ToLong() == category);
    }

    private static int CountOf(DocumentSnapshot snapshot, long category)
    {
        return snapshot.Rows.Count(row => !row.SecondaryOption && snapshot.Groups[row.GroupIndex].GroupId == category);
    }

    private static string NameOf(long category)
    {
        return Category.GetCategory(Host, category.ToElementId())?.Name ?? category.ToString();
    }

    private static bool IsProperties(View view)
    {
        return view.TemporaryViewModes?.IsModeActive(TemporaryViewMode.TemporaryViewProperties) == true;
    }

    private static bool Matches(OverrideGraphicSettings settings, RgbData color)
    {
        var line = settings.ProjectionLineColor;
        return line.IsValid && line.Red == color.R && line.Green == color.G && line.Blue == color.B;
    }

    private static void Shot(string label)
    {
        if (Evidence.Images)
        {
            Evidence.Image(label, path => ViewImage.Export(Host, View, path));
        }
    }
}
