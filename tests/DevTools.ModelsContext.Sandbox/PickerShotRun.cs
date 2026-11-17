using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using DevTools.UI.Theme;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DevTools.ModelsContext.Sandbox;

internal static class PickerShotRun
{
    private static readonly Guid FireRatingId = new("a3f2b1c0-5d6e-4f70-8a9b-0c1d2e3f9c41");
    private static readonly Guid SharedWidthId = new("7b10c2d3-4e5f-4061-9273-84a5b6c7d8e9");

    public static async Task<int> CaptureAsync(SandboxWindow window, Application app, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            await window.MeasureAsync(SandboxSize.Hundred, _ => { });
            var run = window.CurrentRun!;
            run.Model.ExtraCatalog.AddRange(Catalog(run.Data));
            run.Model.Examples["spec:duct"] = "300 mm";
            var panel = run.Panel;
            var view = (ModelContextView)window.Host.Content;
            var prefix = $"{ThemeManager.Current.ActualApplicationTheme}-{(int)window.Width}";
            panel.IsFilterTab = true;
            var filterView = (FilterTabView)view.FindName("FilterView");
            var filter = panel.Filter;
            filter.Library.AddCommand.Execute(null);
            run.Model.RaiseParameterDefinitionsChanged(FakeModelGenerator.CurrentSource);
            foreach (var category in filter.Categories.Items)
            {
                category.IsChecked = true;
            }

            await Settle(1500);
            filter.Root.AddConditionCommand.Execute(null);
            await Settle(500);
            var picker = Find<ComboBox>(filterView, "Filter.Rule.Parameter");
            picker.Focus();
            picker.Text = "Widt";
            await Settle(700);
            picker.IsDropDownOpen = true;
            await Settle(700);
            var drop = (FrameworkElement)picker.Template.FindName("dropDownBorder", picker);
            SaveElement(drop, Path.Combine(directory, $"{prefix}-picker.png"));
            ShotRun.Save(window, Path.Combine(directory, $"{prefix}-picker-panel.png"), drop);
            picker.IsDropDownOpen = false;

            foreach (var (name, info) in TipCases(filter.Root.Parameters))
            {
                var tip = new ToolTip
                {
                    Content = ParameterTip.Of(info),
                    Style = (Style)filterView.Resources["ParameterTipStyle"],
                    PlacementTarget = picker
                };
                tip.IsOpen = true;
                await Settle(400);
                SaveElement(tip, Path.Combine(directory, $"{prefix}-tip-{name}.png"));
                tip.IsOpen = false;
            }

            filter.Root.Children.Clear();
            AddRule(filter, info => info.Key.BuiltInName == "RBS_CURVE_WIDTH_PARAM", FilterOperator.Greater, null);
            AddRule(filter, info => info.DisplayName == "Width" && info.Source == ParameterOrigin.Family, FilterOperator.Equal, null);
            AddRule(filter, info => info.DisplayName == "Slope", FilterOperator.Equal, "2°");
            await Settle(1200);
            ShotRun.Save(window, Path.Combine(directory, $"{prefix}-rules.png"));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static void AddRule(FilterTabViewModel filter, Func<ParameterInfo, bool> pick, FilterOperator op, string? value)
    {
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[^1];
        rule.SelectedParameter = rule.Parameters.First(pick);
        rule.Operator = op;
        if (value is not null)
        {
            rule.Value = value;
        }
    }

    private static IEnumerable<(string Name, ParameterInfo Info)> TipCases(IReadOnlyList<ParameterInfo> parameters)
    {
        yield return ("builtin-measured", parameters.First(info => info.Key.BuiltInName == "RBS_CURVE_WIDTH_PARAM"));
        yield return ("builtin-text", parameters.First(info => info.Key.BuiltInName == "ALL_MODEL_INSTANCE_COMMENTS"));
        yield return ("shared", parameters.First(info => info.Key.SharedId == FireRatingId));
        yield return ("project", parameters.First(info => info.DisplayName == "Zone Code"));
        yield return ("family", parameters.First(info => info.DisplayName == "Width" && info.Source == ParameterOrigin.Family));
        yield return ("yesno", parameters.First(info => info.Key.BuiltInName == "WALL_ATTR_ROOM_BOUNDING"));
    }

    private static IEnumerable<ParameterInfo> Catalog(FakeModelData data)
    {
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var type in data.Sources.Values.SelectMany(source => source.Types))
        {
            ids[type.GroupName] = type.GroupId;
        }

        CategoryIdSet In(params string[] names) => CategoryIdSet.Of(names.Select(name => ids[name]));

        yield return new ParameterInfo(ParameterKey.BuiltIn("Width", "RBS_CURVE_WIDTH_PARAM"), "Width", ValueKind.Number,
            ParameterStorage.Double, "spec:duct", "Duct Size", ParameterOrigin.BuiltIn, ParameterBinding.Instance)
        {
            Categories = In("Ducts"),
            Discipline = "HVAC",
            Group = "Dimensions"
        };
        yield return new ParameterInfo(ParameterKey.BuiltIn("Width", "RBS_DUCT_FITTING_WIDTH_PARAM"), "Width", ValueKind.Number,
            ParameterStorage.Double, "spec:duct", "Duct Size", ParameterOrigin.BuiltIn, ParameterBinding.Instance)
        {
            Categories = In("Duct Fittings"),
            Discipline = "HVAC",
            Group = "Dimensions"
        };
        yield return new ParameterInfo(ParameterKey.BuiltIn("Width", "FAMILY_WIDTH_PARAM"), "Width", ValueKind.Number,
            ParameterStorage.Double, "spec:length", "Length", ParameterOrigin.BuiltIn, ParameterBinding.Instance)
        {
            Categories = In("Doors", "Windows", "Furniture", "Generic Models"),
            Discipline = "Common",
            Group = "Dimensions"
        };
        yield return new ParameterInfo(ParameterKey.Shared("Width", SharedWidthId), "Width", ValueKind.Number,
            ParameterStorage.Double, "spec:length", "Length", ParameterOrigin.Shared, ParameterBinding.Instance)
        {
            Categories = In("Mechanical Equipment"),
            Discipline = "Common",
            Group = "Dimensions"
        };
        yield return new ParameterInfo(ParameterKey.Named("Width", "spec:length-family"), "Width", ValueKind.Number,
            ParameterStorage.Double, "spec:length", "Length", ParameterOrigin.Family, ParameterBinding.Type)
        {
            Categories = In("Air Terminals"),
            Discipline = "Common",
            Group = "Dimensions",
            Families = ["M_Supply Diffuser", "M_Return Diffuser", "M_Linear Diffuser", "M_Exhaust Grille", "M_Slot Diffuser", "M_Round Diffuser", "M_Louver"]
        };
        yield return new ParameterInfo(ParameterKey.BuiltIn("Width", "CASEWORK_WIDTH"), "Width", ValueKind.Number,
            ParameterStorage.Double, "spec:length", "Length", ParameterOrigin.BuiltIn, ParameterBinding.Type)
        {
            Categories = In("Floors"),
            Discipline = "Common",
            Group = "Dimensions"
        };
        yield return new ParameterInfo(ParameterKey.BuiltIn("Comments", "ALL_MODEL_INSTANCE_COMMENTS"), "Comments", ValueKind.Text,
            ParameterStorage.String, "spec:text", "Text", ParameterOrigin.BuiltIn, ParameterBinding.Instance)
        {
            Categories = CategoryIdSet.Of(ids.Values),
            Group = "Identity Data"
        };
        yield return new ParameterInfo(ParameterKey.Shared("Fire Rating", FireRatingId), "Fire Rating", ValueKind.Text,
            ParameterStorage.String, "spec:text", "Text", ParameterOrigin.Shared, ParameterBinding.Type)
        {
            Categories = In("Walls", "Doors"),
            Group = "Identity Data"
        };
        yield return new ParameterInfo(ParameterKey.Named("Zone Code", "spec:text"), "Zone Code", ValueKind.Text,
            ParameterStorage.String, "spec:text", "Text", ParameterOrigin.Project, ParameterBinding.Instance)
        {
            Categories = In("Ducts", "Air Terminals", "Mechanical Equipment"),
            Group = "Identity Data"
        };
        yield return new ParameterInfo(ParameterKey.BuiltIn("Room Bounding", "WALL_ATTR_ROOM_BOUNDING"), "Room Bounding",
            ValueKind.YesNo, ParameterStorage.Integer, "spec:yesno", "Yes/No", ParameterOrigin.BuiltIn, ParameterBinding.Instance)
        {
            Categories = In("Walls"),
            Group = "Constraints"
        };
        yield return new ParameterInfo(ParameterKey.BuiltIn("Slope", "ROOF_SLOPE"), "Slope", ValueKind.Number,
            ParameterStorage.Double, "spec:slope", "Slope", ParameterOrigin.BuiltIn, ParameterBinding.Instance)
        {
            Categories = In("Roofs"),
            Discipline = "Common",
            Group = "Dimensions"
        };
    }

    private static T Find<T>(DependencyObject root, string automationId)
        where T : FrameworkElement
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is T { IsVisible: true } box && System.Windows.Automation.AutomationProperties.GetAutomationId(box) == automationId)
            {
                return box;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, index));
            }
        }

        throw new InvalidOperationException(automationId);
    }

    private static Task Settle(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }

    private static void SaveElement(FrameworkElement element, string path)
    {
        const double Scale = 2;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * Scale), (int)Math.Ceiling(element.ActualHeight * Scale),
            96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }

        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
