# /// script
# dependencies = [
#     "python-fcl==0.7.0.10",
#     "numpy==2.4.2",
#     "trimesh==4.11.2",
#     "manifold3d==3.3.2",
#     "networkx==3.6.1",
# ]
# ///

"""
FCL Collision Detection with Revit Mesh Visualization
------------------------------------------------------
Workflow:
  1. User picks 2 Revit elements.
  2. Solid geometry is extracted and triangulated into numpy meshes.
  3. python-fcl (BVH mesh) checks for collision — fast early-exit.
  4. If collision found, trimesh.boolean.intersection (manifold3d)
     computes the exact intersection mesh.
  5. The intersection mesh is placed in the model as a DirectShape
     via TessellatedShapeBuilder so it is immediately visible.
"""

import fcl
import numpy as np
import trimesh
import trimesh.boolean
from Autodesk.Revit import DB, UI
from Autodesk.Revit.DB import (
    XYZ,
    DirectShape,
    ElementId,
    GeometryInstance,
    Options,
    Solid,
    TessellatedFace,
    TessellatedShapeBuilder,
    TessellatedShapeBuilderFallback,
    TessellatedShapeBuilderTarget,
    Transaction,
)
from Autodesk.Revit.UI.Selection import ObjectType
from System.Collections.Generic import List

# ---------------------------------------------------------------------------
# Helper: extract first valid solid from a Revit element
# ---------------------------------------------------------------------------
def extract_solids(element: DB.Element) -> list[Solid]:
    """Return all non-empty Solids from *element* (traverses GeometryInstances)."""
    opts = Options()
    geom = element.get_Geometry(opts)
    if geom is None:
        return []

    result: list[Solid] = []

    def _collect(geo_objects):
        for obj in geo_objects:
            if isinstance(obj, Solid) and obj.Volume > 0:
                result.append(obj)
            elif isinstance(obj, GeometryInstance):
                _collect(obj.GetInstanceGeometry())

    _collect(geom)
    return result


# ---------------------------------------------------------------------------
# Helper: triangulate a Revit Solid into numpy vertex/triangle arrays
# ---------------------------------------------------------------------------
_LOD = 0.5  # level-of-detail for Face.Triangulate (0 = coarse, 1 = fine)


def solid_to_mesh_arrays(solid: Solid) -> tuple[np.ndarray, np.ndarray]:
    """
    Triangulate *solid* and return (vertices, triangles).

    vertices  : float64 (N, 3) array of XYZ coordinates in feet.
    triangles : int32   (M, 3) array of vertex indices.
    """
    verts: list[tuple[float, float, float]] = []
    tris: list[tuple[int, int, int]] = []
    offset = 0

    for face in solid.Faces:
        mesh : DB.Mesh = face.Triangulate(_LOD)
        if mesh is None or mesh.NumTriangles == 0:
            continue
        # vertices
        for v in mesh.Vertices:
            verts.append((v.X, v.Y, v.Z))
        # triangles (the index is local to each face mesh → add offset)
        for ti in range(mesh.NumTriangles):
            tri = mesh.get_Triangle(ti)
            tris.append((
                offset + tri.get_Index(0),
                offset + tri.get_Index(1),
                offset + tri.get_Index(2),
            ))
        offset += mesh.Vertices.Count

    return np.array(verts, dtype=np.float64), np.array(tris, dtype=np.int32)


# ---------------------------------------------------------------------------
# Helper: build a python-fcl BVH collision object from a Revit Solid
# ---------------------------------------------------------------------------
def solid_to_fcl_object(solid: Solid) -> fcl.CollisionObject:
    """Convert *solid* to an FCL BVH mesh collision object."""
    verts, tris = solid_to_mesh_arrays(solid)
    bvh = fcl.BVHModel()
    bvh.beginModel(len(tris), len(verts))
    bvh.addSubModel(verts, tris)
    bvh.endModel()
    return fcl.CollisionObject(bvh, fcl.Transform())


