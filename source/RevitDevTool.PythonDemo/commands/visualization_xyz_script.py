# /// script
# dependencies = []
# ///

"""
Test XYZ Point Visualization
Demonstrates geometry visualization in 3D view.
Similar to RevitDevTool.DotnetDemo/XYZVisualization.cs
"""

from Autodesk.Revit.DB import XYZ
from Autodesk.Revit.UI.Selection import ObjectType

uiapp = __revit__  # type: ignore  # noqa: F821
uidoc = uiapp.ActiveUIDocument
doc = uidoc.Document


def test_single_point():
    """Pick a point and visualize it"""
    try:
        print("Select a point on an element...")
        ref = uidoc.Selection.PickObject(ObjectType.PointOnElement)
        point = ref.GlobalPoint

        print(f"Selected point: X={point.X:.2f}, Y={point.Y:.2f}, Z={point.Z:.2f}")
        print(point)  # Visualize in 3D view

    except Exception as e:
        print(f"ERROR: {e}")


def test_multiple_points():
    """Pick multiple points and visualize them"""
    try:
        print("Select multiple points (ESC when done)...")
        refs = uidoc.Selection.PickObjects(ObjectType.PointOnElement)

        if len(refs) == 0:
            print("WARNING: No points selected")
            return

        print(f"Selected {len(refs)} points")

        # Visualize each point
        for i, ref in enumerate(refs):
            point = ref.GlobalPoint
            print(f"Point {i + 1}: X={point.X:.2f}, Y={point.Y:.2f}, Z={point.Z:.2f}")
            print(point)

    except Exception as e:
        print(f"ERROR: {e}")


def test_generated_points():
    """Generate and visualize a grid of points"""
    try:
        print("Generating 5x5 point grid...")

        points = []
        for x in range(5):
            for y in range(5):
                point = XYZ(x * 10.0, y * 10.0, 0.0)
                points.append(point)
                print(point)  # Visualize each point

        print(f"Generated {len(points)} points")

    except Exception as e:
        print(f"ERROR: {e}")


if __name__ == "__main__":
    print("=== XYZ Visualization Test ===")
    print()

    # Uncomment the test you want to run:
    test_single_point()
    # test_multiple_points()
    # test_generated_points()
