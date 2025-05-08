using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Render;

namespace RevitDevTool.Visualization.Helpers;

public static class RenderHelper
{
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

    public static void MapNormalVectorBufferForMultiplePoints(RenderingBufferStorage buffer, IList<XYZ> points, XYZ direction, double length)
    {
        if (points.Count == 0) return;
        
        int totalLineCount = points.Count;
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
    
    public static void MapSideBufferForMultiplePoints(RenderingBufferStorage buffer, IList<XYZ> points, XYZ normal, double axisLength)
    {
        if (points.Count == 0) return;
        
        int totalQuadCount = points.Count;
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
            XYZ perpendicular1;
            if (Math.Abs(normalizedNormal.Z) < 0.9)
                perpendicular1 = XYZ.BasisZ.CrossProduct(normalizedNormal).Normalize();
            else
                perpendicular1 = XYZ.BasisX.CrossProduct(normalizedNormal).Normalize();
                
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

    // Add methods to handle multiple meshes
    public static void MapSurfaceBuffer(RenderingBufferStorage buffer, List<Mesh> meshes, double offset)
    {
        if (meshes.Count == 0) return;
        
        // Calculate total vertex and triangle counts
        int totalVertexCount = 0;
        int totalTriangleCount = 0;
        
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
        int vertexOffset = 0;
        
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
    
    public static void MapMeshGridBuffer(RenderingBufferStorage buffer, List<Mesh> meshes, double offset)
    {
        if (meshes.Count == 0) return;
        
        // Count total edges and vertices
        int totalVertexCount = 0;
        int totalEdgeCount = 0;
        
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
        int vertexOffset = 0;
        
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

    // Method to collect and combine meshes from multiple faces
    public static List<Mesh> CollectMeshesFromFaces(IList<Face> faces)
    {
        var meshes = new List<Mesh>();
        foreach (var face in faces)
        {
            var mesh = face.Triangulate();
            meshes.Add(mesh);
        }
        return meshes;
    }
    
    // Method to collect face normal data for visualization
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
    
    // Method to map normal vectors for multiple faces
    public static void MapNormalVectorsForFaces(RenderingBufferStorage buffer, List<(XYZ Point, XYZ Normal, double Length)> normalData)
    {
        if (normalData.Count == 0) return;
        
        int totalLineCount = normalData.Count;
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
}