namespace RevitDevTool.Visualization.Contracts ;

public interface IVisualUpdate
{
    public void UpdateEffects();
    public void UpdateTransparency(double value);
    
    public void UpdateSurfaceColor(Autodesk.Revit.DB.Color color);
    public void UpdateEdgeColor(Autodesk.Revit.DB.Color color);
    public void UpdateAxisColor(Autodesk.Revit.DB.Color color);
    public void UpdateCurveColor(Autodesk.Revit.DB.Color color);
    public void UpdateMeshGridColor(Autodesk.Revit.DB.Color color);
    public void UpdateNormalVectorColor(Autodesk.Revit.DB.Color color);
    public void UpdateDirectionColor(Autodesk.Revit.DB.Color color);
    public void UpdateXColor(Autodesk.Revit.DB.Color color);
    public void UpdateYColor(Autodesk.Revit.DB.Color color);
    public void UpdateZColor(Autodesk.Revit.DB.Color color);
    
    public void UpdateScale(double value);
    public void UpdateExtrusion(double value);
    public void UpdateDiameter(double value);
    public void UpdateAxisLength(double value);
    
    public void UpdateSurfaceVisibility(bool visible);
    public void UpdateEdgeVisibility(bool visible);
    public void UpdateFaceVisibility(bool visible);
    public void UpdateAxisVisibility(bool visible);
    public void UpdateMeshGridVisibility(bool visible);
    public void UpdateNormalVectorVisibility(bool visible);
    public void UpdateCurveVisibility(bool visible);
    public void UpdateDirectionVisibility(bool visible);
    public void UpdatePlaneVisibility(bool visible);
    public void UpdateXAxisVisibility(bool visible);
    public void UpdateYAxisVisibility(bool visible);
    public void UpdateZAxisVisibility(bool visible);
}