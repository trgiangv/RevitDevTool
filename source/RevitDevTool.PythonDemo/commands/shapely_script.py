# /// script
# dependencies = [
#     "shapely==2.1.2",
#     "numpy==2.4.2",
#     "openpyxl==3.1.5",
#     "pydantic==2.12.5",
# ]
# ///

from System.Diagnostics import Trace
from System.Collections.Generic import List
from Autodesk.Revit import DB
from shapely.geometry import Polygon, Point

def create_shapely_geometry_in_revit():

    # 1. Create a circle from Shapely - 2.5m radius
    circle = Point(0, 0).buffer(2.5)
    circle_coords = list(circle.exterior.coords)

    # 2. Create a square from Shapely - 3x3m
    square = Polygon([(5, 0), (8, 0), (8, 3), (5, 3), (5, 0)])
    square_coords = list(square.exterior.coords)

    # 3. Create a triangle from Shapely - 4x3.5m
    triangle = Polygon([(12, 0), (16, 0), (14, 3.5)])
    triangle_coords = list(triangle.exterior.coords)

    all_coordinates = [
        ("Circle", circle_coords),
        ("Square", square_coords),
        ("Triangle", triangle_coords),
    ]

    lines_collection = List[DB.Line]()

    # convert 2D coordinates to Revit Lines
    for shape_name, coords in all_coordinates:
        print(f"\n{shape_name}:")
        for i, (x, y) in enumerate(coords[:-1]):  # remove last point to avoid closing line
            x_next, y_next = coords[i + 1]

            # create 2D line in Revit
            start_point = DB.XYZ(x, y, 0)
            end_point = DB.XYZ(x_next, y_next, 0)

            try:
                line = DB.Line.CreateBound(start_point, end_point)
                lines_collection.Add(line)
            except Exception as e:
                print(f"  Error creating line: {e}")

    Trace.Write(lines_collection)
    print(f"\nCreated {lines_collection.Count} lines in Revit from Shapely geometries.")

    # Hiển thị thông tin các hình
    print(f"Circle area: {circle.area:.2f}, perimeter: {circle.length:.2f}")
    print(f"Square area: {square.area:.2f}, perimeter: {square.length:.2f}")
    print(f"Triangle area: {triangle.area:.2f}, perimeter: {triangle.length:.2f}")

    return lines_collection

if __name__ == "__main__":
    lines = create_shapely_geometry_in_revit()