# ---------------------------------------------------------------------------
# Helper: check FCL collision between two solids
# ---------------------------------------------------------------------------
def fcl_check_collision(solid1: Solid, solid2: Solid) -> tuple[bool, int]:
    """
    Returns (collision_detected, num_contacts).
    Fast BVH early-exit — does NOT compute intersection geometry.
    """
    obj1 = solid_to_fcl_object(solid1)
    obj2 = solid_to_fcl_object(solid2)

    request = fcl.CollisionRequest(num_max_contacts=64, enable_contact=True)
    result = fcl.CollisionResult()
    n = fcl.collide(obj1, obj2, request, result)
    return n > 0, n


# ---------------------------------------------------------------------------
# Helper: convert Revit Solid → trimesh.Trimesh
# ---------------------------------------------------------------------------
def solid_to_trimesh(solid: Solid) -> trimesh.Trimesh:
    """Triangulate a Revit Solid and return a repaired, watertight trimesh.Trimesh."""
    verts, tris = solid_to_mesh_arrays(solid)
    mesh = trimesh.Trimesh(vertices=verts, faces=tris, process=True)
    # Repair: remove degenerate/duplicate faces, fix winding order, fill holes
    trimesh.repair.fix_winding(mesh)
    trimesh.repair.fix_normals(mesh)
    trimesh.repair.fill_holes(mesh)
    return mesh


# ---------------------------------------------------------------------------
# Helper: compute intersection mesh via trimesh boolean (manifold3d)
# ---------------------------------------------------------------------------
def trimesh_intersection_mesh(
    solid1: Solid, solid2: Solid
) -> trimesh.Trimesh | None:
    """
    Returns the intersection trimesh.Trimesh of solid1 ∩ solid2,
    or None if the intersection has zero volume.
    Uses the manifold3d boolean engine (fast, exact).
    """
    mesh1 = solid_to_trimesh(solid1)
    mesh2 = solid_to_trimesh(solid2)
    print(f"  mesh1: is_watertight={mesh1.is_watertight}, faces={len(mesh1.faces)}")
    print(f"  mesh2: is_watertight={mesh2.is_watertight}, faces={len(mesh2.faces)}")
    try:
        result = trimesh.boolean.intersection(
            [mesh1, mesh2], engine="manifold", check_volume=False
        )
        if result is not None and result.volume > 1e-12:
            return result
        print(f"  [INFO] Boolean succeeded but result volume={getattr(result, 'volume', 'N/A')}")
    except Exception:
        import traceback
        print("  [WARNING] trimesh boolean intersection failed:")
        print(traceback.format_exc())
    return None


# ---------------------------------------------------------------------------
# Helper: visualize a trimesh.Trimesh as a DirectShape (TessellatedShapeBuilder)
# ---------------------------------------------------------------------------
def visualize_mesh_as_directshape(
    uiapp: UI.UIApplication,
    mesh: trimesh.Trimesh,
    name: str = "FCL Intersection"
) -> DirectShape | None:
    """
    Create a DirectShape from *mesh* using TessellatedShapeBuilder.
    Must be called inside an active Transaction.
    """
    doc = uiapp.ActiveUIDocument.Document
    category_id = ElementId(DB.BuiltInCategory.OST_GenericModel)

    builder = TessellatedShapeBuilder()
    builder.Target = TessellatedShapeBuilderTarget.AnyGeometry
    builder.Fallback = TessellatedShapeBuilderFallback.Mesh
    builder.OpenConnectedFaceSet(False)

    verts = mesh.vertices
    for tri in mesh.faces:
        pts = [
            XYZ(float(verts[tri[0]][0]), float(verts[tri[0]][1]), float(verts[tri[0]][2])),
            XYZ(float(verts[tri[1]][0]), float(verts[tri[1]][1]), float(verts[tri[1]][2])),
            XYZ(float(verts[tri[2]][0]), float(verts[tri[2]][1]), float(verts[tri[2]][2])),
        ]
        net_list = List[XYZ]()
        for pt in pts:
            net_list.Add(pt)
        face = TessellatedFace(net_list, ElementId.InvalidElementId)
        if builder.DoesFaceHaveEnoughLoopsAndVertices(face):
            builder.AddFace(face)

    builder.CloseConnectedFaceSet()
    builder.Build()
    build_result = builder.GetBuildResult()

    ds = DirectShape.CreateElement(doc, category_id)
    ds.SetName(name)
    ds.SetShape(build_result.GetGeometricalObjects())
    return ds


