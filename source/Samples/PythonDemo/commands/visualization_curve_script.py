# /// script
# dependencies = []
# ///

"""
Test Curve Visualization
Demonstrates curve and edge visualization in 3D view.
Similar to RevitDevTool.DotnetDemo/CurveVisualization.cs
"""

from Autodesk.Revit import UI, DB
from Autodesk.Revit.UI.Selection import ObjectType


def test_single_edge(uidoc: UI.UIDocument):
    """Pick an edge and visualize it"""
    try:
        print("Select an edge...")
        ref = uidoc.Selection.PickObject(ObjectType.Edge, "Select an edge")

        elem = uidoc.Document.GetElement(ref)
        edge = elem.GetGeometryObjectFromReference(ref)

        if edge:
            print(f"Selected edge: {edge.GetType().Name}")
            print(edge)  # Visualize in 3D view
        else:
            print("ERROR: Could not get edge geometry")

    except Exception as e:
        print(f"ERROR: {e}")


def test_multiple_edges(uidoc: UI.UIDocument):
    """Pick multiple edges and visualize them"""
    try:
        print("Select multiple edges (ESC when done)...")
        refs = uidoc.Selection.PickObjects(ObjectType.Edge, "Select edges")

        if len(refs) == 0:
            print("WARNING: No edges selected")
            return

        print(f"Selected {len(refs)} edges")

        # Visualize each edge
        for i, ref in enumerate(refs):
            elem = uidoc.Document.GetElement(ref)
            edge = elem.GetGeometryObjectFromReference(ref)
            if edge:
                print(f"Edge {i + 1}: {edge.GetType().Name}")
                print(edge)

    except Exception as e:
        print(f"ERROR: {e}")


def test_wall_curves(uidoc: UI.UIDocument):
    """Visualize all wall location curves"""
    try:
        print("Collecting wall curves...")

        walls = DB.FilteredElementCollector(uidoc.Document).OfClass(DB.Wall).ToElements()
        count = 0

        for wall in walls:
            if wall.Location and hasattr(wall.Location, "Curve"):
                curve = wall.Location.Curve
                print(curve)  # Visualize in 3D view
                count += 1

        print(f"Visualized {count} wall curves")

    except Exception as e:
        print(f"ERROR: {e}")


def test_generated_lines():
    """Generate and visualize lines"""
    try:
        print("Generating lines...")

        # Create a square
        points = [
            DB.XYZ(0, 0, 0),
            DB.XYZ(10, 0, 0),
            DB.XYZ(10, 10, 0),
            DB.XYZ(0, 10, 0),
            DB.XYZ(0, 0, 0),  # Close the square
        ]

        for i in range(len(points) - 1):
            line = DB.Line.CreateBound(points[i], points[i + 1])
            print(f"Line {i + 1}: {points[i]} -> {points[i + 1]}")
            print(line)  # Visualize in 3D view

        print(f"Generated {len(points) - 1} lines")

    except Exception as e:
        print(f"ERROR: {e}")


def test_arc():
    """Generate and visualize an arc"""
    try:
        print("Generating arc...")

        start = DB.XYZ(0, 0, 0)
        end = DB.XYZ(10, 0, 0)
        mid = DB.XYZ(5, 5, 0)

        arc = DB.Arc.Create(start, end, mid)
        print(f"Arc: radius={arc.Radius:.2f}, length={arc.Length:.2f}")
        print(arc)  # Visualize in 3D view

    except Exception as e:
        print(f"ERROR: {e}")


def main():
    print("=== Curve Visualization Test ===")
    print()

    uidoc: UI.UIDocument = __revit__.ActiveUIDocument  # type: ignore  # noqa: F821

    # Uncomment the test you want to run:
    # test_single_edge(uidoc)
    # test_multiple_edges(uidoc)
    # test_wall_curves(uidoc)
    test_generated_lines()
    # test_arc()


if __name__ == "__main__":
    main()
