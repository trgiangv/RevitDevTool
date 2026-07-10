# /// script
# dependencies = [
#     "shapely==2.1.2",
#     "numpy==2.4.2",
# ]
# ///

import numpy as np
from itertools import combinations
from math import atan2

from System import Guid
from System.Collections.Generic import List
from shapely import affinity
from shapely.geometry import Polygon, box
from shapely.ops import unary_union
from shapely.prepared import prep
from Autodesk.Revit import DB, UI
from Autodesk.Revit.UI.Selection import ISelectionFilter, ObjectType
from System.Diagnostics import Trace


class RoomSpaceSelectionFilter(ISelectionFilter):
    __namespace__ = str(Guid.NewGuid())  # pythonnet3: unique namespace for interface impl

    def AllowElement(self, element):
        return isinstance(element, (DB.Architecture.Room, DB.Mechanical.Space))

    def AllowReference(self, reference, position):
        return False


def _curve_to_xy_points(curve):
    return [(p.X, p.Y) for p in curve.Tessellate()]


def _ring_from_boundary_loop(loop):
    pts = []
    for seg in loop:
        seg_pts = _curve_to_xy_points(seg.GetCurve())
        if not seg_pts:
            continue
        if pts:
            pts.extend(seg_pts[1:])
        else:
            pts.extend(seg_pts)

    if len(pts) < 3:
        return None
    if pts[0] != pts[-1]:
        pts.append(pts[0])
    return pts


def _spatial_element_to_polygon(spatial_element):
    options = DB.SpatialElementBoundaryOptions()
    loops = spatial_element.GetBoundarySegments(options)
    if loops is None:
        return None

    ring_data = []
    for loop in loops:
        ring = _ring_from_boundary_loop(loop)
        if ring is None:
            continue

        poly = Polygon(ring).buffer(0)
        if poly.is_empty or poly.area < 1e-8:
            continue
        ring_data.append((ring, poly.area))

    if not ring_data:
        return None

    ring_data.sort(key=lambda x: x[1], reverse=True)
    shell = ring_data[0][0]
    holes = [ring for ring, _ in ring_data[1:]]
    return Polygon(shell, holes).buffer(0)


def _get_alignment_angle(poly):
    mrr = poly.minimum_rotated_rectangle
    if mrr.is_empty:
        return 0.0

    coords = list(mrr.exterior.coords)
    if len(coords) < 3:
        return 0.0

    edges = []
    for i in range(4):
        x1, y1 = coords[i]
        x2, y2 = coords[i + 1]
        dx = x2 - x1
        dy = y2 - y1
        edges.append((dx * dx + dy * dy, dx, dy))

    _, dx, dy = max(edges, key=lambda e: e[0])
    return np.degrees(atan2(dy, dx))


def _polygon_to_lines(poly, z):
    lines = List[DB.Line]()

    coords = list(poly.exterior.coords)
    for i in range(len(coords) - 1):
        x1, y1 = coords[i]
        x2, y2 = coords[i + 1]
        p1 = DB.XYZ(x1, y1, z)
        p2 = DB.XYZ(x2, y2, z)
        lines.Add(DB.Line.CreateBound(p1, p2))

    return lines


def get_largest_internal_rect(poly):
    rings = [poly.exterior] + list(poly.interiors)
    coords = np.vstack([np.asarray(r.coords)[:, :2] for r in rings])
    xs, ys = map(np.unique, coords.T)
    pp = prep(poly)

    rects = (
        box(x1, y1, x2, y2)
        for x1, x2 in combinations(xs, 2)
        for y1, y2 in combinations(ys, 2)
    )
    return max((r for r in rects if pp.contains(r)), key=lambda r: r.area, default=None)


def get_polys(geo):
    if geo.is_empty:
        return []
    if geo.geom_type == 'Polygon':
        return [geo]
    return [g for g in getattr(geo, 'geoms', []) if g.geom_type == 'Polygon']


def subtractive_decomposition(poly, max_rooms=15):
    remaining, rectangles = poly, []

    for _ in range(max_rooms):
        if remaining.is_empty or remaining.area < .01:
            break

        pieces = sorted(get_polys(remaining), key=lambda p: p.area, reverse=True)
        if not pieces:
            break

        target, others = pieces[0], pieces[1:]
        rect = get_largest_internal_rect(target)
        ok = rect is not None and rect.area >= .01

        rectangles.append(rect if ok else target)
        rest = [target.difference(rect)] if ok else []
        remaining = unary_union(rest + others) if rest or others else Polygon()

    return rectangles


def main():
    uiapp: UI.UIApplication = __revit__  # type: ignore  # noqa: F821
    uidoc = uiapp.ActiveUIDocument
    doc = uidoc.Document

    try:
        ref = uidoc.Selection.PickObject(
            ObjectType.Element,
            RoomSpaceSelectionFilter(),
            "Select a Room or Space",
        )
    except Exception as exc:
        print("Selection cancelled or failed: {}".format(exc))
        return

    spatial = doc.GetElement(ref)
    poly = _spatial_element_to_polygon(spatial)
    if poly is None or poly.is_empty or poly.area < 0.01:
        print("Selected element has no valid boundary polygon.")
        return

    angle = _get_alignment_angle(poly)
    aligned = affinity.rotate(poly, -angle, origin=(0, 0))

    final_decomposition = subtractive_decomposition(aligned)
    final_decomposition = [affinity.rotate(x, angle, origin=(0, 0)) for x in final_decomposition]
    final_decomposition = [x for x in final_decomposition if x.geom_type == "Polygon" and len(x.exterior.coords) <= 5]

    all_lines = List[DB.Line]()
    zvalue = spatial.Location.Point.Z if hasattr(spatial, "Location") and spatial.Location else 0.0

    for region in final_decomposition:
        region_lines = _polygon_to_lines(region, zvalue)
        Trace.Write(region_lines)
        for line in region_lines:
            all_lines.Add(line)

    print("Subdivided into {} regions and exported {} lines via Trace.Write.".format(len(final_decomposition), all_lines.Count))


if __name__ == "__main__":
    main()
