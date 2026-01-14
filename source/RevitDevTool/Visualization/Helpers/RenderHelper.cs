using Autodesk.Revit.DB.DirectContext3D;
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

        buffer.VertexBufferCount = vertexCount;
        buffer.PrimitiveCount = triangleCount;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        var normals = new List<XYZ>(mesh.NumberOfNormals);

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);
            normals.Add(normal);
        }

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var vertex = mesh.Vertices[i];
            var normal = normals[i];
            var offsetVertex = vertex + (normal * offset);
            var vertexPosition = new VertexPosition(offsetVertex);
            vertexStream.AddVertex(vertexPosition);
        }

        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = triangleCount * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();

        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            var index0 = (int)meshTriangle.get_Index(0);
            var index1 = (int)meshTriangle.get_Index(1);
            var index2 = (int)meshTriangle.get_Index(2);
            indexStream.AddTriangle(new IndexTriangle(index0, index1, index2));
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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
        var vertexCount = vertices.Count;

        buffer.VertexBufferCount = vertexCount;
        buffer.PrimitiveCount = vertexCount - 1;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();

        foreach (var vertex in vertices)
        {
            var vertexPosition = new VertexPosition(vertex);
            vertexStream.AddVertex(vertexPosition);
        }

        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = (vertexCount - 1) * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();

        for (var i = 0; i < vertexCount - 1; i++)
        {
            indexStream.AddLine(new IndexLine(i, i + 1));
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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
        var newVertexCount = vertices.Count * segmentVerticesCount;

        buffer.VertexBufferCount = newVertexCount;
        buffer.PrimitiveCount = (vertices.Count - 1) * segmentVerticesCount * 4;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();

        foreach (var segment in tubeSegments)
        {
            foreach (var point in segment)
            {
                var vertexPosition = new VertexPosition(point);
                vertexStream.AddVertex(vertexPosition);
            }
        }

        buffer.VertexBuffer.Unmap();

        buffer.IndexBufferCount = (vertices.Count - 1) * segmentVerticesCount * 4 * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();

        for (var i = 0; i < vertices.Count - 1; i++)
        {
            for (var j = 0; j < segmentVerticesCount; j++)
            {
                var currentStart = (i * segmentVerticesCount) + j;
                var nextStart = ((i + 1) * segmentVerticesCount) + j;
                var currentEnd = (i * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);
                var nextEnd = ((i + 1) * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);

                // First triangle
                indexStream.AddLine(new IndexLine(currentStart, nextStart));
                indexStream.AddLine(new IndexLine(nextStart, nextEnd));

                // Second triangle
                indexStream.AddLine(new IndexLine(nextEnd, currentEnd));
                indexStream.AddLine(new IndexLine(currentEnd, currentStart));
            }
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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
        var newVertexCount = vertices.Count * segmentVerticesCount;

        buffer.VertexBufferCount = newVertexCount;
        buffer.PrimitiveCount = (vertices.Count - 1) * segmentVerticesCount * 2;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();

        foreach (var segment in tubeSegments)
        {
            foreach (var point in segment)
            {
                var vertexPosition = new VertexPosition(point);
                vertexStream.AddVertex(vertexPosition);
            }
        }

        buffer.VertexBuffer.Unmap();

        buffer.IndexBufferCount = (vertices.Count - 1) * segmentVerticesCount * 6 * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();

        for (var i = 0; i < vertices.Count - 1; i++)
        {
            for (var j = 0; j < segmentVerticesCount; j++)
            {
                var currentStart = (i * segmentVerticesCount) + j;
                var nextStart = ((i + 1) * segmentVerticesCount) + j;
                var currentEnd = (i * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);
                var nextEnd = ((i + 1) * segmentVerticesCount) + ((j + 1) % segmentVerticesCount);

                // First triangle
                indexStream.AddTriangle(new IndexTriangle(currentStart, nextStart, nextEnd));

                // Second triangle
                indexStream.AddTriangle(new IndexTriangle(nextEnd, currentEnd, currentStart));
            }
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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

        buffer.VertexBufferCount = vertexCount * 2;
        buffer.PrimitiveCount = (3 * triangleCount * 2) + mesh.Vertices.Count;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        var normals = new List<XYZ>(mesh.NumberOfNormals);
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);
            normals.Add(normal);
        }

        foreach (var vertex in mesh.Vertices)
        {
            var vertexPosition = new VertexPosition(vertex);
            vertexStream.AddVertex(vertexPosition);
        }

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var vertex = mesh.Vertices[i];
            var normal = normals[i];
            var offsetVertex = vertex + (normal * offset);
            var vertexPosition = new VertexPosition(offsetVertex);
            vertexStream.AddVertex(vertexPosition);
        }

        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = ((3 * triangleCount * 2) + mesh.Vertices.Count) * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();

        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            var index0 = (int)meshTriangle.get_Index(0);
            var index1 = (int)meshTriangle.get_Index(1);
            var index2 = (int)meshTriangle.get_Index(2);

            indexStream.AddLine(new IndexLine(index0, index1));
            indexStream.AddLine(new IndexLine(index1, index2));
            indexStream.AddLine(new IndexLine(index2, index0));
        }

        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            var index0 = (int)meshTriangle.get_Index(0) + vertexCount;
            var index1 = (int)meshTriangle.get_Index(1) + vertexCount;
            var index2 = (int)meshTriangle.get_Index(2) + vertexCount;

            indexStream.AddLine(new IndexLine(index0, index1));
            indexStream.AddLine(new IndexLine(index1, index2));
            indexStream.AddLine(new IndexLine(index2, index0));
        }

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            indexStream.AddLine(new IndexLine(i, i + mesh.Vertices.Count));
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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
        // Generate 8 corners in LOCAL coordinate system first
        XYZ[] localCorners =
        [
            new(box.Min.X, box.Min.Y, box.Min.Z),  // 0: min corner
            new(box.Max.X, box.Min.Y, box.Min.Z),  // 1
            new(box.Max.X, box.Max.Y, box.Min.Z),  // 2
            new(box.Min.X, box.Max.Y, box.Min.Z),  // 3
            new(box.Min.X, box.Min.Y, box.Max.Z),  // 4
            new(box.Max.X, box.Min.Y, box.Max.Z),  // 5
            new(box.Max.X, box.Max.Y, box.Max.Z),  // 6: max corner
            new(box.Min.X, box.Max.Y, box.Max.Z)   // 7
        ];

        // Transform each corner individually to world coordinates
        var corners = localCorners
            .Select(corner => box.Transform.OfPoint(corner))
            .ToArray();

        int[] triangles =
        [
            0, 1, 2, 2, 3, 0, // bottom face
            4, 5, 6, 6, 7, 4, // top face
            0, 4, 5, 5, 1, 0, // front face
            1, 5, 6, 6, 2, 1, // right face
            2, 6, 7, 7, 3, 2, // back face
            3, 7, 4, 4, 0, 3  // left face
        ];

        buffer.VertexBufferCount = corners.Length;
        buffer.PrimitiveCount = triangles.Length / 3;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();

        foreach (var corner in corners)
        {
            var vertexPosition = new VertexPosition(corner);
            vertexStream.AddVertex(vertexPosition);
        }

        buffer.VertexBuffer.Unmap();

        buffer.IndexBufferCount = triangles.Length * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();

        for (var i = 0; i < triangles.Length; i += 3)
        {
            indexStream.AddTriangle(new IndexTriangle(triangles[i], triangles[i + 1], triangles[i + 2]));
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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
        // Generate 8 corners in LOCAL coordinate system first
        XYZ[] localCorners =
        [
            new(box.Min.X, box.Min.Y, box.Min.Z),  // 0: min corner
            new(box.Max.X, box.Min.Y, box.Min.Z),  // 1
            new(box.Max.X, box.Max.Y, box.Min.Z),  // 2
            new(box.Min.X, box.Max.Y, box.Min.Z),  // 3
            new(box.Min.X, box.Min.Y, box.Max.Z),  // 4
            new(box.Max.X, box.Min.Y, box.Max.Z),  // 5
            new(box.Max.X, box.Max.Y, box.Max.Z),  // 6: max corner
            new(box.Min.X, box.Max.Y, box.Max.Z)   // 7
        ];

        // Transform each corner individually to world coordinates
        var corners = localCorners
            .Select(corner => box.Transform.OfPoint(corner))
            .ToArray();

        int[] edges =
        [
            0, 1, 1, 2, 2, 3, 3, 0, // bottom face
            4, 5, 5, 6, 6, 7, 7, 4, // top face
            0, 4, 1, 5, 2, 6, 3, 7  // vertical edges
        ];

        buffer.VertexBufferCount = corners.Length;
        buffer.PrimitiveCount = edges.Length / 2;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();

        foreach (var corner in corners)
        {
            var vertexPosition = new VertexPosition(corner);
            vertexStream.AddVertex(vertexPosition);
        }

        buffer.VertexBuffer.Unmap();

        buffer.IndexBufferCount = edges.Length * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();

        for (var i = 0; i < edges.Length; i += 2)
        {
            indexStream.AddLine(new IndexLine(edges[i], edges[i + 1]));
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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

        buffer.VertexBufferCount = 4;
        buffer.PrimitiveCount = 3;

        var vertexBufferSizeInFloats = 4 * VertexPosition.GetSizeInFloats();
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        vertexStream.AddVertex(new VertexPosition(origin));
        vertexStream.AddVertex(new VertexPosition(endPoint));
        vertexStream.AddVertex(new VertexPosition(arrowHeadBase + perpendicular1));
        vertexStream.AddVertex(new VertexPosition(arrowHeadBase - perpendicular1));

        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = 3 * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();
        indexStream.AddLine(new IndexLine(0, 1));
        indexStream.AddLine(new IndexLine(1, 2));
        indexStream.AddLine(new IndexLine(1, 3));

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
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
        var vertexCount = 4;
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

        buffer.VertexBufferCount = vertexCount;
        buffer.PrimitiveCount = 2;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        vertexStream.AddVertex(new VertexPosition(point1));
        vertexStream.AddVertex(new VertexPosition(point2));
        vertexStream.AddVertex(new VertexPosition(point3));
        vertexStream.AddVertex(new VertexPosition(point4));

        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = 2 * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();
        indexStream.AddTriangle(new IndexTriangle(0, 1, 2));
        indexStream.AddTriangle(new IndexTriangle(1, 2, 3));

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }

}