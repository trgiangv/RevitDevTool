using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
public static class MaterialTools
{
    [McpServerTool(Name = "revit_clone_material", Title = "Clone Material", ReadOnly = false)]
    [Description("Clones an existing material under a new name with optional color and transparency overrides.")]
    public static object CloneMaterial(
        [Description("Source material name")] string sourceMaterialName,
        [Description("New material name")] string newMaterialName,
        [Description("Red (0-255)")] int colorRed,
        [Description("Green (0-255)")] int colorGreen,
        [Description("Blue (0-255)")] int colorBlue,
        [Description("Transparency (0-100)")] int transparency = 0)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sourceMaterial = new FilteredElementCollector(doc).OfClass(typeof(Material))
            .Cast<Material>().FirstOrDefault(m => m.Name.Equals(sourceMaterialName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Material '{sourceMaterialName}' not found.");

        using var tx = new Transaction(doc, "Duplicate Material");
        tx.Start();
        var newMaterial = Material.Create(doc, newMaterialName);
        var mat = doc.GetElement(newMaterial) as Material;
        if (mat is not null)
        {
            mat.Color = new Color((byte)colorRed, (byte)colorGreen, (byte)colorBlue);
            mat.Transparency = transparency;
        }
        tx.Commit();
        return new { status = "Success", materialId = newMaterial.ToValue() };
    }
}
