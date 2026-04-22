using Autodesk.Revit.DB.DirectContext3D;
using Nice3point.Revit.Extensions.Runtime;
using RevitDevTool.Visualization.Render;

namespace RevitDevTool.Visualization.Helpers;

public static class RenderHelper
{
    /// <summary>
    /// Maps the vertex and index data of a mesh to the specified rendering buffer, applying an offset to the vertex
    /// positions.
    /// </summary>
    /// <remarks>This method processes the vertices and triangles of the provided <paramref name="mesh"/> and
    /// maps them to the  <paramref name="buffer"/>. Each vertex position is adjusted by adding the specified <paramref
    /// name="offset"/>  along its normal vector. The method also updates the rendering buffer's vertex and index
    /// buffers, as well as  its format and primitive counts.</remarks>
    /// <param name="buffer">The rendering buffer to which the mesh data will be mapped. This buffer will be updated with vertex and index
    /// data.</param>
    /// <param name="mesh">The mesh containing the vertex and triangle data to be mapped to the rendering buffer.</param>
    /// <param name="offset">The offset to apply to each vertex position along its normal direction.</param>
    public static void MapSurfaceBuffer(RenderingBufferStorage buffer, Mesh mesh, double offset)
    {
        var vertexCount = mesh.Vertices.Count;
        var triangleCount = mesh.NumTriangles;
        var normals = GetMeshVertexNormals(mesh);

        var vertices = new XYZ[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            vertices[i] = mesh.Vertices[i] + (normals[i] * offset);
        }

        var triangles = new List<IndexTriangle>(triangleCount);
        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            triangles.Add(new IndexTriangle(
                (int)meshTriangle.get_Index(0),
                (int)meshTriangle.get_Index(1),
                (int)meshTriangle.get_Index(2)));
        }

