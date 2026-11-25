using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Visibility;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class VisibilityTests : RecordedTest
{
    private static readonly BuiltInCategory[] Datums = [BuiltInCategory.OST_Levels, BuiltInCategory.OST_VolumeOfInterest];

    public static IEnumerable<TestCaseData> Rows => Areas.Rows(Areas.Visibility);

    [OneTimeSetUp]
    public void Open()
    {
        VisibilityRun.Open();
    }

    [TestCaseSource(nameof(Rows))]
    public void VisibilityMatchesApi(string id)
    {
        var item = Areas.Find(Areas.Visibility, id);
        Record(item, () => VisibilityRun.Check(item), writesView: true);
    }

    [Test]
    public void IsolateRestHoldsLinkedDatums()
    {
        Record(new CaseItem
        {
            Id = "VI-21",
            Description = "T3 link isolate rest: for every loaded link '{3D}' draws, the rest sent to Hide in View holds every linked Level and Scope Box and no Model Group. L2 checks the plan only",
            Expected = JsonSerializer.SerializeToElement("every drawn linked Level and Scope Box is in the rest; no Model Group"),
            ManualSteps = ["Activate '{3D}'. Explorer tab: Isolate on Pipe Fittings under the Plumbing link: only the pipe fittings of that link stay; its Levels and Scope Boxes are hidden too. Click Reset."]
        }, () =>
        {
            SampleViews.Switch(SampleViews.ThreeD);
            var model = SampleModel.Host;
            var view = SampleViews.Active.ActiveView;
            var outcomes = new List<string>();
            var problems = new List<string>();
            foreach (var link in SampleModel.All.Where(document => !document.Key.IsCurrentModel))
            {
                Check(model, view, link, outcomes, problems);
            }

            Evidence.Values("every drawn linked Level and Scope Box is in the rest; no Model Group", string.Join("; ", outcomes));
            Assert.That(outcomes, Is.Not.Empty, "'{3D}' draws no linked Level or Scope Box.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }, writesView: true);
    }

    private static void Check(Document model, View view, SampleDocument link, List<string> outcomes, List<string> problems)
    {
        if (LinkElements.InView(model, view, link.Key.LinkInstanceId.ToElementId()) is not { } collector)
        {
            return;
        }

        var drawn = collector.WhereElementIsNotElementType().ToElements();
        var kept = drawn.FirstOrDefault(element => element.Category?.CategoryType == CategoryType.Model && element is not Group);
        var datums = drawn.Where(element => element.Category?.BuiltInCategory is { } category && Datums.Contains(category))
            .Select(element => element.Id.ToLong())
            .ToList();
        if (kept is null || datums.Count == 0)
        {
            return;
        }

        var rest = new HashSet<long>(LinkIsolation.RestOf(model, view, [new LinkIsolateStep(link.Key, [kept.Id.ToLong()])])
            .Select(element => element.Id));
        var missed = datums.Where(id => !rest.Contains(id)).ToList();
        var groups = drawn.OfType<Group>().Count(group => rest.Contains(group.Id.ToLong()));
        outcomes.Add($"{link.Label}: {datums.Count - missed.Count}/{datums.Count} datums in a rest of {rest.Count}, {groups} groups");
        Evidence.Step($"{link.Label}: kept '{kept.Category!.Name}' {kept.Id}; rest {rest.Count}; Levels and Scope Boxes {datums.Count}, missed {missed.Count}; groups in the rest {groups}.");
        if (missed.Count > 0)
        {
            problems.Add($"{link.Label}: Levels or Scope Boxes {string.Join(", ", missed)} are not in the rest");
        }

        if (groups > 0)
        {
            problems.Add($"{link.Label}: {groups} Model Groups are in the rest");
        }
    }
}
