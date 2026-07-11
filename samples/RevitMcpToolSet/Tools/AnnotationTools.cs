using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for annotating elements in Revit views, such as tagging and color overrides.")]
[PublicAPI]
public static class AnnotationTools
{
    private const int LargeElementWarningThreshold = 10_000;

    [McpServerTool(Name = "revit_color_by_parameter", Title = "Color by Parameter", ReadOnly = false)]
    [Description("Applies color overrides grouped by a parameter value for all elements in a category.")]
    public static object ColorByParameter(
        [Description("Category display name, e.g. Rooms or Doors")] string categoryName,
        [Description("Parameter name to group by")] string parameterName,
        [Description("View ID; null uses the active view")] long? viewId = null,
        [Description("When true, assign a gradient palette across unique values")] bool? useGradient = null,
        [Description("Optional hex colors (#RRGGBB); auto-generated when omitted")] string[]? colors = null)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            throw new McpException("Category name cannot be empty.");
        if (string.IsNullOrWhiteSpace(parameterName))
            throw new McpException("Parameter name cannot be empty.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var view = ResolveView(doc, viewId);
        var category = FindCategory(doc, categoryName)
            ?? throw new McpException($"Category '{categoryName}' not found.");

        var elements = new FilteredElementCollector(doc, view.Id)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToElements();

        if (elements.Count == 0)
            throw new McpException($"No elements found in category '{categoryName}' for the target view.");

        var grouped = elements
            .GroupBy(e => GetParameterDisplayValue(e, parameterName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var uniqueValues = grouped.Keys
            .OrderBy(v => v == "None")
            .ThenBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueValues.Count == 0)
            throw new McpException($"No parameter values found for '{parameterName}'.");

        var palette = SelectColors(uniqueValues.Count, useGradient == true, colors);
        var solidFillPatternId = GetSolidFillPatternId(doc);

        var elementCount = 0;
        using var tx = new Transaction(doc, "MCP: revit_color_by_parameter");
        tx.Start();
        for (var i = 0; i < uniqueValues.Count; i++)
        {
            var overrideSettings = BuildColorOverride(palette[i], solidFillPatternId);
            foreach (var element in grouped[uniqueValues[i]])
            {
                view.SetElementOverrides(element.Id, overrideSettings);
                elementCount++;
            }
        }
        tx.Commit();

        var result = new Dictionary<string, object?>
        {
            ["groups_colored"] = uniqueValues.Count,
            ["element_count"] = elementCount,
        };

        if (elementCount > LargeElementWarningThreshold)
        {
            result["warning"] =
                $"Colored {elementCount} elements; operations on more than {LargeElementWarningThreshold} elements may be slow.";
        }

        return result;
    }

    [McpServerTool(Name = "revit_clear_overrides", Title = "Clear Graphic Overrides", ReadOnly = false)]
    [Description("Clears graphic overrides for all elements in a category within a view.")]
    public static object ClearOverrides(
        [Description("Category display name")] string categoryName,
        [Description("View ID; null uses the active view")] long? viewId = null)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            throw new McpException("Category name cannot be empty.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var view = ResolveView(doc, viewId);
        var category = FindCategory(doc, categoryName)
            ?? throw new McpException($"Category '{categoryName}' not found.");

        var elements = new FilteredElementCollector(doc, view.Id)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToElements();

        if (elements.Count == 0)
            throw new McpException($"No elements found in category '{categoryName}' for the target view.");

        var cleared = 0;
        var emptyOverride = new OverrideGraphicSettings();

        using var tx = new Transaction(doc, "MCP: revit_clear_overrides");
        tx.Start();
        foreach (var element in elements)
        {
            view.SetElementOverrides(element.Id, emptyOverride);
            cleared++;
        }
        tx.Commit();

        return new { cleared };
    }

    [McpServerTool(Name = "revit_place_tags", Title = "Place Tags", ReadOnly = false)]
    [Description("Places tags on elements in specified views.")]
    public static object PlaceTags(
        [Description("View-element pairs to tag")] TagPlacement[] taggingData)
    {
        if (taggingData.Length == 0) throw new McpException("No tagging data provided.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var tagsPlaced = 0;

        using var txGroup = new TransactionGroup(doc, "MCP: revit_place_tags");
        txGroup.Start();
        try
        {
            foreach (var td in taggingData)
            {
                var view = doc.GetElement(td.ViewId.ToElementId()) as View
                    ?? throw new McpException($"View {td.ViewId} not found.");

                using var tx = new Transaction(doc, "MCP: revit_place_tags");
                tx.Start();
                foreach (var eid in td.ElementIds)
                {
                    var element = doc.GetElement(eid.ToElementId());
                    if (element is null) continue;

                    var location = element.Location as LocationPoint;
                    var tagPoint = location?.Point ?? XYZ.Zero;

                    IndependentTag.Create(doc, view.Id, new Reference(element), false,
                        TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, tagPoint);
                    tagsPlaced++;
                }
                tx.Commit();
            }
            txGroup.Assimilate();
        }
        catch (Exception ex)
        {
            txGroup.RollBack();
            throw new McpException($"Failed to tag elements: {ex.Message}");
        }

        return new { tags_placed = tagsPlaced };
    }

    [McpServerTool(Name = "revit_override_colors", Title = "Override Element Colors", ReadOnly = false)]
    [Description("Applies a solid color graphic override to elements in the active view.")]
    public static object OverrideColors(
        [Description("Element IDs to color")] long[] elementIds,
        [Description("Color as [R, G, B] bytes (0-255)")] int[] color)
    {
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (color.Length < 3) throw new McpException("Color must have 3 components [R, G, B].");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var activeView = RevitContext.ActiveView ?? throw new McpException("No active view.");

        var revitColor = new Color(
            (byte)Math.Clamp(color[0], 0, 255),
            (byte)Math.Clamp(color[1], 0, 255),
            (byte)Math.Clamp(color[2], 0, 255));
        var solidFillPatternId = GetSolidFillPatternId(doc);
        var overrideSettings = BuildColorOverride(revitColor, solidFillPatternId);

        var overriddenCount = 0;
        using var tx = new Transaction(doc, "MCP: revit_override_colors");
        tx.Start();
        foreach (var eid in elementIds)
        {
            var elementId = eid.ToElementId();
            if (doc.GetElement(elementId) is null) continue;

            activeView.SetElementOverrides(elementId, overrideSettings);
            overriddenCount++;
        }
        tx.Commit();

        return new { overridden_count = overriddenCount };
    }

    private static View ResolveView(Document doc, long? viewId)
    {
        if (viewId is null)
            return RevitContext.ActiveView ?? throw new McpException("No active view.");

        return doc.GetElement(viewId.Value.ToElementId()) as View
            ?? throw new McpException($"View {viewId.Value} not found.");
    }

    private static Category? FindCategory(Document doc, string categoryName)
    {
        foreach (Category cat in doc.Settings.Categories)
        {
            if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                return cat;
        }

        return null;
    }

    private static string GetParameterDisplayValue(Element element, string parameterName)
    {
        var value = GetParameterValue(element.LookupParameter(parameterName), element.Document);
        if (value != "None")
            return value;

        var elementType = element.Document.GetElement(element.GetTypeId());
        return elementType is null
            ? "None"
            : GetParameterValue(elementType.LookupParameter(parameterName), element.Document);
    }

    private static string GetParameterValue(Parameter? parameter, Document document)
    {
        if (parameter is null || !parameter.HasValue)
            return "None";

        try
        {
            return parameter.StorageType switch
            {
                StorageType.Integer => parameter.AsInteger().ToString(),
                StorageType.Double => parameter.AsDouble().ToString("G15"),
                StorageType.String => parameter.AsString() ?? "None",
                StorageType.ElementId => parameter.AsElementId()?.ToString() ?? "None",
                _ => parameter.AsValueString() ?? "None",
            };
        }
        catch
        {
            return "None";
        }
    }

    private static ElementId GetSolidFillPatternId(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill)?.Id ?? ElementId.InvalidElementId;
    }

    private static OverrideGraphicSettings BuildColorOverride(Color color, ElementId solidFillPatternId)
    {
        var overrideSettings = new OverrideGraphicSettings();
        overrideSettings.SetProjectionLineColor(color);
        overrideSettings.SetSurfaceForegroundPatternColor(color);
        overrideSettings.SetCutForegroundPatternColor(color);
        overrideSettings.SetCutLineColor(color);
        overrideSettings.SetProjectionLineWeight(3);
        overrideSettings.SetSurfaceForegroundPatternVisible(true);

        if (solidFillPatternId != ElementId.InvalidElementId)
        {
            overrideSettings.SetSurfaceForegroundPatternId(solidFillPatternId);
            overrideSettings.SetCutForegroundPatternId(solidFillPatternId);
        }

        return overrideSettings;
    }

    private static List<Color> SelectColors(int count, bool useGradient, string[]? customColors)
    {
        if (customColors is { Length: > 0 })
        {
            var colors = customColors.Select(HexToColor).ToList();
            if (colors.Count < count)
                colors.AddRange(GenerateDistinctColors(count - colors.Count));
            return colors;
        }

        return useGradient ? GenerateGradientColors(count) : GenerateDistinctColors(count);
    }

    private static Color HexToColor(string hexColor)
    {
        var value = (hexColor ?? "").Trim().TrimStart('#');
        if (value.Length != 6 || !int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return new Color(255, 0, 0);

        return new Color(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));
    }

    private static List<Color> GenerateDistinctColors(int count)
    {
        (int R, int G, int B)[] baseColors =
        [
            (255, 0, 0),
            (0, 128, 255),
            (0, 180, 0),
            (255, 180, 0),
            (180, 0, 255),
            (0, 200, 200),
            (255, 105, 180),
            (128, 128, 0),
            (128, 64, 0),
            (90, 90, 255),
        ];

        var colors = new List<Color>(count);
        for (var index = 0; index < count; index++)
        {
            var (r, g, b) = baseColors[index % baseColors.Length];
            var cycle = index / baseColors.Length;
            if (cycle > 0)
            {
                var factor = Math.Max(0.45, 1.0 - (cycle * 0.15));
                r = (int)(r * factor);
                g = (int)(g * factor);
                b = (int)(b * factor);
            }

            colors.Add(new Color((byte)r, (byte)g, (byte)b));
        }

        return colors;
    }

    private static List<Color> GenerateGradientColors(int count)
    {
        if (count <= 1)
            return [new Color(255, 0, 0)];

        var colors = new List<Color>(count);
        for (var index = 0; index < count; index++)
        {
            var ratio = (double)index / (count - 1);
            var red = (byte)(255 * ratio);
            var green = (byte)(255 * (1 - Math.Abs((2 * ratio) - 1)));
            var blue = (byte)(255 * (1 - ratio));
            colors.Add(new Color(red, green, blue));
        }

        return colors;
    }
}
