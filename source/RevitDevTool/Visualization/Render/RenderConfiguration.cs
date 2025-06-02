using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace RevitDevTool.Visualization.Render;

public static class BoundingBoxVisualizationSettings
{
    public const double Transparency  = 60;

    public static readonly Color SurfaceColor  = Colors.DodgerBlue;
    public static readonly Color EdgeColor  = Color.FromArgb(255, 30, 81, 255);
    public static readonly Color AxisColor  = Color.FromArgb(255, 255, 89, 30);

    public const bool ShowSurface  = true;
    public const bool ShowEdge  = true;
    public const bool ShowAxis  = true;
}

public static class FaceVisualizationSettings
{
    public const double Transparency  = 20;
    public static readonly double Extrusion  = Context.Application.VertexTolerance * 12;
    public static readonly double MinExtrusion  = Context.Application.VertexTolerance * 12;

    public static readonly Color SurfaceColor  = Colors.DodgerBlue;
    public static readonly Color MeshColor  = Color.FromArgb(255, 30, 81, 255);
    public static readonly Color NormalVectorColor  = Color.FromArgb(255, 255, 89, 30);

    public const bool ShowSurface  = true;
    public const bool ShowMeshGrid  = true;
    public const bool ShowNormalVector  = true;
}

public static class MeshVisualizationSettings
{
    public const double Transparency  = 20;
    public static readonly double Extrusion  = Context.Application.VertexTolerance * 12;
    public static readonly double MinExtrusion  = Context.Application.VertexTolerance * 12;

    public static readonly Color SurfaceColor  = Colors.DodgerBlue;
    public static readonly Color MeshColor  = Color.FromArgb(255, 30, 81, 255);
    public static readonly Color NormalVectorColor  = Color.FromArgb(255, 255, 89, 30);

    public const bool ShowSurface  = true;
    public const bool ShowMeshGrid  = true;
    public const bool ShowNormalVector  = true;
}

public static class PolylineVisualizationSettings
{
    public const double Transparency  = 20;
    public const double Diameter  = 2;
    public const double MinThickness  = 0.1;

    public static readonly Color SurfaceColor  = Colors.DodgerBlue;
    public static readonly Color CurveColor  = Color.FromArgb(255, 30, 81, 255);
    public static readonly Color DirectionColor  = Color.FromArgb(255, 255, 89, 30);

    public const bool ShowSurface  = true;
    public const bool ShowCurve  = true;
    public const bool ShowDirection  = true;
}

public static class SolidVisualizationSettings
{
    public const double Transparency  = 20;
    public const double Scale  = 1;

    public static readonly Color FaceColor  = Colors.DodgerBlue;
    public static readonly Color EdgeColor  = Color.FromArgb(255, 30, 81, 255);

    public const bool ShowFace  = true;
    public const bool ShowEdge  = true;
}

public static class XyzVisualizationSettings
{
    public const double Transparency = 20;
    public static readonly double AxisLength  = 18.0.FromMillimeters();
    public const double MinAxisLength  = 0.1;

    public static readonly Color XColor  = Color.FromArgb(255, 238, 65 , 54);
    public static readonly Color YColor  = Color.FromArgb(255, 30, 144, 255);
    public static readonly Color ZColor  = Color.FromArgb(255, 54, 255, 30);

    public const bool ShowPlane  = true;
    public const bool ShowXAxis  = true;
    public const bool ShowYAxis  = true;
    public const bool ShowZAxis  = true;
}