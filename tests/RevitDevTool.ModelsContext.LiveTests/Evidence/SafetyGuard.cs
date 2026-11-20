using Nice3point.Revit.Extensions;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class SafetyState
{
    public Dictionary<string, bool> Modified { get; set; } = [];

    public string? ActiveModel { get; set; }

    public long ActiveView { get; set; }

    public List<string> Modes { get; set; } = [];

    public List<long> Selection { get; set; } = [];
}

internal static class SafetyGuard
{
    public const string BaselineFile = "safety-baseline.json";
    public const string EndFile = "safety-host-end.json";

    private static SafetyState? _baseline;

    public static void Start()
    {
        if (_baseline is not null)
        {
            return;
        }

        _baseline = Capture();
        File.WriteAllText(Path.Combine(Evidence.Folder, BaselineFile), JsonSerializer.Serialize(_baseline, CaseFiles.Options));
    }

    public static void Observe()
    {
        Report(Check(includeSelection: true));
    }

    public static void Verify(bool writesView = false)
    {
        var violations = Check(includeSelection: true, includeModified: !writesView);
        Report(violations);
        if (violations.Count > 0)
        {
            Assert.Fail("Safety guard: " + string.Join("; ", violations));
        }
    }

    public static List<string> CheckAgainst(string folder, bool includeSelection)
    {
        var path = Path.Combine(folder, BaselineFile);
        if (!File.Exists(path))
        {
            return [$"No {BaselineFile} in {folder}; run L2 first."];
        }

        var baseline = JsonSerializer.Deserialize<SafetyState>(File.ReadAllText(path), CaseFiles.Options)!;
        return Compare(baseline, Capture(), includeSelection, includeModified: false);
    }

    public static SafetyState Current()
    {
        return Capture();
    }

    private static List<string> Check(bool includeSelection, bool includeModified = true)
    {
        var baseline = _baseline ?? Capture();
        var now = Capture();
        var violations = Compare(baseline, now, includeSelection, includeModified);
        if (violations.Count > 0 || !includeModified)
        {
            _baseline = now;
        }

        return violations;
    }

    private static void Report(List<string> violations)
    {
        Evidence.Safety(violations.Count == 0
            ? "ok: no document modified, no temporary mode left, selection unchanged"
            : "VIOLATION: " + string.Join("; ", violations));
    }

    private static List<string> Compare(SafetyState baseline, SafetyState now, bool includeSelection, bool includeModified = true)
    {
        var violations = new List<string>();
        foreach (var (name, modified) in now.Modified)
        {
            if (includeModified && modified && baseline.Modified.TryGetValue(name, out var was) && !was)
            {
                violations.Add($"{name} became modified");
            }
        }

        var sameView = baseline.ActiveModel == now.ActiveModel && baseline.ActiveView == now.ActiveView;
        var left = sameView ? now.Modes.Except(baseline.Modes).ToList() : now.Modes;
        if (left.Count > 0)
        {
            violations.Add($"temporary mode left in the active view: {string.Join(", ", left)}");
        }

        if (includeSelection && baseline.ActiveModel == now.ActiveModel && !baseline.Selection.SequenceEqual(now.Selection))
        {
            violations.Add($"selection changed from {baseline.Selection.Count} to {now.Selection.Count} elements");
        }

        return violations;
    }

    private static SafetyState Capture()
    {
        var application = SampleModel.Application;
        var state = new SafetyState();
        foreach (Document document in application.Application.Documents)
        {
            state.Modified[Name(document)] = document.IsModified;
        }

        if (application.ActiveUIDocument is { } active)
        {
            state.ActiveModel = Name(active.Document);
            state.ActiveView = active.ActiveView.Id.ToLong();
            state.Selection = [.. active.Selection.GetElementIds().Select(id => id.ToLong()).OrderBy(id => id)];
            state.Modes = Modes(active.ActiveView);
        }

        return state;
    }

    private static List<string> Modes(View view)
    {
        var modes = new List<string>();
        if (view.IsTemporaryHideIsolateActive())
        {
            modes.Add(nameof(TemporaryViewMode.TemporaryHideIsolate));
        }

        if (view.IsInTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties))
        {
            modes.Add(nameof(TemporaryViewMode.TemporaryViewProperties));
        }

        if (view.IsInTemporaryViewMode(TemporaryViewMode.RevealHiddenElements))
        {
            modes.Add(nameof(TemporaryViewMode.RevealHiddenElements));
        }

        return modes;
    }

    private static string Name(Document document)
    {
        return string.IsNullOrEmpty(document.PathName) ? document.Title : document.PathName;
    }
}