        MapTriangles(buffer, vertices, triangles);
    }

    /// <summary>
    /// Maps a curve defined by a list of vertices into the specified rendering buffer.
    /// </summary>
    /// <remarks>This method populates the provided rendering buffer with vertex and index data based on the
    /// input vertices. The buffer's vertex and index buffers are initialized, mapped, populated, and then unmapped. The
    /// method assumes that the vertices define a continuous curve, and it generates line indices connecting consecutive
    /// vertices.</remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance where the vertex and index data will be stored.</param>
    /// <param name="vertices">A collection of <see cref="XYZ"/> objects representing the vertices of the curve to be mapped.</param>
    public static void MapCurveBuffer(RenderingBufferStorage buffer, IList<XYZ> vertices)
    {
        if (vertices.Count < 2)
        {
            MapLines(buffer, vertices, []);
            return;
        }

        var lines = new List<IndexLine>(vertices.Count - 1);
        for (var i = 0; i < vertices.Count - 1; i++)
        {
            lines.Add(new IndexLine(i, i + 1));
        }

        MapLines(buffer, vertices, lines);
    }

    /// <summary>
    /// Maps a curve defined by a series of vertices into a rendering buffer, creating a tubular geometry with the
    /// specified diameter.
    /// </summary>
    /// <remarks>This method generates a segmented tubular geometry around the provided curve, where each
    /// segment corresponds to a portion of the curve. The method calculates the necessary vertex and index data to
    /// represent the tubular geometry and maps this data into the provided rendering buffer.  The <paramref
    /// name="buffer"/> is updated with the following: <list type="bullet"> <item><description>Vertex buffer containing
    /// the positions of the tubular geometry's vertices.</description></item> <item><description>Index buffer defining
    /// the connectivity of the vertices to form the geometry.</description></item> <item><description>Primitive count
    /// and vertex format information for rendering.</description></item> </list>  The method assumes that the <paramref
    /// name="vertices"/> list contains at least two points to define a valid curve. The generated geometry is segmented
    /// based on the curve's vertices, and each segment is represented as a series of connected quads.</remarks>
    /// <param name="buffer">The rendering buffer to populate with vertex and index data for the tubular geometry.</param>
    /// <param name="vertices">A list of 3D points representing the curve to be mapped into the buffer.</param>
    /// <param name="diameter">The diameter of the tubular geometry to be generated around the curve.</param>
    public static void MapCurveBuffer(RenderingBufferStorage buffer, IList<XYZ> vertices, double diameter)
    {
        var tubeSegments = RenderGeometryHelper.GetSegmentationTube(vertices, diameter);
        var segmentVerticesCount = tubeSegments[0].Count;

        var allVertices = tubeSegments
            .SelectMany(segment => segment)
            .ToList();

        var lines = new List<IndexLine>((vertices.Count - 1) * segmentVerticesCount * 4);

        for (var i = 0; i < vertices.Count - 1; i++)
        {
            for (var j = 0; j < segmentVerticesCount; j++)
            {
                var currentStart = (i * segmentVerticesCount) + j;
                var nextStart = ((i + 1) * segmentVerticesCount) + j;
                var currentEnd = (i * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);
                var nextEnd = ((i + 1) * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);

                lines.Add(new IndexLine(currentStart, nextStart));
                lines.Add(new IndexLine(nextStart, nextEnd));
                lines.Add(new IndexLine(nextEnd, currentEnd));
                lines.Add(new IndexLine(currentEnd, currentStart));
            }
        }

        MapLines(buffer, allVertices, lines);
    }

    /// <summary>
    /// Maps a curve surface to the specified rendering buffer by generating vertex and index data based on the provided
    /// vertices and diameter.
    /// </summary>
    /// <remarks>This method generates a segmented tube-like surface along the provided vertices, with the
    /// specified diameter. The resulting geometry is stored in the provided rendering buffer, including vertex
    /// positions and triangle indices for rendering. The method assumes that the vertices form a continuous path and
    /// that the diameter is valid (greater than zero).  The rendering buffer's vertex and index buffers are mapped,
    /// populated with the generated data, and then unmapped. The buffer's format and counts are updated
    /// accordingly.</remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance where the generated vertex and index data will be stored.</param>
    /// <param name="vertices">A collection of <see cref="XYZ"/> points representing the curve's path. The vertices define the centerline of
    /// the tube-like surface to be generated.</param>
    /// <param name="diameter">The diameter of the tube-like surface to be generated. Must be a positive value.</param>
    public static void MapCurveSurfaceBuffer(RenderingBufferStorage buffer, IList<XYZ> vertices, double diameter)
    {
        var tubeSegments = RenderGeometryHelper.GetSegmentationTube(vertices, diameter);
        var segmentVerticesCount = tubeSegments[0].Count;

        var allVertices = tubeSegments
            .SelectMany(segment => segment)
            .ToList();

        var triangles = new List<IndexTriangle>((vertices.Count - 1) * segmentVerticesCount * 2);

        for (var i = 0; i < vertices.Count - 1; i++)
        {
            for (var j = 0; j < segmentVerticesCount; j++)
            {
                var currentStart = (i * segmentVerticesCount) + j;
                var nextStart = ((i + 1) * segmentVerticesCount) + j;
                var currentEnd = (i * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);
                var nextEnd = ((i + 1) * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);

                triangles.Add(new IndexTriangle(currentStart, nextStart, nextEnd));
                triangles.Add(new IndexTriangle(nextEnd, currentEnd, currentStart));
            }
        }

        MapTriangles(buffer, allVertices, triangles);
    }

    /// <summary>
    /// Maps the vertex and index data of a mesh to the specified rendering buffer,  applying an offset to the mesh
    /// vertices to create a duplicate layer.
    /// </summary>
    /// <remarks>This method processes the mesh by calculating vertex normals, duplicating vertices with an
    /// offset,  and generating index data for rendering both the original and offset layers. The resulting data is 
    /// stored in the provided <paramref name="buffer"/> for use in rendering operations.  The method assumes that the
    /// <paramref name="mesh"/> contains valid vertex and triangle data.  The <paramref name="buffer"/> will be
    /// initialized and populated with the appropriate vertex and  index buffers, as well as format and size
    /// metadata.</remarks>
    /// <param name="buffer">The rendering buffer to which the mesh data will be mapped.  This buffer will be updated with vertex and index
    /// data, as well as format and size information.</param>
    /// <param name="mesh">The mesh whose vertex and triangle data will be used to populate the rendering buffer.</param>
    /// <param name="offset">The offset distance to apply to the mesh vertices when creating the duplicate layer.  This is typically used to
    /// create a visual effect, such as an extruded or layered appearance.</param>
    public static void MapMeshGridBuffer(RenderingBufferStorage buffer, Mesh mesh, double offset)
    {
        var vertexCount = mesh.Vertices.Count;
        var triangleCount = mesh.NumTriangles;
        var normals = GetMeshVertexNormals(mesh);

        var vertices = new List<XYZ>(vertexCount * 2);

        foreach (var vertex in mesh.Vertices)
        {
            vertices.Add(vertex);
        }

        for (var i = 0; i < vertexCount; i++)
        {
            vertices.Add(mesh.Vertices[i] + (normals[i] * offset));
        }

        var lines = new List<IndexLine>((3 * triangleCount * 2) + vertexCount);

        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            var index0 = (int)meshTriangle.get_Index(0);
            var index1 = (int)meshTriangle.get_Index(1);
            var index2 = (int)meshTriangle.get_Index(2);

            lines.Add(new IndexLine(index0, index1));
            lines.Add(new IndexLine(index1, index2));
            lines.Add(new IndexLine(index2, index0));
        }

        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            var index0 = (int)meshTriangle.get_Index(0) + vertexCount;
            var index1 = (int)meshTriangle.get_Index(1) + vertexCount;
            var index2 = (int)meshTriangle.get_Index(2) + vertexCount;

            lines.Add(new IndexLine(index0, index1));
            lines.Add(new IndexLine(index1, index2));
            lines.Add(new IndexLine(index2, index0));
        }

        for (var i = 0; i < vertexCount; i++)
        {
            lines.Add(new IndexLine(i, i + vertexCount));
        }

        MapLines(buffer, vertices, lines);
    }

    /// <summary>
    /// Maps the vertices and indices of a 3D bounding box to the specified rendering buffer.
    /// </summary>
    /// <remarks>This method generates the vertex and index data required to render a 3D bounding box as a set
    /// of triangles. The bounding box is defined by its minimum and maximum points, which are transformed using the
    /// bounding box's transformation matrix. The resulting geometry includes the vertices for all eight corners of the
    /// box and the indices for rendering its six faces as triangles.  The method updates the provided <paramref
    /// name="buffer"/> with the following: <list type="bullet"> <item><description>The vertex buffer, containing the
    /// transformed corner positions of the bounding box.</description></item> <item><description>The index buffer,
    /// defining the triangles that make up the faces of the bounding box.</description></item>
    /// <item><description>Metadata such as the vertex count, primitive count, and vertex format.</description></item>
    /// </list></remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance where the bounding box's vertex and index data will be stored.</param>
    /// <param name="box">The <see cref="BoundingBoxXYZ"/> representing the 3D bounding box to be mapped. The bounding box is transformed
    /// using its associated transformation matrix before being processed.</param>
    public static void MapBoundingBoxSurfaceBuffer(RenderingBufferStorage buffer, BoundingBoxXYZ box)
    {
        var corners = GetBoundingBoxCorners(box);

        List<IndexTriangle> triangles =
        [
            new(0, 1, 2), new(2, 3, 0),
            new(4, 5, 6), new(6, 7, 4),
            new(0, 4, 5), new(5, 1, 0),
            new(1, 5, 6), new(6, 2, 1),
            new(2, 6, 7), new(7, 3, 2),
            new(3, 7, 4), new(4, 0, 3)
        ];

        MapTriangles(buffer, corners, triangles);
    }

    /// <summary>
    /// Maps the edges of a 3D bounding box to a rendering buffer for visualization or processing.
    /// </summary>
    /// <remarks>This method generates the vertices and edges of the bounding box in 3D space and populates
    /// the provided rendering buffer with the corresponding vertex and index data. The buffer is configured with the
    /// appropriate vertex format and primitive counts to represent the bounding box as a wireframe.</remarks>
    /// <param name="buffer">The rendering buffer where the bounding box edges will be stored. This buffer will be updated with vertex and
    /// index data.</param>
    /// <param name="box">The 3D bounding box whose edges are to be mapped. The box's transformation is applied to determine the final
    /// vertex positions.</param>
    public static void MapBoundingBoxEdgeBuffer(RenderingBufferStorage buffer, BoundingBoxXYZ box)
    {
        var corners = GetBoundingBoxCorners(box);

        List<IndexLine> edges =
        [
            new(0, 1), new(1, 2), new(2, 3), new(3, 0),
            new(4, 5), new(5, 6), new(6, 7), new(7, 4),
            new(0, 4), new(1, 5), new(2, 6), new(3, 7)
        ];

        MapLines(buffer, corners, edges);
    }

    /// <summary>
    /// Maps a normal vector to a rendering buffer, creating a visual representation of the vector as a line with an
    /// arrowhead.
    /// </summary>
    /// <remarks>This method generates a line representing the vector from the specified <paramref
    /// name="origin"/> in the direction of <paramref name="vector"/> with the given <paramref name="length"/>. An
    /// arrowhead is added at the end of the line to indicate direction. The rendering buffer is configured with the
    /// necessary vertex and index data to render the vector.</remarks>
    /// <param name="buffer">The rendering buffer to which the vector representation will be mapped. This buffer will be updated with vertex
    /// and index data.</param>
    /// <param name="origin">The starting point of the vector in 3D space.</param>
    /// <param name="vector">The direction and magnitude of the vector to be visualized.</param>
    /// <param name="length">The length of the vector to be rendered. Determines the size of the arrowhead.</param>
    public static void MapNormalVectorBuffer(RenderingBufferStorage buffer, XYZ origin, XYZ vector, double length)
    {
        var headSize = length > 1 ? 0.2 : length * 0.2;

        var endPoint = origin + (vector * length);
        var arrowHeadBase = endPoint - (vector * headSize);
        var basisVector = Math.Abs(vector.Z).IsAlmostEqual(1) ? XYZ.BasisY : XYZ.BasisZ;
        var perpendicular1 = vector.CrossProduct(basisVector).Normalize().Multiply(headSize * 0.5);

        List<XYZ> vertices =
        [
            origin,
            endPoint,
            arrowHeadBase + perpendicular1,
            arrowHeadBase - perpendicular1
        ];

        List<IndexLine> lines =
        [
            new(0, 1),
            new(1, 2),
            new(1, 3)
        ];

        MapLines(buffer, vertices, lines);
    }

    /// <summary>
    /// Configures the specified <see cref="RenderingBufferStorage"/> instance to represent a rectangular buffer defined
    /// by the given minimum and maximum points in 3D space.
    /// </summary>
    /// <remarks>This method calculates the vertices and indices required to represent a rectangular buffer in
    /// 3D space based on the provided <paramref name="min"/> and <paramref name="max"/> points. The buffer is
    /// configured with four vertices and two triangles, and the vertex and index buffers are populated accordingly. 
    /// The orientation of the rectangle is determined by the direction of the vector from <paramref name="min"/> to
    /// <paramref name="max"/>. The method adjusts the rectangle's dimensions and orientation based on whether the
    /// vector aligns with the X, Y, or Z axis.  The caller is responsible for ensuring that the <paramref
    /// name="buffer"/> instance is properly initialized before calling this method.</remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance to be configured. This parameter cannot be null.</param>
    /// <param name="min">The minimum point of the rectangular buffer in 3D space.</param>
    /// <param name="max">The maximum point of the rectangular buffer in 3D space.</param>
    public static void MapSideBuffer(RenderingBufferStorage buffer, XYZ min, XYZ max)
    {
        var normal = (max - min).Normalize();
        var length = (max - min).GetLength() / 2;

        XYZ point1;
        XYZ point2;
        XYZ point3;
        XYZ point4;

        if (normal.IsAlmostEqualTo(XYZ.BasisX))
        {
            point1 = new XYZ(min.X, min.Y - length, min.Z);
            point2 = new XYZ(min.X, min.Y + length, min.Z);
            point3 = new XYZ(max.X, max.Y - length, max.Z);
            point4 = new XYZ(max.X, max.Y + length, max.Z);
        }
        else if (normal.IsAlmostEqualTo(XYZ.BasisY))
        {
            point1 = new XYZ(min.X, min.Y, min.Z - length);
            point2 = new XYZ(min.X, min.Y, min.Z + length);
            point3 = new XYZ(max.X, max.Y, max.Z - length);
            point4 = new XYZ(max.X, max.Y, max.Z + length);
        }
        else
        {
            point1 = new XYZ(min.X - length, min.Y, min.Z);
            point2 = new XYZ(min.X + length, min.Y, min.Z);
            point3 = new XYZ(max.X - length, max.Y, max.Z);
            point4 = new XYZ(max.X + length, max.Y, max.Z);
        }

        List<XYZ> vertices = [point1, point2, point3, point4];
        List<IndexTriangle> triangles =
        [
            new(0, 1, 2),
            new(1, 2, 3)
        ];

        MapTriangles(buffer, vertices, triangles);
    }

    /// <summary>
    /// Computes the four corner points of a finite rectangular patch on a plane.
    /// </summary>
    /// <remarks>
    /// The returned points are derived from the plane origin and its local <c>XVec</c> and <c>YVec</c> axes.
    /// The patch extends equally in all directions from the origin by <paramref name="halfSize"/>.
    /// </remarks>
    /// <param name="plane">
    /// The source plane used to generate the patch.
    /// </param>
    /// <param name="halfSize">
    /// Half of the rectangle side length measured along the local X and Y directions.
    /// </param>
    /// <returns>
    /// An array containing four corner points ordered around the perimeter of the plane patch.
    /// </returns>
    public static XYZ[] GetPlaneCorners(Plane plane, double halfSize)
    {
        var origin = plane.Origin;
        var x = plane.XVec.Normalize() * halfSize;
        var y = plane.YVec.Normalize() * halfSize;

        return
        [
            origin - x - y,
            origin + x - y,
            origin + x + y,
            origin - x + y
        ];
    }

    /// <summary>
    /// Maps a finite rectangular patch of a plane into a triangle buffer.
    /// </summary>
    /// <param name="buffer">
    /// The rendering buffer that will receive the generated surface data.
    /// </param>
    /// <param name="plane">
    /// The source plane to visualize.
    /// </param>
    /// <param name="halfSize">
    /// Half of the rectangle side length used to construct the visualized plane patch.
    /// </param>
    public static void MapPlaneBuffer(RenderingBufferStorage buffer, Plane plane, double halfSize)
    {
        var corners = GetPlaneCorners(plane, halfSize);

        List<IndexTriangle> triangles =
        [
            new(0, 1, 2),
            new(0, 2, 3)
        ];

        MapTriangles(buffer, corners, triangles);
    }

    /// <summary>
    /// Maps the outline of a finite rectangular plane patch into a line buffer.
    /// </summary>
    /// <param name="buffer">
    /// The rendering buffer that will receive the generated line data.
    /// </param>
    /// <param name="plane">
    /// The source plane to visualize.
    /// </param>
    /// <param name="halfSize">
    /// Half of the rectangle side length used to construct the visualized plane patch.
    /// </param>
    public static void MapPlaneGridBuffer(RenderingBufferStorage buffer, Plane plane, double halfSize)
    {
        var corners = GetPlaneCorners(plane, halfSize);

        List<IndexLine> lines =
        [
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 0)
        ];

        MapLines(buffer, corners, lines);
    }

    /// <summary>
    /// Computes one normal per mesh vertex using the mesh normal distribution metadata.
    /// </summary>
    /// <remarks>
    /// This helper centralizes the normal extraction logic used by multiple mesh mapping methods.
    /// </remarks>
    /// <param name="mesh">
    /// The mesh whose vertex normals will be evaluated.
    /// </param>
    /// <returns>
    /// A list of normals aligned by index with <see cref="Mesh.Vertices"/>.
    /// </returns>
    private static List<XYZ> GetMeshVertexNormals(Mesh mesh)
    {
        var normals = new List<XYZ>(mesh.NumberOfNormals);

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            normals.Add(RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals));
        }

        return normals;
    }

    /// <summary>
    /// Returns the eight world-space corner points of a bounding box.
    /// </summary>
    /// <remarks>
    /// Corners are first created in the local coordinate system of the bounding box and then transformed
    /// into model coordinates using <see cref="BoundingBoxXYZ.Transform"/>.
    /// </remarks>
    /// <param name="box">
    /// The bounding box whose corners will be computed.
    /// </param>
    /// <returns>
    /// An array containing the eight transformed corner points.
    /// </returns>
    private static XYZ[] GetBoundingBoxCorners(BoundingBoxXYZ box)
    {
        XYZ[] localCorners =
        [
            new(box.Min.X, box.Min.Y, box.Min.Z),
            new(box.Max.X, box.Min.Y, box.Min.Z),
            new(box.Max.X, box.Max.Y, box.Min.Z),
            new(box.Min.X, box.Max.Y, box.Min.Z),
            new(box.Min.X, box.Min.Y, box.Max.Z),
            new(box.Max.X, box.Min.Y, box.Max.Z),
            new(box.Max.X, box.Max.Y, box.Max.Z),
            new(box.Min.X, box.Max.Y, box.Max.Z)
        ];

        return localCorners
            .Select(corner => box.Transform.OfPoint(corner))
            .ToArray();
    }

    /// <summary>
    /// Maps arbitrary vertices and triangle indices into a triangle rendering buffer.
    /// </summary>
    /// <remarks>
    /// This is a low-level utility used by higher-level mapping methods once the final triangle topology
    /// has already been computed.
    /// </remarks>
    /// <param name="buffer">
    /// The rendering buffer that will receive the generated triangle data.
    /// </param>
    /// <param name="vertices">
    /// The vertex positions to write into the vertex buffer.
    /// </param>
    /// <param name="triangles">
    /// The triangle indices to write into the index buffer.
    /// </param>
    private static void MapTriangles(RenderingBufferStorage buffer, IList<XYZ> vertices, IList<IndexTriangle> triangles)
    {
        InitializeVertexBuffer(buffer, vertices.Count);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        foreach (var vertex in vertices)
        {
            vertexStream.AddVertex(new VertexPosition(vertex));
        }
        buffer.VertexBuffer.Unmap();

        buffer.PrimitiveCount = triangles.Count;
        buffer.IndexBufferCount = triangles.Count * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();
        foreach (var triangle in triangles)
        {
            indexStream.AddTriangle(triangle);
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }

    /// <summary>
    /// Maps arbitrary vertices and line indices into a line rendering buffer.
    /// </summary>
    /// <remarks>
    /// This is a low-level utility used by higher-level mapping methods once the final line topology
    /// has already been computed.
    /// </remarks>
    /// <param name="buffer">
    /// The rendering buffer that will receive the generated line data.
    /// </param>
    /// <param name="vertices">
    /// The vertex positions to write into the vertex buffer.
    /// </param>
    /// <param name="lines">
    /// The line indices to write into the index buffer.
    /// </param>
    private static void MapLines(RenderingBufferStorage buffer, IList<XYZ> vertices, IList<IndexLine> lines)
    {
        InitializeVertexBuffer(buffer, vertices.Count);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        foreach (var vertex in vertices)
        {
            vertexStream.AddVertex(new VertexPosition(vertex));
        }
        buffer.VertexBuffer.Unmap();

        buffer.PrimitiveCount = lines.Count;
        buffer.IndexBufferCount = lines.Count * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();
        foreach (var line in lines)
        {
            indexStream.AddLine(line);
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }

    /// <summary>
    /// Initializes a position-only vertex buffer for the specified number of vertices.
    /// </summary>
    /// <remarks>
    /// The created buffer is mapped immediately and must be unmapped by the caller after all vertices
    /// have been written.
    /// </remarks>
    /// <param name="buffer">
    /// The rendering buffer whose vertex storage will be initialized.
    /// </param>
    /// <param name="vertexCount">
    /// The number of vertices that will be written to the buffer.
    /// </param>
    private static void InitializeVertexBuffer(RenderingBufferStorage buffer, int vertexCount)
    {
        buffer.VertexBufferCount = vertexCount;
        buffer.FormatBits = VertexFormatBits.Position;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * vertexCount;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
    }
}