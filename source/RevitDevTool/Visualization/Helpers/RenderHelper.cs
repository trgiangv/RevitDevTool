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
            var offsetVertex = vertex + normal * offset;
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
            var index0 = (int) meshTriangle.get_Index(0);
            var index1 = (int) meshTriangle.get_Index(1);
            var index2 = (int) meshTriangle.get_Index(2);
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
                var currentStart = i * segmentVerticesCount + j;
                var nextStart = (i + 1) * segmentVerticesCount + j;
                var currentEnd = i * segmentVerticesCount + (j + 1) % segmentVerticesCount;
                var nextEnd = (i + 1) * segmentVerticesCount + (j + 1) % segmentVerticesCount;

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
                var currentStart = i * segmentVerticesCount + j;
                var nextStart = (i + 1) * segmentVerticesCount + j;
                var currentEnd = i * segmentVerticesCount + (j + 1) % segmentVerticesCount;
                var nextEnd = (i + 1) * segmentVerticesCount + (j + 1) % segmentVerticesCount;

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
        buffer.PrimitiveCount = 3 * triangleCount * 2 + mesh.Vertices.Count;

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
            var offsetVertex = vertex + normal * offset;
            var vertexPosition = new VertexPosition(offsetVertex);
            vertexStream.AddVertex(vertexPosition);
        }

        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = (3 * triangleCount * 2 + mesh.Vertices.Count) * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();

        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            var index0 = (int) meshTriangle.get_Index(0);
            var index1 = (int) meshTriangle.get_Index(1);
            var index2 = (int) meshTriangle.get_Index(2);

            indexStream.AddLine(new IndexLine(index0, index1));
            indexStream.AddLine(new IndexLine(index1, index2));
            indexStream.AddLine(new IndexLine(index2, index0));
        }

        for (var i = 0; i < triangleCount; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            var index0 = (int) meshTriangle.get_Index(0) + vertexCount;
            var index1 = (int) meshTriangle.get_Index(1) + vertexCount;
            var index2 = (int) meshTriangle.get_Index(2) + vertexCount;

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
    /// Configures the specified <see cref="RenderingBufferStorage"/> to represent a rectangular buffer defined by the
    /// given minimum and maximum points in 3D space.
    /// </summary>
    /// <remarks>The method calculates the vertices and indices required to represent a rectangle in 3D space
    /// based on the provided <paramref name="min"/> and <paramref name="max"/> points. The rectangle is oriented
    /// perpendicular to the axis determined by the normalized direction vector between the two points.  The <paramref
    /// name="buffer"/> is updated with the following: <list type="bullet"> <item><description>Four vertices
    /// representing the corners of the rectangle.</description></item> <item><description>Two triangles forming the
    /// rectangle, defined by their vertex indices.</description></item> </list> The method assumes that the <paramref
    /// name="buffer"/> is properly initialized before being passed in.</remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> to be configured. This object will be updated with vertex and index
    /// data.</param>
    /// <param name="min">The minimum point of the rectangle in 3D space.</param>
    /// <param name="max">The maximum point of the rectangle in 3D space.</param>
    public static void MapSideBuffer(RenderingBufferStorage buffer, XYZ min, XYZ max)
    {
        const int vertexCount = 4;
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

    /// <summary>
    /// Maps a collection of bounding boxes to a rendering buffer by generating vertex and index data for their
    /// surfaces.
    /// </summary>
    /// <remarks>This method processes each bounding box in the provided collection, calculates its corner
    /// points, and generates the necessary vertex and index data to represent the surfaces of the bounding boxes as
    /// triangles. The resulting data is stored in the specified rendering buffer.  The method assumes that the bounding
    /// boxes are defined in a coordinate space that can be transformed using the <see cref="BoundingBoxXYZ.Transform"/>
    /// property. The generated vertex data includes the transformed corner points, and the index data defines the
    /// triangles that form the surfaces of the bounding boxes.  The rendering buffer is updated with the following:
    /// <list type="bullet"> <item><description>Vertex buffer containing the corner points of all bounding
    /// boxes.</description></item> <item><description>Index buffer defining the triangles for rendering the
    /// surfaces.</description></item> <item><description>Vertex format and primitive count for
    /// rendering.</description></item> </list></remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance where the vertex and index data will be stored.</param>
    /// <param name="boxes">A collection of <see cref="BoundingBoxXYZ"/> objects representing the bounding boxes to be mapped.</param>
    public static void MapBoundingBoxSurfaceBuffer(RenderingBufferStorage buffer, List<BoundingBoxXYZ> boxes)
    {
        var allCorners = new List<XYZ>();
        var allTriangles = new List<int>();
        var vertexOffset = 0;
        foreach (var box in boxes)
        {
            var minPoint = box.Transform.OfPoint(box.Min);
            var maxPoint = box.Transform.OfPoint(box.Max);
            XYZ[] corners =
            [
                new(minPoint.X, minPoint.Y, minPoint.Z),
                new(maxPoint.X, minPoint.Y, minPoint.Z),
                new(maxPoint.X, maxPoint.Y, minPoint.Z),
                new(minPoint.X, maxPoint.Y, minPoint.Z),
                new(minPoint.X, minPoint.Y, maxPoint.Z),
                new(maxPoint.X, minPoint.Y, maxPoint.Z),
                new(maxPoint.X, maxPoint.Y, maxPoint.Z),
                new(minPoint.X, maxPoint.Y, maxPoint.Z)
            ];
            int[] triangles =
            [
                0, 1, 2, 2, 3, 0,
                4, 5, 6, 6, 7, 4,
                0, 4, 5, 5, 1, 0,
                1, 5, 6, 6, 2, 1,
                2, 6, 7, 7, 3, 2,
                3, 7, 4, 4, 0, 3
            ];
            allCorners.AddRange(corners);
            allTriangles.AddRange(triangles.Select(idx => idx + vertexOffset));
            vertexOffset += corners.Length;
        }
        buffer.VertexBufferCount = allCorners.Count;
        buffer.PrimitiveCount = allTriangles.Count / 3;
        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        foreach (var corner in allCorners)
        {
            var vertexPosition = new VertexPosition(corner);
            vertexStream.AddVertex(vertexPosition);
        }
        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = allTriangles.Count * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);
        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();
        for (var i = 0; i < allTriangles.Count; i += 3)
        {
            indexStream.AddTriangle(new IndexTriangle(allTriangles[i], allTriangles[i + 1], allTriangles[i + 2]));
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
        var minPoint = box.Transform.OfPoint(box.Min);
        var maxPoint = box.Transform.OfPoint(box.Max);

        XYZ[] corners =
        [
            new(minPoint.X, minPoint.Y, minPoint.Z),
            new(maxPoint.X, minPoint.Y, minPoint.Z),
            new(maxPoint.X, maxPoint.Y, minPoint.Z),
            new(minPoint.X, maxPoint.Y, minPoint.Z),
            new(minPoint.X, minPoint.Y, maxPoint.Z),
            new(maxPoint.X, minPoint.Y, maxPoint.Z),
            new(maxPoint.X, maxPoint.Y, maxPoint.Z),
            new(minPoint.X, maxPoint.Y, maxPoint.Z)
        ];

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
    /// Maps a collection of bounding boxes into a rendering buffer by generating vertex and edge data.
    /// </summary>
    /// <remarks>This method processes each bounding box by calculating its transformed corner points and
    /// edges,  and then populates the provided rendering buffer with the corresponding vertex and index data.  The
    /// method ensures that the buffer is properly mapped and unmapped during the operation.</remarks>
    /// <param name="buffer">The rendering buffer to populate with vertex and edge data. This buffer will be updated with the  vertices and
    /// edges representing the bounding boxes.</param>
    /// <param name="boxes">A list of <see cref="BoundingBoxXYZ"/> objects representing the bounding boxes to be mapped into the buffer.
    /// Each bounding box is transformed and its corners and edges are calculated.</param>
    public static void MapBoundingBoxEdgeBuffer(RenderingBufferStorage buffer, List<BoundingBoxXYZ> boxes)
    {
        var allCorners = new List<XYZ>();
        var allEdges = new List<int>();
        var vertexOffset = 0;
        foreach (var box in boxes)
        {
            var minPoint = box.Transform.OfPoint(box.Min);
            var maxPoint = box.Transform.OfPoint(box.Max);
            XYZ[] corners =
            [
                new(minPoint.X, minPoint.Y, minPoint.Z),
                new(maxPoint.X, minPoint.Y, minPoint.Z),
                new(maxPoint.X, maxPoint.Y, minPoint.Z),
                new(minPoint.X, maxPoint.Y, minPoint.Z),
                new(minPoint.X, minPoint.Y, maxPoint.Z),
                new(maxPoint.X, minPoint.Y, maxPoint.Z),
                new(maxPoint.X, maxPoint.Y, maxPoint.Z),
                new(minPoint.X, maxPoint.Y, maxPoint.Z)
            ];
            int[] edges =
            [
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            ];
            allCorners.AddRange(corners);
            allEdges.AddRange(edges.Select(idx => idx + vertexOffset));
            vertexOffset += corners.Length;
        }
        buffer.VertexBufferCount = allCorners.Count;
        buffer.PrimitiveCount = allEdges.Count / 2;
        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        foreach (var corner in allCorners)
        {
            var vertexPosition = new VertexPosition(corner);
            vertexStream.AddVertex(vertexPosition);
        }
        buffer.VertexBuffer.Unmap();
        buffer.IndexBufferCount = allEdges.Count * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);
        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();
        for (var i = 0; i < allEdges.Count; i += 2)
        {
            indexStream.AddLine(new IndexLine(allEdges[i], allEdges[i + 1]));
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
        var minPoint = box.Transform.OfPoint(box.Min);
        var maxPoint = box.Transform.OfPoint(box.Max);

        XYZ[] corners =
        [
            new(minPoint.X, minPoint.Y, minPoint.Z),
            new(maxPoint.X, minPoint.Y, minPoint.Z),
            new(maxPoint.X, maxPoint.Y, minPoint.Z),
            new(minPoint.X, maxPoint.Y, minPoint.Z),
            new(minPoint.X, minPoint.Y, maxPoint.Z),
            new(maxPoint.X, minPoint.Y, maxPoint.Z),
            new(maxPoint.X, maxPoint.Y, maxPoint.Z),
            new(minPoint.X, maxPoint.Y, maxPoint.Z)
        ];

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

        var endPoint = origin + vector * length;
        var arrowHeadBase = endPoint - vector * headSize;
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
    /// Maps a buffer to represent normal vectors originating from multiple points in 3D space.
    /// </summary>
    /// <remarks>This method generates a set of line segments, where each line represents a normal vector
    /// originating from a point in the <paramref name="points"/> collection. The direction and length of each normal
    /// vector are determined by the <paramref name="direction"/> and <paramref name="length"/> parameters,
    /// respectively. The resulting vertex and index data are stored in the provided <paramref name="buffer"/> for
    /// rendering.</remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance where the vertex and index data for the normal vectors will be
    /// stored.</param>
    /// <param name="points">A collection of <see cref="XYZ"/> points representing the starting positions of the normal vectors.</param>
    /// <param name="direction">The <see cref="XYZ"/> direction vector that determines the orientation of the normal vectors. This vector will
    /// be normalized.</param>
    /// <param name="length">The length of each normal vector. Must be a positive value.</param>
    public static void MapNormalVectorBufferForMultiplePoints(RenderingBufferStorage buffer, IList<XYZ> points, XYZ direction, double length)
    {
        if (points.Count == 0) return;
        
        var totalLineCount = points.Count;
        buffer.VertexBufferCount = totalLineCount * 2; // 2 points per line
        buffer.PrimitiveCount = totalLineCount;
        
        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
        
        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        
        // Add all start and end points for each normal vector
        foreach (var point in points)
        {
            var normalizedDirection = direction.Normalize();
            var endPoint = point + normalizedDirection * length;
            
            vertexStream.AddVertex(new VertexPosition(point));
            vertexStream.AddVertex(new VertexPosition(endPoint));
        }
        
        buffer.VertexBuffer.Unmap();
        
        // Create index buffer for lines
        buffer.IndexBufferCount = totalLineCount * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);
        
        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();
        
        // Add lines connecting start and end points
        for (var i = 0; i < totalLineCount; i++)
        {
            indexStream.AddLine(new IndexLine(i * 2, i * 2 + 1));
        }
        
        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }
    
    /// <summary>
    /// Maps a rendering buffer to represent a series of quads, each centered at a specified point,  with a given normal
    /// and axis-aligned dimensions.
    /// </summary>
    /// <remarks>This method generates vertex and index data for a series of quads, where each quad is 
    /// centered at a point in the <paramref name="points"/> collection. The quads are oriented  based on the specified
    /// <paramref name="normal"/> vector, and their size is determined by  the <paramref name="axisLength"/> parameter.
    /// The resulting data is stored in the provided  <paramref name="buffer"/> for rendering purposes.  The method
    /// ensures that the buffer is properly mapped and populated with the required  vertex and index data. Each quad is
    /// represented by four vertices and two triangles,  resulting in a total of eight indices per quad.  If the
    /// <paramref name="points"/> collection is empty, the method returns immediately  without modifying the
    /// buffer.</remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance to be populated with vertex and index data  for the quads.</param>
    /// <param name="points">A collection of <see cref="XYZ"/> points, where each point represents the center of a quad.</param>
    /// <param name="normal">The normal vector defining the orientation of the quads. This vector is used to calculate  the plane on which
    /// the quads lie.</param>
    /// <param name="axisLength">The length of the axes used to define the size of each quad. Each quad will have sides  proportional to twice
    /// this length.</param>
    public static void MapSideBufferForMultiplePoints(RenderingBufferStorage buffer, IList<XYZ> points, XYZ normal, double axisLength)
    {
        if (points.Count == 0) return;
        
        var totalQuadCount = points.Count;
        buffer.VertexBufferCount = totalQuadCount * 4; // 4 corners per quad
        buffer.PrimitiveCount = totalQuadCount * 2; // 2 triangles per quad
        
        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
        
        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        
        // Create quads for each point
        foreach (var point in points)
        {
            var normalizedNormal = normal.Normalize();
            
            // Calculate perpendicular vectors to create a plane
            var perpendicular1 = Math.Abs(normalizedNormal.Z) < 0.9 
                ? XYZ.BasisZ.CrossProduct(normalizedNormal).Normalize() 
                : XYZ.BasisX.CrossProduct(normalizedNormal).Normalize();
                
            var perpendicular2 = normalizedNormal.CrossProduct(perpendicular1).Normalize();
            
            // Scale perpendicular vectors
            perpendicular1 *= axisLength;
            perpendicular2 *= axisLength;
            
            // Create quad corners
            var corner1 = point + perpendicular1 + perpendicular2;
            var corner2 = point + perpendicular1 - perpendicular2;
            var corner3 = point - perpendicular1 - perpendicular2;
            var corner4 = point - perpendicular1 + perpendicular2;
            
            // Add vertices
            vertexStream.AddVertex(new VertexPosition(corner1));
            vertexStream.AddVertex(new VertexPosition(corner2));
            vertexStream.AddVertex(new VertexPosition(corner3));
            vertexStream.AddVertex(new VertexPosition(corner4));
        }
        
        buffer.VertexBuffer.Unmap();
        
        // Create index buffer for triangles
        buffer.IndexBufferCount = totalQuadCount * 2 * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);
        
        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();
        
        // Add triangles for each quad
        for (var i = 0; i < totalQuadCount; i++)
        {
            var baseIndex = i * 4;
            
            // First triangle
            indexStream.AddTriangle(new IndexTriangle(baseIndex, baseIndex + 1, baseIndex + 2));
            
            // Second triangle
            indexStream.AddTriangle(new IndexTriangle(baseIndex, baseIndex + 2, baseIndex + 3));
        }
        
        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }

    /// <summary>
    /// Maps the provided collection of meshes into the specified rendering buffer, applying a positional offset to each
    /// vertex.
    /// </summary>
    /// <remarks>This method processes the provided meshes to calculate the total vertex and triangle counts,
    /// allocates the necessary buffers,  and maps the vertex and index data into the rendering buffer. Each vertex is
    /// adjusted by the specified offset along its normal direction. The method also ensures that triangle indices are
    /// adjusted to account for the vertex offsets of preceding meshes in the collection.</remarks>
    /// <param name="buffer">The rendering buffer where the vertex and index data will be stored. This buffer will be updated with the
    /// calculated vertex and index data.</param>
    /// <param name="meshes">A collection of meshes to be processed and added to the rendering buffer. Each mesh contributes its vertices and
    /// triangles to the buffer.</param>
    /// <param name="offset">The positional offset to apply to each vertex in the meshes. This offset is applied along the vertex normal
    /// direction.</param>
    public static void MapSurfaceBuffer(RenderingBufferStorage buffer, List<Mesh> meshes, double offset)
    {
        if (meshes.Count == 0) return;
        
        // Calculate total vertex and triangle counts
        var totalVertexCount = 0;
        var totalTriangleCount = 0;
        
        foreach (var mesh in meshes)
        {
            totalVertexCount += mesh.Vertices.Count;
            totalTriangleCount += mesh.NumTriangles;
        }
        
        buffer.VertexBufferCount = totalVertexCount;
        buffer.PrimitiveCount = totalTriangleCount;
        
        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
        
        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        
        buffer.IndexBufferCount = totalTriangleCount * IndexTriangle.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);
        
        var indexStream = buffer.IndexBuffer.GetIndexStreamTriangle();
        
        // Process each mesh and add to buffers with correct vertex offsets
        var vertexOffset = 0;
        
        foreach (var mesh in meshes)
        {
            var normals = new List<XYZ>(mesh.NumberOfNormals);
            
            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);
                normals.Add(normal);
            }
            
            // Add vertices
            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                var normal = normals[i];
                var offsetVertex = vertex + normal * offset;
                var vertexPosition = new VertexPosition(offsetVertex);
                vertexStream.AddVertex(vertexPosition);
            }
            
            // Add triangles with adjusted indices
            for (var i = 0; i < mesh.NumTriangles; i++)
            {
                var meshTriangle = mesh.get_Triangle(i);
                var index0 = (int)meshTriangle.get_Index(0) + vertexOffset;
                var index1 = (int)meshTriangle.get_Index(1) + vertexOffset;
                var index2 = (int)meshTriangle.get_Index(2) + vertexOffset;
                indexStream.AddTriangle(new IndexTriangle(index0, index1, index2));
            }
            
            vertexOffset += mesh.Vertices.Count;
        }
        
        buffer.VertexBuffer.Unmap();
        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }
    
    /// <summary>
    /// Maps a collection of meshes into a rendering buffer, generating vertex and index data for rendering a grid of
    /// mesh edges with an optional offset applied to the vertices.
    /// </summary>
    /// <remarks>This method processes each mesh in the provided list, calculating the total number of 
    /// vertices and edges, and populates the rendering buffer with the corresponding vertex  and index data. The vertex
    /// data includes positions adjusted by the specified offset,  and the index data represents the edges of the mesh
    /// triangles. <para> The rendering buffer is initialized with the appropriate size and format to accommodate  the
    /// combined data from all meshes. The method ensures that vertex indices are adjusted  correctly to account for the
    /// offset between meshes in the buffer. </para> <para> If the <paramref name="meshes"/> list is empty, the method
    /// returns immediately without  modifying the buffer. </para></remarks>
    /// <param name="buffer">The rendering buffer where the vertex and index data will be stored.  This buffer will be initialized and
    /// populated with the calculated data.</param>
    /// <param name="meshes">A list of meshes to be processed. Each mesh contributes its vertices  and edges to the rendering buffer.</param>
    /// <param name="offset">A value applied to offset each vertex along its normal direction.  This can be used to create a visual effect,
    /// such as expanding the mesh outward.</param>
    public static void MapMeshGridBuffer(RenderingBufferStorage buffer, List<Mesh> meshes, double offset)
    {
        if (meshes.Count == 0) return;
        
        // Count total edges and vertices
        var totalVertexCount = 0;
        var totalEdgeCount = 0;
        
        foreach (var mesh in meshes)
        {
            totalVertexCount += mesh.Vertices.Count;
            totalEdgeCount += mesh.NumTriangles * 3; // Each triangle has 3 edges
        }
        
        buffer.VertexBufferCount = totalVertexCount;
        buffer.PrimitiveCount = totalEdgeCount;
        
        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
        
        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        
        buffer.IndexBufferCount = totalEdgeCount * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);
        
        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();
        
        // Process each mesh and add to buffers with correct vertex offsets
        var vertexOffset = 0;
        
        foreach (var mesh in meshes)
        {
            var normals = new List<XYZ>(mesh.NumberOfNormals);
            
            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);
                normals.Add(normal);
            }
            
            // Add vertices
            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                var normal = normals[i];
                var offsetVertex = vertex + normal * offset;
                var vertexPosition = new VertexPosition(offsetVertex);
                vertexStream.AddVertex(vertexPosition);
            }
            
            // Add edges with adjusted indices
            for (var i = 0; i < mesh.NumTriangles; i++)
            {
                var meshTriangle = mesh.get_Triangle(i);
                var index0 = (int)meshTriangle.get_Index(0) + vertexOffset;
                var index1 = (int)meshTriangle.get_Index(1) + vertexOffset;
                var index2 = (int)meshTriangle.get_Index(2) + vertexOffset;
                
                indexStream.AddLine(new IndexLine(index0, index1));
                indexStream.AddLine(new IndexLine(index1, index2));
                indexStream.AddLine(new IndexLine(index2, index0));
            }
            
            vertexOffset += mesh.Vertices.Count;
        }
        
        buffer.VertexBuffer.Unmap();
        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }

    public static List<Mesh> CollectMeshesFromFaces(IList<Face> faces)
    {
        return faces.Select(face => face.Triangulate()).ToList();
    }
    
    /// <summary>
    /// Collects data about the normals of a set of faces, including their points, directions, and lengths.
    /// </summary>
    /// <remarks>The method calculates the center of each face's bounding box, computes the normal at that
    /// center,  and adjusts the resulting point by applying an offset and the specified extrusion along the normal
    /// direction.  The length of the normal vector is interpolated based on the area of the face.</remarks>
    /// <param name="faces">A collection of <see cref="Face"/> objects for which normal data will be calculated.</param>
    /// <param name="extrusion">An additional extrusion value applied to the calculated points along the normal direction.</param>
    /// <returns>A list of tuples, where each tuple contains: <list type="bullet"> <item><description>An <see cref="XYZ"/> point
    /// representing the evaluated position on the face, adjusted by the normal and extrusion.</description></item>
    /// <item><description>An <see cref="XYZ"/> vector representing the normal direction at the evaluated
    /// point.</description></item> <item><description>A <see cref="double"/> value representing the length of the
    /// normal vector, interpolated based on the face's area.</description></item> </list></returns>
    public static List<(XYZ Point, XYZ Normal, double Length)> CollectFaceNormalData(IList<Face> faces, double extrusion)
    {
        var normalData = new List<(XYZ Point, XYZ Normal, double Length)>();
        
        foreach (var face in faces)
        {
            var faceBox = face.GetBoundingBox();
            var center = (faceBox.Min + faceBox.Max) / 2;
            var normal = face.ComputeNormal(center);
            var offset = RenderGeometryHelper.InterpolateOffsetByArea(face.Area);
            var normalLength = RenderGeometryHelper.InterpolateAxisLengthByArea(face.Area);
            
            var point = face.Evaluate(center) + normal * (offset + extrusion);
            normalData.Add((point, normal, normalLength));
        }
        
        return normalData;
    }
    
    /// <summary>
    /// Maps normal vectors to line segments in a rendering buffer for visualization purposes.
    /// </summary>
    /// <remarks>This method populates the provided <paramref name="buffer"/> with vertex and index data to
    /// represent the normal vectors as line segments. Each normal vector is visualized as a line starting at the given
    /// point and extending in the direction of the normalized vector, scaled by the specified length. <para> The method
    /// updates the following properties of the <paramref name="buffer"/>: <list type="bullet">
    /// <item><description><c>VertexBuffer</c>: Stores the start and end points of the line
    /// segments.</description></item> <item><description><c>IndexBuffer</c>: Defines the line segments connecting the
    /// start and end points.</description></item> <item><description><c>VertexBufferCount</c>, <c>PrimitiveCount</c>,
    /// and <c>IndexBufferCount</c>: Reflect the number of vertices, primitives, and indices,
    /// respectively.</description></item> </list> </para></remarks>
    /// <param name="buffer">The rendering buffer where the vertex and index data for the normal vectors will be stored.</param>
    /// <param name="normalData">A list of tuples containing the start point, normal vector, and length of each normal vector. Each tuple
    /// consists of: <list type="bullet"> <item><description><see cref="XYZ"/> Point: The starting point of the normal
    /// vector.</description></item> <item><description><see cref="XYZ"/> Normal: The direction of the normal vector,
    /// which will be normalized.</description></item> <item><description><see cref="double"/> Length: The length of the
    /// normal vector.</description></item> </list></param>
    public static void MapNormalVectorsForFaces(RenderingBufferStorage buffer, List<(XYZ Point, XYZ Normal, double Length)> normalData)
    {
        if (normalData.Count == 0) return;
        
        var totalLineCount = normalData.Count;
        buffer.VertexBufferCount = totalLineCount * 2; // 2 points per line
        buffer.PrimitiveCount = totalLineCount;
        
        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);
        
        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();
        
        // Add all start and end points for each normal vector
        foreach (var (point, normal, length) in normalData)
        {
            var normalizedDirection = normal.Normalize();
            var startPoint = point;
            var endPoint = point + normalizedDirection * length;
            
            vertexStream.AddVertex(new VertexPosition(startPoint));
            vertexStream.AddVertex(new VertexPosition(endPoint));
        }
        
        buffer.VertexBuffer.Unmap();
        
        // Create index buffer for lines
        buffer.IndexBufferCount = totalLineCount * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);
        
        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();
        
        // Add lines connecting start and end points
        for (var i = 0; i < totalLineCount; i++)
        {
            indexStream.AddLine(new IndexLine(i * 2, i * 2 + 1));
        }
        
        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }

    /// <summary>
    /// Maps normal vectors to arrow representations and stores them in the specified rendering buffer.
    /// </summary>
    /// <remarks>This method generates arrow representations for a set of normal vectors, where each arrow
    /// consists of a shaft and an arrowhead. The arrow's length and orientation are determined by the provided normal
    /// vector and length. The generated vertices and indices are stored in the provided rendering buffer for use in
    /// rendering pipelines.  The method ensures that the rendering buffer is properly configured with the required
    /// vertex and index data, including mapping and unmapping the buffer resources.</remarks>
    /// <param name="buffer">The <see cref="RenderingBufferStorage"/> instance where the arrow vertices and indices will be stored.</param>
    /// <param name="normalData">A list of tuples containing the origin point, normal vector, and length for each arrow to be generated. Each
    /// tuple represents a normal vector to be visualized as an arrow.</param>
    public static void MapNormalArrowVectorsForFaces(RenderingBufferStorage buffer, List<(XYZ Point, XYZ Normal, double Length)> normalData)
    {
        if (normalData.Count == 0) return;
        
        // Each arrow: 4 vertices (shaft start, shaft end, head left, head right), 3 lines (shaft, left head, right head)
        const int arrowVertexCount = 4;
        const int arrowLineCount = 3;
        var totalVertexCount = normalData.Count * arrowVertexCount;
        var totalLineCount = normalData.Count * arrowLineCount;

        buffer.VertexBufferCount = totalVertexCount;
        buffer.PrimitiveCount = totalLineCount;

        var vertexBufferSizeInFloats = VertexPosition.GetSizeInFloats() * buffer.VertexBufferCount;
        buffer.FormatBits = VertexFormatBits.Position;
        buffer.VertexBuffer = new VertexBuffer(vertexBufferSizeInFloats);
        buffer.VertexBuffer.Map(vertexBufferSizeInFloats);

        var vertexStream = buffer.VertexBuffer.GetVertexStreamPosition();

        // Store all arrow vertices
        foreach (var (point, normal, length) in normalData)
        {
            var normalized = normal.Normalize();
            var origin = point;
            var endPoint = origin + normalized * length;
            var headSize = length > 1 ? 0.2 : length * 0.2;
            var arrowHeadBase = endPoint - normalized * headSize;
            var basisVector = Math.Abs(normalized.Z).IsAlmostEqual(1) ? XYZ.BasisY : XYZ.BasisZ;
            var perpendicular1 = normalized.CrossProduct(basisVector).Normalize().Multiply(headSize * 0.5);

            // 0: origin, 1: endPoint, 2: head left, 3: head right
            vertexStream.AddVertex(new VertexPosition(origin));
            vertexStream.AddVertex(new VertexPosition(endPoint));
            vertexStream.AddVertex(new VertexPosition(arrowHeadBase + perpendicular1));
            vertexStream.AddVertex(new VertexPosition(arrowHeadBase - perpendicular1));
        }

        buffer.VertexBuffer.Unmap();

        buffer.IndexBufferCount = totalLineCount * IndexLine.GetSizeInShortInts();
        buffer.IndexBuffer = new IndexBuffer(buffer.IndexBufferCount);
        buffer.IndexBuffer.Map(buffer.IndexBufferCount);

        var indexStream = buffer.IndexBuffer.GetIndexStreamLine();
        for (var i = 0; i < normalData.Count; i++)
        {
            var baseIdx = i * arrowVertexCount;
            // Shaft
            indexStream.AddLine(new IndexLine(baseIdx + 0, baseIdx + 1));
            // Arrow head
            indexStream.AddLine(new IndexLine(baseIdx + 1, baseIdx + 2));
            indexStream.AddLine(new IndexLine(baseIdx + 1, baseIdx + 3));
        }

        buffer.IndexBuffer.Unmap();
        buffer.VertexFormat = new VertexFormat(buffer.FormatBits);
    }
}