# ---------------------------------------------------------------------------
# Main workflow
# ---------------------------------------------------------------------------
def check_collision_and_visualize(uiapp: UI.UIApplication):
    """
    Pick 2 elements → FCL collision check → visualize the intersection Solid
    as a DirectShape in the Revit model.
    """
    print("=" * 60)
    print("FCL Collision Detection + Solid Visualization")
    print("=" * 60)

    uidoc = uiapp.ActiveUIDocument
    doc = uidoc.Document

    # ── 1. Pick element A ──────────────────────────────────────────────────
    try:
        print("\n[Step 1] Pick FIRST element...")
        ref1 = uidoc.Selection.PickObject(ObjectType.Element, "Pick the FIRST element")
        elem1 = doc.GetElement(ref1)
        print(f"  → Element A: '{elem1.Name}'  ID={elem1.Id.IntegerValue}")
    except Exception as exc:
        print(f"  Selection cancelled or failed: {exc}")
        return

    # ── 2. Pick element B ──────────────────────────────────────────────────
    try:
        print("\n[Step 2] Pick SECOND element...")
        ref2 = uidoc.Selection.PickObject(ObjectType.Element, "Pick the SECOND element")
        elem2 = doc.GetElement(ref2)
        print(f"  → Element B: '{elem2.Name}'  ID={elem2.Id.IntegerValue}")
    except Exception as exc:
        print(f"  Selection cancelled or failed: {exc}")
        return

    # ── 3. Extract solids ──────────────────────────────────────────────────
    print("\n[Step 3] Extracting solid geometry...")
    solids1 = extract_solids(elem1)
    solids2 = extract_solids(elem2)

    if not solids1:
        print("  ERROR: Element A has no solid geometry.")
        return
    if not solids2:
        print("  ERROR: Element B has no solid geometry.")
        return

    # Use the largest solid from each element (by volume)
    solid1 = max(solids1, key=lambda s: s.Volume)
    solid2 = max(solids2, key=lambda s: s.Volume)
    print(f"  Solid A: Volume={solid1.Volume:.4f} ft³")
    print(f"  Solid B: Volume={solid2.Volume:.4f} ft³")

    # ── 4. FCL collision check ─────────────────────────────────────────────
    print("\n[Step 4] Running FCL collision check...")
    try:
        colliding, n_contacts = fcl_check_collision(solid1, solid2)
    except Exception as exc:
        print(f"  ERROR during FCL check: {exc}")
        return

    if not colliding:
        print("  ✗ No collision detected by FCL. Elements do not intersect.")
        return

    print(f"  ✓ Collision DETECTED! Contact points reported by FCL: {n_contacts}")

    # ── 5. Compute intersection mesh (trimesh boolean) ─────────────────────
    print("\n[Step 5] Computing intersection mesh (trimesh boolean / manifold3d)...")
    inter_mesh = trimesh_intersection_mesh(solid1, solid2)

    if inter_mesh is None:
        print("  WARNING: trimesh boolean returned no mesh.")
        print("  Elements may touch at a face/edge only (zero-volume intersection).")
        return

    print(f"  ✓ Intersection mesh: {len(inter_mesh.faces)} triangles, "
          f"volume≈{inter_mesh.volume:.6f} ft³")

    # ── 6. Create DirectShape to visualize ────────────────────────────────
    print("\n[Step 6] Creating DirectShape to visualize intersection mesh...")
    tx = Transaction(doc, "FCL Intersection Visualization")
    tx.Start()
    try:
        ds = visualize_mesh_as_directshape(uiapp, inter_mesh, "FCL Intersection")
        tx.Commit()
        print(f"  ✓ DirectShape created: ID={ds.Id.IntegerValue}")
    except Exception as exc:
        tx.RollBack()
        print(f"  ERROR creating DirectShape: {exc}")
        return

    print("\n" + "=" * 60)
    print("Done! The intersection mesh is highlighted in the model.")
    print("(Look for a Generic-Model named 'FCL Intersection')")
    print("=" * 60)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    check_collision_and_visualize(__revit__) # type: ignore
