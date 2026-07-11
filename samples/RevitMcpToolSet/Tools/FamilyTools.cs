using System.ComponentModel;
using Autodesk.Revit.DB.Structure;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for placing family instances in the Revit model.")]
[PublicAPI]
public static class FamilyTools
{
    [McpServerTool(Name = "revit_place_family", Title = "Place Family", ReadOnly = false)]
    [Description("Creates one or more family instances at specified locations.")]
    public static object PlaceFamily(
        [Description("Family name")] string familyName,
        [Description("Type name (optional — uses first matching type if omitted)")] string? typeName,
        [Description("Placement locations")] Placement[] placements,
        [Description("Optional instance parameters to set after placement")] Dictionary<string, string>? properties)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (string.IsNullOrWhiteSpace(familyName)) throw new McpException("Family name is required.");
        if (placements.Length == 0) throw new McpException("At least one placement is required.");

        var symbol = FindFamilySymbol(doc, familyName, typeName)
            ?? throw new McpException(BuildFamilyNotFoundMessage(doc, familyName, typeName));

        var created = new List<object>();
        var failures = new List<ToolError>();

        using var tx = new Transaction(doc, "MCP: revit_place_family");
        tx.Start();

        if (!symbol.IsActive)
        {
            symbol.Activate();
            doc.Regenerate();
        }

        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            try
            {
                var point = new XYZ(placement.X, placement.Y, placement.Z);
                var level = ResolveLevel(doc, placement.LevelName);
                var instance = CreateInstance(doc, symbol, point, level, placement.HostId);

                var rotation = placement.Rotation ?? 0.0;
                if (Math.Abs(rotation) > 1e-9)
                {
                    var radians = rotation * Math.PI / 180.0;
                    var axis = Line.CreateBound(point, point + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, instance.Id, axis, radians);
                }

                if (properties is { Count: > 0 })
                {
                    foreach (var pair in properties)
                    {
                        var (success, message) = ParameterAccessor.SetParameterValue(instance, pair.Key, pair.Value);
                        if (!success)
                            failures.Add(ToolErrorHelper.FromMessage(
                                $"Placement {i}: {message}", instance.Id.ToValue()));
                    }
                }

                created.Add(new
                {
                    id = instance.Id.ToValue(),
                    location = GetInstanceLocation(instance, point),
                });
            }
            catch (Exception ex)
            {
                failures.Add(ToolErrorHelper.FromMessage($"Placement {i}: {ex.Message}"));
            }
        }

        tx.Commit();
        return new
        {
            created,
            failures = failures.Count > 0 ? failures : null,
        };
    }

    private static FamilySymbol? FindFamilySymbol(Document doc, string familyName, string? typeName)
    {
        var symbols = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .Where(s => s.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(typeName))
            symbols = symbols.Where(s => s.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

        return symbols.FirstOrDefault();
    }

    private static string BuildFamilyNotFoundMessage(Document doc, string familyName, string? typeName)
    {
        var available = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .Select(s => s.Family.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        var msg = $"Family '{familyName}'" +
                  (string.IsNullOrWhiteSpace(typeName) ? "" : $" type '{typeName}'") +
                  " not found.";
        if (available.Count > 0)
            msg += $" Available families (first 20): {string.Join(", ", available)}.";
        return msg;
    }

    private static Level? ResolveLevel(Document doc, string? levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return null;

        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Level '{levelName}' not found.");
    }

    private static FamilyInstance CreateInstance(
        Document doc, FamilySymbol symbol, XYZ point, Level? level, long? hostId)
    {
        if (hostId is long hostElementId)
        {
            var host = doc.GetElement(hostElementId.ToElementId())
                ?? throw new McpException($"Host element {hostElementId} not found.");

            var hostLevel = level ?? doc.GetElement(host.LevelId) as Level;
            if (hostLevel is null && host is Wall wall)
                hostLevel = doc.GetElement(wall.LevelId) as Level;

            if (hostLevel is null)
                throw new McpException($"Could not resolve level for host element {hostElementId}.");

            return doc.Create.NewFamilyInstance(point, symbol, host, hostLevel, StructuralType.NonStructural);
        }

        if (level is not null)
            return doc.Create.NewFamilyInstance(point, symbol, level, StructuralType.NonStructural);

        return doc.Create.NewFamilyInstance(point, symbol, StructuralType.NonStructural);
    }

    private static object GetInstanceLocation(FamilyInstance instance, XYZ fallback)
    {
        if (instance.Location is LocationPoint locationPoint)
        {
            var pt = locationPoint.Point;
            return new { x = pt.X, y = pt.Y, z = pt.Z };
        }

        return new { x = fallback.X, y = fallback.Y, z = fallback.Z };
    }
}
