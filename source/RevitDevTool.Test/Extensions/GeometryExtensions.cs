namespace RevitDevTool.Test.Extensions;

[PublicAPI]
public static class GeometryExtensions
{
    public static List<Solid> GetSolids(this Element element)
    {
        var opts = new Options
        {
            ComputeReferences = false,
            IncludeNonVisibleObjects = true
        };

        var solids = new List<Solid>();
        var geometry = element.get_Geometry(opts);
        if (geometry == null) return solids;

        foreach (var geoObj in geometry)
        {
            switch (geoObj)
            {
                case Solid solid when solid.Volume != 0:
                    solids.Add(solid);
                    break;
                case GeometryInstance geoInstance:
                    foreach (var instGeoObj in geoInstance.GetInstanceGeometry())
                    {
                        if (instGeoObj is Solid instSolid && instSolid.Volume != 0)
                        {
                            solids.Add(instSolid);
                        }
                    }
                    break;
            }
        }

        return solids;
    }

    public static List<Mesh> GetMeshes(this Element element)
    {
        var opts = new Options
        {
            ComputeReferences = false,
            IncludeNonVisibleObjects = true
        };
        var meshes = new List<Mesh>();
        var geometry = element.get_Geometry(opts);
        if (geometry == null) return meshes;
        foreach (var geoObj in geometry)
        {
            switch (geoObj)
            {
                case Mesh mesh:
                    meshes.Add(mesh);
                    break;
                case GeometryInstance geoInstance:
                    foreach (var instGeoObj in geoInstance.GetInstanceGeometry())
                    {
                        if (instGeoObj is Mesh instMesh)
                        {
                            meshes.Add(instMesh);
                        }
                    }
                    break;
            }
        }
        return meshes;
    }

    public static List<Face> GetFaces(this Solid solid)
    {
        return solid.Faces.Cast<Face>().ToList();
    }

    public static List<XYZ> GetVertices(this Mesh mesh)
    {
        return mesh.Vertices.ToList();
    }

    public static List<XYZ> GetVertices(this Solid solid)
    {
        return solid.Edges.Cast<Edge>().SelectMany(edge => edge.Tessellate()).ToList();
    }

    public static List<XYZ> GetVertices(this GeometryObject geometryObject)
    {
        return geometryObject switch
        {
            Solid solid => solid.GetVertices(),
            Mesh mesh => mesh.GetVertices(),
            _ => []
        };
    }

    public static List<Edge> GetEdges(this Solid solid)
    {
        return solid.Edges.Cast<Edge>().ToList();
    }

    public static List<Curve> GetCurves(this Solid solid)
    {
        return solid.Edges.Cast<Edge>().Select(edge => edge.AsCurve()).ToList();
    }

    public static List<Edge> GetEdges(this Face face)
    {
        var edgeArrays = face.EdgeLoops.Cast<EdgeArray>();
        return edgeArrays.SelectMany(edgeLoop => edgeLoop.Cast<Edge>()).ToList();
    }

    public static List<Curve> GetCurves(this Face face)
    {
        var curveLoops = face.GetEdgesAsCurveLoops();
        return curveLoops?.SelectMany(loop => loop).ToList() ?? [];
    }
}