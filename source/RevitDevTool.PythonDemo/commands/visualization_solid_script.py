# /// script
# dependencies = []
# ///

"""
Test Solid and Face Visualization
Demonstrates complex geometry visualization (solids, faces, meshes).
Similar to RevitDevTool.DotnetDemo/SolidVisualization.cs and FaceVisualization.cs
"""

from Autodesk.Revit.DB import BoundingBoxXYZ, Face, FilteredElementCollector, GeometryInstance, Options, Solid
from Autodesk.Revit.UI.Selection import ObjectType

uiapp = __revit__  # type: ignore  # noqa: F821
uidoc = uiapp.ActiveUIDocument
doc = uidoc.Document


def test_element_solid():
    """Pick an element and visualize its solid geometry"""
    try:
        print("Select an element...")
        ref = uidoc.Selection.PickObject(ObjectType.Element, "Select an element")

        elem = doc.GetElement(ref)
        options = Options()
        geom = elem.get_Geometry(options)

        if not geom:
            print("ERROR: No geometry found")
            return

        print(f"Element: {elem.Name} (ID: {elem.Id.IntegerValue})")

        # Extract solids
        solids = []
        for geo_obj in geom:
            if isinstance(geo_obj, Solid) and geo_obj.Volume > 0:
                solids.append(geo_obj)
            elif isinstance(geo_obj, GeometryInstance):
                inst_geom = geo_obj.GetInstanceGeometry()
                for inst_obj in inst_geom:
                    if isinstance(inst_obj, Solid) and inst_obj.Volume > 0:
                        solids.append(inst_obj)

        print(f"Found {len(solids)} solid(s)")

        # Visualize each solid
        for i, solid in enumerate(solids):
            print(f"Solid {i + 1}: Volume={solid.Volume:.2f}, SurfaceArea={solid.SurfaceArea:.2f}")
            print(solid)

    except Exception as e:
        print(f"ERROR: {e}")


def test_element_faces():
    """Pick an element and visualize its faces"""
    try:
        print("Select an element...")
        ref = uidoc.Selection.PickObject(ObjectType.Element, "Select an element")

        elem = doc.GetElement(ref)
        options = Options()
        geom = elem.get_Geometry(options)

        if not geom:
            print("ERROR: No geometry found")
            return

        print(f"Element: {elem.Name} (ID: {elem.Id.IntegerValue})")

        # Extract faces from solids
        faces = []
        for geo_obj in geom:
            if isinstance(geo_obj, Solid) and geo_obj.Volume > 0:
                for face in geo_obj.Faces:
                    faces.append(face)
            elif isinstance(geo_obj, GeometryInstance):
                inst_geom = geo_obj.GetInstanceGeometry()
                for inst_obj in inst_geom:
                    if isinstance(inst_obj, Solid) and inst_obj.Volume > 0:
                        for face in inst_obj.Faces:
                            faces.append(face)

        print(f"Found {len(faces)} face(s)")

        # Visualize each face
        for i, face in enumerate(faces):
            print(f"Face {i + 1}: Area={face.Area:.2f}")
            print(face)

    except Exception as e:
        print(f"ERROR: {e}")


def test_picked_face():
    """Pick a face directly and visualize it"""
    try:
        print("Select a face...")
        ref = uidoc.Selection.PickObject(ObjectType.Face, "Select a face")

        elem = doc.GetElement(ref)
        face = elem.GetGeometryObjectFromReference(ref)

        if face:
            print(f"Selected face: Area={face.Area:.2f}")
            print(face)
        else:
            print("ERROR: Could not get face geometry")

    except Exception as e:
        print(f"ERROR: {e}")


def test_bounding_boxes():
    """Visualize bounding boxes of selected elements"""
    try:
        print("Select elements (ESC when done)...")
        refs = uidoc.Selection.PickObjects(ObjectType.Element, "Select elements")

        if len(refs) == 0:
            print("WARNING: No elements selected")
            return

        print(f"Selected {len(refs)} element(s)")

        for i, ref in enumerate(refs):
            elem = doc.GetElement(ref)
            bbox = elem.get_BoundingBox(None)

            if bbox:
                print(f"Element {i + 1} ({elem.Name}): BBox Min={bbox.Min}, Max={bbox.Max}")
                print(bbox)
            else:
                print(f"Element {i + 1}: No bounding box")

    except Exception as e:
        print(f"ERROR: {e}")


def test_all_element_solids():
    """Visualize solids of all visible elements (warning: may be slow)"""
    try:
        print("WARNING: This will visualize all element solids in the view")
        print("Collecting elements...")

        collector = FilteredElementCollector(doc, uidoc.ActiveView.Id)
        elements = collector.WhereElementIsNotElementType().ToElements()

        print(f"Found {len(elements)} elements")

        count = 0
        options = Options()

        for elem in elements:
            geom = elem.get_Geometry(options)
            if not geom:
                continue

            for geo_obj in geom:
                if isinstance(geo_obj, Solid) and geo_obj.Volume > 0:
                    print(geo_obj)
                    count += 1

                    if count >= 10:  # Limit to first 10 for performance
                        print(f"Limiting to first {count} solids...")
                        return

        print(f"Visualized {count} solid(s)")

    except Exception as e:
        print(f"ERROR: {e}")


if __name__ == "__main__":
    print("=== Solid and Face Visualization Test ===")
    print()

    # Uncomment the test you want to run:
    test_element_solid()
    # test_element_faces()
    # test_picked_face()
    # test_bounding_boxes()
    # test_all_element_solids()
