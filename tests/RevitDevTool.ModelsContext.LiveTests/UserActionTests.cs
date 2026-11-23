using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Visibility;
using System.Text.Json;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class UserActionSetup
{
    public string Category { get; set; } = string.Empty;

    public string LinkCategory { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public string FamilyParameter { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int Edited { get; set; }

    public int Deleted { get; set; }

    public double CopyOffsetFeet { get; set; }

    public int Repeats { get; set; }

    public string ReloadLink { get; set; } = string.Empty;
}

internal sealed class UserActionCase : CaseItem
{
    public string? ViewKind { get; set; }
}

internal sealed class ReloadStep
{
    public string Link { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string PreviousPath { get; set; } = string.Empty;

    public string VersionBefore { get; set; } = string.Empty;

    public string VersionAfter { get; set; } = string.Empty;

    public int ElementsBefore { get; set; }

    public int Changed { get; set; }
}

[TestFixture]
public sealed class UserActionTests : RecordedTest
{
    private static readonly Lazy<CaseFile<UserActionSetup, UserActionCase>> LazyData =
        new(() => CaseFiles.Read<CaseFile<UserActionSetup, UserActionCase>>("user-actions.json"));

    private static CaseFile<UserActionSetup, UserActionCase> Data => LazyData.Value;

    private static UserActionSetup Setup => Data.Setup;

    [Test]
    public void EditedValueRecounts()
    {
        Record(Areas.Find(Data.Cases, "US-01"), () =>
        {
            var adapter = new Adapter();
            var documents = SampleModel.Of("Both");
            var key = Keys.Of(Setup.Parameter);
            var definition = Ducts(new RuleNode(key, FilterOperator.Equal, Setup.Value));
            var before = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count before");

            using var edit = new RolledBack("Model Context user edit");
            foreach (var element in DuctElements().Take(Setup.Edited))
            {
                Oracle.Find(element, key)!.Set(Setup.Value);
            }

            Evidence.Step($"Set {Setup.Parameter} = '{Setup.Value}' on {Setup.Edited} ducts in a transaction that is rolled back.");
            adapter.Cache.InvalidateValues(SampleModel.Current.Source);
            var after = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count after edit");
            Evidence.Values($"0 before, {Setup.Edited} after", $"{before} before, {after} after");

            Assert.That((before, after), Is.EqualTo((0, Setup.Edited)));
        });
    }

    [Test]
    public void CopiedDuctRecounts()
    {
        Record(Areas.Find(Data.Cases, "US-02"), () =>
        {
            var adapter = new Adapter();
            var documents = SampleModel.Of("Both");
            var definition = Ducts();
            var before = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count before");

            using var edit = new RolledBack("Model Context user copy");
            ElementTransformUtils.CopyElement(SampleModel.Host, DuctElements().First().Id, new XYZ(0, 0, Setup.CopyOffsetFeet));
            Evidence.Step($"Copied one duct {Setup.CopyOffsetFeet} ft up in a transaction that is rolled back.");
            adapter.Cache.InvalidateValues(SampleModel.Current.Source);
            var after = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count after copy");
            Evidence.Values($"{before}, then {before + 1}", $"{before}, then {after}");

            Assert.That(after, Is.EqualTo(before + 1));
        });
    }

    [Test]
    public void DeletedDuctsRecount()
    {
        Record(Areas.Find(Data.Cases, "US-03"), () =>
        {
            var adapter = new Adapter();
            var documents = SampleModel.Of("Both");
            var definition = Ducts();
            var before = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count before");

            using var edit = new RolledBack("Model Context user delete");
            var removed = SampleModel.Host.Delete([.. DuctElements().Take(Setup.Deleted).Select(element => element.Id)]);
            Evidence.Step($"Deleted {Setup.Deleted} ducts ({removed.Count} elements with dependents) in a transaction that is rolled back.");
            adapter.Cache.InvalidateValues(SampleModel.Current.Source);
            var after = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count after delete");
            Evidence.Values($"{before}, then fewer", $"{before}, then {after}");

            Assert.That(after, Is.LessThan(before));
        });
    }

    [Test]
    public void ChangedTypeRecounts()
    {
        Record(Areas.Find(Data.Cases, "US-04"), () =>
        {
            if (Retypable() is not { } found)
            {
                Assert.Inconclusive("No duct has a valid type of another family.");
                return;
            }

            var (element, target, family) = found;
            var adapter = new Adapter();
            var documents = SampleModel.Of("Both");
            var definition = Ducts(new RuleNode(Keys.Of(Setup.FamilyParameter), FilterOperator.Equal, family));
            var before = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count before");

            using var edit = new RolledBack("Model Context user type change");
            element.ChangeTypeId(target);
            Evidence.Step($"Changed one duct from family '{family}' to a type of another family in a transaction that is rolled back.");
            adapter.Cache.InvalidateValues(SampleModel.Current.Source);
            var after = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count after type change");
            Evidence.Values($"{before}, then {before - 1}", $"{before}, then {after}");

            Assert.That(after, Is.EqualTo(before - 1));
        });
    }

    [Test]
    public void UndoneEditRecounts()
    {
        Record(Areas.Find(Data.Cases, "US-05"), () =>
        {
            var adapter = new Adapter();
            var documents = SampleModel.Of("Both");
            var key = Keys.Of(Setup.Parameter);
            var definition = Ducts(new RuleNode(key, FilterOperator.Equal, Setup.Value));
            var before = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count before");

            int edited;
            int undone;
            var group = new TransactionGroup(SampleModel.Host, "Model Context user undo");
            group.Start();
            try
            {
                using (var edit = new Transaction(SampleModel.Host, "Model Context user edit"))
                {
                    edit.Start();
                    foreach (var element in DuctElements().Take(Setup.Edited))
                    {
                        Oracle.Find(element, key)!.Set(Setup.Value);
                    }

                    edit.Commit();
                }

                Evidence.Step($"Committed the edit of {Setup.Edited} ducts inside a transaction group.");
                adapter.Cache.InvalidateValues(SampleModel.Current.Source);
                edited = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count after edit");
            }
            finally
            {
                if (group.HasStarted())
                {
                    group.RollBack();
                }

                group.Dispose();
            }

            Evidence.Step("Rolled the group back (the API form of Undo). Redo cannot run inside a run; the Revit Undo and Redo commands are the MCP follow-up US-13.");
            adapter.Cache.InvalidateValues(SampleModel.Current.Source);
            undone = Recount(adapter, definition, documents, ScopeKind.WholeModel, "count after undo");
            Evidence.Values($"0, {Setup.Edited}, 0", $"{before}, {edited}, {undone}");

            Assert.That((before, edited, undone), Is.EqualTo((0, Setup.Edited, 0)));
        });
    }

    [Test]
    [Explicit("Run alone after the MCP step scripts/models-context/user-action-activate-view.cs: it checks the view that is active now and never switches views.")]
    public void ActiveViewFollows()
    {
        var view = SampleViews.Active.ActiveView;
        var kind = KindOf(view);
        var item = Data.Cases.FirstOrDefault(candidate => candidate.ViewKind == kind);
        if (item is null)
        {
            Assert.Inconclusive($"The active view '{view.Name}' ({view.ViewType}) is not a plan, 3D, sheet, schedule, drafting or legend view.");
            return;
        }

        Record(item, () =>
        {
            var host = SampleModel.Host;
            var showsLinks = kind is "Plan" or "ThreeD";
            var adapter = new Adapter();
            var all = new FilterDefinition([], FilterSet.All());
            var current = SampleModel.Current;
            var architectural = SampleModel.Architectural;
            var hostCount = Recount(adapter, all, [current], ScopeKind.ActiveView, "Current model in view");
            var ids = Evidence.Time("view ids", () => Adapter.Run(adapter.Source.CollectScopeAsync(current.Key, ScopeKind.ActiveView, CancellationToken.None)));
            Assert.That(Oracle.ViewIds(current).SetEquals(ids), Is.True, "The Current model view id set differs from the view collector.");

            var collector = LinkElements.InView(host, view, architectural.Key.LinkInstanceId.ToElementId());
            if (showsLinks)
            {
                Assert.That(collector, Is.Not.Null, "A plan or 3D view must give the link collector.");
                var linkCount = Recount(adapter, all, [architectural], ScopeKind.ActiveView, "link in view");
                Evidence.Values($"{hostCount} in the Current model", $"{hostCount} in the Current model, {linkCount} in the link");
                return;
            }

            Assert.That(collector, Is.Null, "A view that does not draw links must not build the link collector.");
            var none = Evidence.Time("link in view", () => adapter.Count(all, [architectural], ScopeKind.ActiveView));
            Evidence.Step($"The active {kind} view '{view.Name}': LinkElements.InView returned null; the panel counted {none} link elements.");
            Evidence.Values($"{hostCount} in the Current model, 0 in the link", $"{hostCount} in the Current model, {none} in the link");
            Assert.That(none, Is.Zero);
        });
    }
    [Test]
    public void RepeatedRequestsLastWins()
    {
        Record(Areas.Find(Data.Cases, "US-12"), () =>
        {
            var adapter = new Adapter();
            var known = SampleModel.KnownSources;
            var documents = new[] { SampleModel.Current, SampleModel.Architectural };
            var categories = new[] { Cases.Category(Setup.Category), Cases.Category(Setup.LinkCategory) };
            var tasks = new List<Task<IReadOnlyCollection<long>>>();
            CancellationTokenSource? previous = null;
            var sources = new List<CancellationTokenSource>();
            Evidence.Time("requests", () =>
            {
                for (var index = 0; index < Setup.Repeats; index++)
                {
                    previous?.Cancel();
                    previous = new CancellationTokenSource();
                    sources.Add(previous);
                    var definition = new FilterDefinition([categories[index % 2]], FilterSet.All());
                    var document = documents[index % 2];
                    var root = FilterScopes.ForDocument(definition.Root, document.Source, known)!;
                    tasks.Add(adapter.Evaluator.EvaluateAsync(document.Key, definition with { Root = root }, null, previous.Token));
                }
            });

            var canceled = 0;
            foreach (var task in tasks)
            {
                try
                {
                    Adapter.Run(task);
                }
                catch (Exception exception) when (exception is OperationCanceledException or AggregateException { InnerException: OperationCanceledException })
                {
                    canceled++;
                }
            }

            Evidence.Step($"Sent {Setup.Repeats} requests, each canceling the one before; {canceled} ended canceled, {Setup.Repeats - canceled} completed.");
            var lastIndex = Setup.Repeats - 1;
            var last = documents[lastIndex % 2];
            var lastDefinition = new FilterDefinition([categories[lastIndex % 2]], FilterSet.All());
            var lastCount = Adapter.Run(tasks[lastIndex]).Count;
            var oracle = Oracle.Count(lastDefinition, [last], ScopeKind.WholeModel);
            var fresh = Recount(new Adapter(), lastDefinition, [last], ScopeKind.WholeModel, "fresh count");
            Evidence.Values($"{oracle} for the last request", $"{lastCount} for the last request, {fresh} fresh");

            Assert.That(lastCount, Is.EqualTo(oracle), "The last request did not win.");
            Assert.That(fresh, Is.EqualTo(oracle));
        });
    }

    [Test]
    [Explicit("Run after the MCP step scripts/models-context/user-action-reload.cs: it reads reload-step.json that step wrote.")]
    public void ReloadedLinkMatchesOracle()
    {
        Record(Areas.Find(Data.Cases, "US-15"), () =>
        {
            var adapter = new Adapter();
            var all = new FilterDefinition([], FilterSet.All());
            var documents = SampleModel.All;
            var reload = documents.FirstOrDefault(document => document.Label.StartsWith(Setup.ReloadLink, StringComparison.OrdinalIgnoreCase));
            if (reload is null)
            {
                Assert.Inconclusive($"The link '{Setup.ReloadLink}' is not loaded in the Current model; run the reload setup first.");
                return;
            }

            var panel = new List<string>();
            var oracle = new List<string>();
            var different = new List<string>();
            foreach (var document in documents)
            {
                var panelCount = Evidence.Time("panel " + document.Label, () => adapter.Count(all, [document], ScopeKind.WholeModel));
                var oracleCount = Evidence.Time("oracle " + document.Label, () => Oracle.Count(all, [document], ScopeKind.WholeModel));
                panel.Add($"{document.Label} {panelCount}");
                oracle.Add($"{document.Label} {oracleCount}");
                if (panelCount != oracleCount)
                {
                    different.Add(document.Label);
                }
            }

            Evidence.Values(string.Join("; ", oracle), string.Join("; ", panel));
            Assert.That(different, Is.Empty, $"Panel and oracle differ in {string.Join(", ", different)}.");

            var stepFile = Path.Combine(Evidence.WorkFolder(SampleModel.Application.Application.VersionNumber), "reload-step.json");
            if (!File.Exists(stepFile))
            {
                Assert.Inconclusive($"Counts match. No {stepFile}: run user-action-reload.cs to check the snapshot rule.");
                return;
            }

            var step = JsonSerializer.Deserialize<ReloadStep>(File.ReadAllText(stepFile), CaseFiles.Options)!;
            var expected = ExpectedOutcome(step);
            Evidence.Step($"Last reload step: {step.Link} from {step.Path}; version {(step.VersionBefore == step.VersionAfter ? "same" : "changed")}, {step.Changed} of {step.ElementsBefore} elements changed; the rule gives {expected}.");
            var line = ReloadLine(reload.Source.Label);
            if (line is null)
            {
                Assert.Inconclusive($"Counts match. No 'Snapshot reload {reload.Source.Label}' Trace line in the host log: set Logging level to Trace and keep the Model Context pane shown during the step.");
                return;
            }

            Evidence.Step("Panel Trace line: " + line);
            Assert.That(line, Does.Contain(expected), $"The panel's snapshot reload does not follow the version rule ({expected}).");
        });
    }

    private static string ExpectedOutcome(ReloadStep step)
    {
        if (step.VersionBefore == step.VersionAfter)
        {
            return nameof(ReloadOutcome.Kept);
        }

        return step.Changed < 0 || step.Changed > Math.Max(1, step.ElementsBefore) * VersionRule.RebuildShare
            ? nameof(ReloadOutcome.Rebuilt)
            : nameof(ReloadOutcome.Patched);
    }

    private static string? ReloadLine(string label)
    {
        var year = SampleModel.Application.Application.VersionNumber;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitDevTool", year, "Logs");
        var log = Directory.Exists(folder)
            ? new DirectoryInfo(folder).GetFiles("log_*").OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()
            : null;
        if (log is null)
        {
            return null;
        }

        using var stream = new FileStream(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n')
            .Where(line => line.Contains("Snapshot reload " + label, StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim())
            .LastOrDefault();
    }

    private static int Recount(Adapter adapter, FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope, string step)
    {
        var panel = Evidence.Time(step, () => adapter.Count(definition, documents, scope));
        var oracle = Oracle.Count(definition, documents, scope);
        Evidence.Step($"{step}: panel {panel}, oracle {oracle}.");
        Assert.That(panel, Is.EqualTo(oracle), $"{step}: the panel counts {panel}, the oracle {oracle}.");
        return panel;
    }

    private static FilterDefinition Ducts(params RuleNode[] rules)
    {
        return new FilterDefinition([Cases.Category(Setup.Category)], FilterSet.All(rules));
    }

    private static IEnumerable<Element> DuctElements()
    {
        return Oracle.Candidates(SampleModel.Host, [Cases.Category(Setup.Category)]);
    }

    private static (Element Element, ElementId Target, string Family)? Retypable()
    {
        var host = SampleModel.Host;
        var tried = new HashSet<long>();
        foreach (var element in DuctElements())
        {
            if (!tried.Add(element.GetTypeId().ToLong()) || host.GetElement(element.GetTypeId()) is not ElementType current)
            {
                continue;
            }

            foreach (var id in element.GetValidTypes())
            {
                if (host.GetElement(id) is ElementType other && other.FamilyName != current.FamilyName)
                {
                    return (element, id, current.FamilyName);
                }
            }
        }

        return null;
    }

    private static string? KindOf(View view)
    {
        return view.ViewType switch
        {
            ViewType.FloorPlan or ViewType.EngineeringPlan or ViewType.CeilingPlan or ViewType.AreaPlan => "Plan",
            ViewType.ThreeD => "ThreeD",
            ViewType.DrawingSheet => "Sheet",
            ViewType.Schedule => "Schedule",
            ViewType.DraftingView => "Drafting",
            ViewType.Legend => "Legend",
            _ => null
        };
    }

    private sealed class RolledBack : IDisposable
    {
        private readonly Transaction _transaction;

        public RolledBack(string name)
        {
            _transaction = new Transaction(SampleModel.Host, name);
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
