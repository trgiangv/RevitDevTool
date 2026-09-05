using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DevTools.AssemblyIsolation;

namespace AcadDevTool.Adapters;

internal sealed class AcadHostAssemblies : HostAssemblies
{
    protected override IEnumerable<Assembly> LoadedByType
    {
        get
        {
            yield return typeof(CommandMethodAttribute).Assembly; // AcCoreMgd.dll
            yield return typeof(Database).Assembly; // AcDbMgd.dll
            yield return typeof(Autodesk.AutoCAD.BoundaryRepresentation.Brep).Assembly; // acdbmgdbrep.dll
            yield return typeof(Autodesk.AutoCAD.Windows.PaletteSet).Assembly; // AcMgd.dll
            yield return typeof(Autodesk.AutoCAD.Customization.CustomizationSection).Assembly; // AcCui.dll
            yield return typeof(Autodesk.AutoCAD.DataExtraction.DxExtractionSettings).Assembly; // AcDx.dll
            yield return typeof(Autodesk.AutoCAD.Windows.ToolPalette.ToolPaletteManager).Assembly; // AcTcMgd.dll
            yield return typeof(Autodesk.AutoCAD.Ribbon.RibbonServices).Assembly; // AcWindows.dll
            yield return typeof(Autodesk.Windows.Palettes.PaletteSet).Assembly; // AdUiPalettes.dll
            yield return typeof(Autodesk.Windows.ComponentManager).Assembly; // AdWindows.dll
        }
    }

    protected override IReadOnlyList<string> LoadedByName { get; } =
    [
        // ============================================================
        // AutoCAD.NET - package assemblies without a useful stable
        // public type anchor
        // ============================================================
        "AcMr",
        "AcSeamless",
        "AdUIMgd",

        // AutoCAD COM interop
        "Autodesk.AutoCAD.Interop",
        "Autodesk.AutoCAD.Interop.Common",

        // ============================================================
        // AutoCAD Map 3D / Geospatial
        // ============================================================
        "ManagedMapApi",
        "Autodesk.Map.Platform",
        "OSGeo.MapGuide.Foundation",
        "OSGeo.MapGuide.Geometry",
        "OSGeo.MapGuide.PlatformBase",
        "OSGeo.FDO",
        "OSGeo.FDO.Common",
        "OSGeo.FDO.Geometry",

        // ============================================================
        // AutoCAD Architecture / AEC
        // ============================================================
        "AecBaseMgd",
        "AecBaseUtilsMgd",
        "AecArchBaseMgd",
        "AecArchMgd",
        "AecStructureMgd",
        "AecPropDataMgd",
        "AecProjectBaseMgd",
        "AecUiBaseMgd",

        // AEC internal / UI
        "AecMgdReverse",
        "AecRibbon",
        "AecGuiInterop",
        "AecUiWindows",

        // ============================================================
        // AutoCAD MEP
        // ============================================================
        "AecbBldSrvMgd",
        "AecbElecBaseMgd",
        "AecbHvacBaseMgd",
        "AecbPartBaseMgd",
        "AecbPipeBaseMgd",
        "AecbPlumbingBaseMgd",

        // MEP internal / UI
        "AecbMgdReverse",
        "AecbRibbon",
        "AecbUiWindows",

        // ============================================================
        // Civil 3D
        // ============================================================
        "AeccDbMgd",
        "AeccPressurePipesMgd",
        "AeccDataShortcutMgd",
        "AeccCogoMgd",
        "AeccHydroCalcsMgd",
        "AeccDrainageDesignMgd",

        // Civil internal / UI / utility
        "AeccUiMgd",
        "AeccUiMgdForm",
        "AeccUiWindows",
        "AeccWindows",
        "AeccContextTabRules",
        "AeccLogMgd",

        // Civil COM interop
        "Autodesk.AEC.Interop.Base",
        "Autodesk.AEC.Interop.UiBase",
        "Autodesk.AECC.Interop.Land",
        "Autodesk.AECC.Interop.Roadway",
        "Autodesk.AECC.Interop.Pipe",
        "Autodesk.AECC.Interop.Survey",

        // ============================================================
        // Plant 3D - Common / Project
        // ============================================================
        "PnPCommonMgd",
        "PnPCommonDbxMgd",
        "PnPCommonArxMgd",
        "PnPCommonUIMgdCs",
        "PnPProjectManagerMgd",
        "PnPProjectManagerUI",

        // Plant 3D - Data
        "PnPDataObjects",
        "PnPDataLinks",
        "PnPSQLiteEngine",

        // Plant 3D - P&ID
        "PnIDMgd",
        "PnIDGUIUtilMgd",
        "PnIdProjectPartsMgd",
        "PnIDSpecUI",
        "PnIDCIP",

        // Plant 3D - 3D / Piping
        "PnP3dObjectsMgd",
        "PnP3dProjectPartsMgd",
        "PnP3dOrthoProjectPart",
        "PnP3dACPUtils",
        "PnP3dPipeUI",
        "PnP3dContentMgd",
        "PnP3dMain",

        // Plant 3D - Catalog / Spec
        "PnP3dPartsRepository",
        "PnP3dSpecUI",

        // Plant 3D - Validation
        "PnPValidation",
        "PnIDDwgValidation",
    ];
}
