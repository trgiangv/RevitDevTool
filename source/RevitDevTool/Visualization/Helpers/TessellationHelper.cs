using System.Diagnostics;

namespace RevitDevTool.Visualization.Helpers;

public static class TessellationHelper
{
    public static List<XYZ> GetXyz(List<GeometryObject> visualizeGeometries)
    {
        if (visualizeGeometries.Count == 0) return Enumerable.Empty<XYZ>().ToList();

        var allPoints = new List<XYZ?>();
        var hasContinuity = false;
        
        // Process all curves in the collection
        var curves = visualizeGeometries.OfType<Curve>().ToList();
        foreach (var curve in curves)
        {
            try
            {
                // Use better tessellation with parameter if possible
                IList<XYZ> points;
                
                // Try using a more detailed tessellation method if available
                if (curve is HermiteSpline or NurbSpline)
                {
                    // If it's a spline, try to get more detailed tessellation
                    var tessellationDivisions = Math.Max(20, (int)(curve.Length * 10));
                    points = GetDetailedCurveTessellation(curve, tessellationDivisions);
                }
                else
                {
                    // Default tessellation for other curve types
                    points = curve.Tessellate();
                }

                if (points.Count <= 0) continue;

                if (hasContinuity && allPoints.Count > 0 && allPoints.Last()!.IsAlmostEqualTo(points.First()))
                {
                    // Add a separator point (null) if there's a gap between curves
                    allPoints.Add(null);
                }
                    
                allPoints.AddRange(points);
                hasContinuity = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error tessellating curve in PolylineVisualizationServer: {ex.Message}");
                
                // Fallback to basic tessellation if the advanced method fails
                try
                {
                    var points = curve.Tessellate();
                    if (points.Count <= 0) continue;
                    if (hasContinuity && allPoints.Count > 0 && allPoints.Last()!.IsAlmostEqualTo(points.First()))
                    {
                        allPoints.Add(null);
                    }
                        
                    allPoints.AddRange(points);
                    hasContinuity = true;
                }
                catch
                {
                    // If both methods fail, just skip this curve
                    Trace.TraceError($"Failed to tessellate curve using fallback method");
                }
            }
        }
        
        // Process all edges in the collection
        var edges = visualizeGeometries.OfType<Edge>().ToList();
        foreach (var edge in edges)
        {
            try
            {
                var points = edge.Tessellate();
                if (points.Count <= 0) continue;
                if (hasContinuity && allPoints.Count > 0 && allPoints.Last()!.IsAlmostEqualTo(points.First()))
                {
                    allPoints.Add(null);
                }
                    
                allPoints.AddRange(points);
                hasContinuity = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error tessellating edge in PolylineVisualizationServer: {ex.Message}");
            }
        }
        
        // If we have null points (separators), we need to split the points into segments
        return ProcessPointsWithSeparators(allPoints);
    }
    
    private static IList<XYZ> GetDetailedCurveTessellation(Curve curve, int divisions)
    {
        var result = new List<XYZ>();
        if (divisions <= 0) return result;
        
        try
        {
            // Get parameters at start and end of curve
            var startParam = curve.GetEndParameter(0);
            var endParam = curve.GetEndParameter(1);
            var paramRange = endParam - startParam;
            
            // Add the start point
            result.Add(curve.GetEndPoint(0));
            
            // Add intermediate points
            for (var i = 1; i < divisions; i++)
            {
                var param = startParam + (paramRange * i / divisions);
                var point = curve.Evaluate(param, false);
                result.Add(point);
            }
            
            // Add the end point
            result.Add(curve.GetEndPoint(1));
            
            return result;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error in GetDetailedCurveTessellation: {ex.Message}");
            return curve.Tessellate(); // Fallback to regular tessellation
        }
    }
    
    private static List<XYZ> ProcessPointsWithSeparators(List<XYZ?> pointsWithSeparators)
    {
        // If no points or no separators, return the original points
        if (pointsWithSeparators.Count == 0 || !pointsWithSeparators.Contains(null))
        {
            return pointsWithSeparators.Where(p => p is not null).ToList()!;
        }
        
        // Split the points at each null separator and process each segment
        var segments = new List<List<XYZ>>();
        var currentSegment = new List<XYZ>();
        
        foreach (var point in pointsWithSeparators)
        {
            if (point == null)
            {
                // End of segment
                if (currentSegment.Count <= 0) continue;
                segments.Add(currentSegment);
                currentSegment = [];
            }
            else
            {
                currentSegment.Add(point);
            }
        }
        
        // Add the last segment if it has points
        if (currentSegment.Count > 0)
        {
            segments.Add(currentSegment);
        }
        
        // Create a continuous path with artificial connecting points
        var result = new List<XYZ?>();
        var isFirstSegment = true;
        
        foreach (var segment in segments)
        {
            if (!isFirstSegment)
            {
                // If this isn't the first segment, add the first point of this segment
                // This creates a visible "jump" between disconnected segments
                result.Add(segment.First());
            }
            
            result.AddRange(segment);
            isFirstSegment = false;
        }
        
        return result!;
    }
}