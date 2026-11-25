using DevTools.ModelsContext.Panel;
using FlaUI.Core.Tools;
using RevitDevTool.ModelsContext.LiveTests;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RevitDevTool.ModelsContext.UiTests;

internal sealed class LiveStepSetup
{
    public string Category { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LiveStepAttribute : ConditionBaseAttribute
{
    public const string Variable = "MODELS_CONTEXT_LIVE_STEP";

    public LiveStepAttribute()
        : base(ConditionMode.Include)
    {
        IgnoreMessage = $"Live step between MCP steps; runs only with {Variable}=1.";
    }

    public override string GroupName => nameof(LiveStepAttribute);

    public override bool IsConditionMet => Environment.GetEnvironmentVariable(Variable) == "1";
}

[TestClass]
[DoNotParallelize]
[LiveStep]
public sealed class LiveStepTests : RecordedTest
{
    public const string ExpectVariable = "MODELS_CONTEXT_EXPECT";
    public const string ExpectModelVariable = "MODELS_CONTEXT_EXPECT_MODEL";
    public const string ExpectDocumentsVariable = "MODELS_CONTEXT_EXPECT_DOCS";

    private static readonly Lazy<CaseFile<LiveStepSetup, CaseItem>> LazyData =
        new(() => CaseFiles.Read<CaseFile<LiveStepSetup, CaseItem>>("live-steps.json"));

    private static RevitPanel _panel = null!;
    private static PanelSteps _steps = null!;

    private static LiveStepSetup Setup => LazyData.Value.Setup;

    [ClassInitialize]
    public static void Open(TestContext context)
    {
        if (!new LiveStepAttribute().IsConditionMet)
        {
            Assert.Inconclusive($"Set {LiveStepAttribute.Variable}=1 to run a live step.");
        }

        SetProcessDpiAwarenessContext(new IntPtr(-4));
        Evidence.Layer = "L3-step-" + DateTime.Now.ToString("HHmmssfff", CultureInfo.InvariantCulture);
        _panel = RevitPanel.Attach(CaseFiles.Facts.HostTitle);
        _steps = new PanelSteps(_panel);
        _panel.Settle();
    }

    [ClassCleanup]
    public static void Close()
    {
        _panel?.Dispose();
    }

    [TestMethod]
    public void BuildsStepFilter()
    {
        Record(Find("LS-01"), () =>
        {
            _panel.Select("ModelContext.Scope.WholeModel");
            _panel.Select("ModelContext.View.Filter");
            _panel.Toggle(_steps.Document(ContextBarViewModel.CurrentModelLabel), true);
            _steps.NewFilter([Setup.Category]);
            _steps.AddRule(new NodeData { Parameter = Setup.Parameter, Operator = "Equal", Value = Setup.Value });
            _panel.Settle();
            var count = _panel.Count();
            _panel.Shot("after");
            Evidence.Step($"Built Ducts with {Setup.Parameter} equals '{Setup.Value}' on Whole model with the Current model checked.");
            Evidence.Values("a count", count);
        });
    }

    [TestMethod]
    public void CountIs()
    {
        Record(Find("LS-02"), () =>
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable(ExpectVariable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected))
            {
                Assert.Inconclusive($"Set {ExpectVariable} to the count the step should give.");
            }

            _panel.Select("ModelContext.View.Filter");
            _panel.Settle();
            var count = _panel.Count();
            _panel.Shot("after");
            Evidence.Step($"Read Filter.ResultCount after the panel settled ({_panel.Activity.Generation} jobs done).");
            Evidence.Values(expected, count);
            Assert.AreEqual(expected, count);
        });
    }

    [TestMethod]
    public void ShowsModel()
    {
        Record(Find("LS-03"), () =>
        {
            var expected = Environment.GetEnvironmentVariable(ExpectModelVariable);
            if (string.IsNullOrWhiteSpace(expected))
            {
                Assert.Inconclusive($"Set {ExpectModelVariable} to the title of the model the panel should show.");
            }

            _panel.Settle();
            var documents = _panel.Find("ModelContext.Documents");
            var drop = documents.Patterns.ExpandCollapse.Pattern;
            string title;
            drop.Expand();
            try
            {
                var current = Retry.WhileNull(() => documents.FindAllDescendants(cf => cf.ByAutomationId("ModelContext.Document"))
                        .FirstOrDefault(item => item.Name == ContextBarViewModel.CurrentModelLabel
                            && !string.IsNullOrEmpty(item.Properties.HelpText.ValueOrDefault)),
                    TimeSpan.FromSeconds(5), ignoreException: true).Result;
                Assert.IsNotNull(current, "No Current model item with a title in ModelContext.Documents.");
                title = current.Properties.HelpText.ValueOrDefault ?? string.Empty;
            }
            finally
            {
                drop.Collapse();
            }

            _panel.Shot("after");
            Evidence.Step("Read the HelpText of the Current model item in ModelContext.Documents.");
            Evidence.Values(expected, title);
            Assert.IsTrue(title.StartsWith(expected!, StringComparison.OrdinalIgnoreCase), $"The panel shows '{title}', expected '{expected}'.");
        });
    }

    [TestMethod]
    public void ChecksFollowView()
    {
        Record(Find("LS-04"), () =>
        {
            var expected = (Environment.GetEnvironmentVariable(ExpectDocumentsVariable) ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (expected.Length == 0)
            {
                Assert.Inconclusive($"Set {ExpectDocumentsVariable} to the documents the view draws, for example 'Current model,Architectural'.");
            }

            _panel.Settle();
            var checkedNames = _panel.Find("ModelContext.Documents").FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem))
                .Where(item => item.Patterns.Toggle.IsSupported && item.Patterns.Toggle.Pattern.ToggleState.Value == FlaUI.Core.Definitions.ToggleState.On)
                .Select(item => item.Name).ToList();
            _panel.Shot("after");
            Evidence.Step("Read the checked items of ModelContext.Documents after the view change.");
            Evidence.Values(string.Join(", ", expected), string.Join(", ", checkedNames));

            Assert.AreEqual(expected.Length, checkedNames.Count, "Checked: " + string.Join(", ", checkedNames));
            foreach (var part in expected)
            {
                Assert.IsTrue(checkedNames.Any(name => name.Contains(part, StringComparison.OrdinalIgnoreCase)), $"'{part}' is not checked: {string.Join(", ", checkedNames)}.");
            }
        });
    }

    private static CaseItem Find(string id)
    {
        return LazyData.Value.Cases.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException($"No case '{id}' in live-steps.json.");
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
