# /// script
# dependencies = [
#     "shapely==2.1.2",
#     "trimesh==4.11.2",
#     "pydantic==2.12.5",
# ]
# ///

from Autodesk.Revit import DB
import trimesh


def create_simple_box():
    cube = trimesh.creation.box(extents=(2, 2, 2))
    edges = cube.edges_unique
    vertices = cube.vertices

    bbox = DB.BoundingBoxXYZ()
    bbox.Min = DB.XYZ(vertices.min(axis=0)[0], vertices.min(axis=0)[1], vertices.min(axis=0)[2])
    bbox.Max = DB.XYZ(vertices.max(axis=0)[0], vertices.max(axis=0)[1], vertices.max(axis=0)[2])
    print(bbox)

    lines = []

    # 2. Convert edges to Revit Lines
    for edge in edges:
        start_vertex = vertices[edge[0]]
        end_vertex = vertices[edge[1]]

        start_point = DB.XYZ(start_vertex[0], start_vertex[1], start_vertex[2])
        end_point = DB.XYZ(end_vertex[0], end_vertex[1], end_vertex[2])

        try:
            line = DB.Line.CreateBound(start_point, end_point)
            lines.append(line)
        except Exception as e:
            print(f"Error creating line: {e}")

    # print(lines) 
    print(f"Created {len(lines)} lines in Revit from Trimesh cube edges.")

def create_complex_shape():
    sphere = trimesh.creation.icosphere(subdivisions=2, radius=1.0)
    edges = sphere.edges_unique
    vertices = sphere.vertices

    lines = []

    for edge in edges:
        start_vertex = vertices[edge[0]]
        end_vertex = vertices[edge[1]]

        start_point = DB.XYZ(start_vertex[0], start_vertex[1], start_vertex[2])
        end_point = DB.XYZ(end_vertex[0], end_vertex[1], end_vertex[2])

        try:
            line = DB.Line.CreateBound(start_point, end_point)
            lines.append(line)
        except Exception as e:
            print(f"Error creating line: {e}")

    print(lines)
    print(f"Created {len(lines)} lines in Revit from Trimesh sphere edges.")

if __name__ == "__main__":
    create_simple_box()
    create_complex_shape()