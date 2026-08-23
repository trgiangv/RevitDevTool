# /// script
# dependencies = [
#     "polars==1.38.1",
#     "numpy==2.4.2",
#     "shapely==2.1.2",
#     "openpyxl==3.1.5",
# ]
# ///

"""AutoCAD model-space analysis with Polars, NumPy, and Shapely.

Run inside AutoCAD / Civil 3D / Plant 3D via AcadDevTool.
Reads LINE / CIRCLE / ARC / LWPOLYLINE from Model Space, then:
  - tabular summary (Polars)
  - length/area stats (NumPy)
  - 2D envelope + union area (Shapely)
"""

from __future__ import annotations

from typing import Any

import numpy as np
import polars as pl
from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import (
    Arc,
    BlockTableRecord,
    Circle,
    Line,
    OpenMode,
    Polyline,
)
from shapely.geometry import LineString, Point, Polygon
from shapely.ops import unary_union


def _active_document():
    doc = Application.DocumentManager.MdiActiveDocument
    if doc is None:
        raise RuntimeError("No active AutoCAD document.")
    return doc


def _entity_row(ent) -> dict[str, Any] | None:
    handle = str(ent.ObjectId.Handle)
    layer = ent.Layer if ent.Layer else ""
    type_name = ent.GetType().Name

    if isinstance(ent, Line):
        start, end = ent.StartPoint, ent.EndPoint
        geom = LineString([(start.X, start.Y), (end.X, end.Y)])
        return {
            "Handle": handle,
            "Type": type_name,
            "Layer": layer,
            "Length": float(ent.Length),
            "Area": 0.0,
            "Geometry": geom,
        }

    if isinstance(ent, Circle):
        center = ent.Center
        geom = Point(center.X, center.Y).buffer(float(ent.Radius))
        return {
            "Handle": handle,
            "Type": type_name,
            "Layer": layer,
            "Length": float(ent.Circumference),
            "Area": float(ent.Radius * ent.Radius * np.pi),
            "Geometry": geom,
        }

    if isinstance(ent, Arc):
        start, end = ent.StartPoint, ent.EndPoint
        geom = LineString([(start.X, start.Y), (end.X, end.Y)])
        return {
            "Handle": handle,
            "Type": type_name,
            "Layer": layer,
            "Length": float(ent.Length),
            "Area": 0.0,
            "Geometry": geom,
        }

    if isinstance(ent, Polyline):
        coords = [(ent.GetPoint2dAt(i).X, ent.GetPoint2dAt(i).Y) for i in range(ent.NumberOfVertices)]
        if len(coords) < 2:
            return None
        closed = bool(ent.Closed) and len(coords) >= 3
        if closed and coords[0] != coords[-1]:
            coords.append(coords[0])
        geom = Polygon(coords) if closed else LineString(coords)
        if not geom.is_valid:
            geom = geom.buffer(0)
        return {
            "Handle": handle,
            "Type": type_name,
            "Layer": layer,
            "Length": float(ent.Length),
            "Area": float(ent.Area) if closed else 0.0,
            "Geometry": geom,
        }

    return None


def collect_model_space(doc) -> list[dict[str, Any]]:
    db = doc.Database
    rows: list[dict[str, Any]] = []
    tr = db.TransactionManager.StartTransaction()
    try:
        bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
        model_space = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
        for oid in model_space:
            ent = tr.GetObject(oid, OpenMode.ForRead)
            row = _entity_row(ent)
            if row is not None:
                rows.append(row)
        tr.Commit()
    except Exception:
        tr.Abort()
        raise
    finally:
        tr.Dispose()
    return rows


def analyze(rows: list[dict[str, Any]]) -> None:
    geometries = [row.pop("Geometry") for row in rows]
    df = pl.DataFrame(rows)

    print("=== Entity table ===")
    print(df)

    print()
    print("=== By type ===")
    print(
        df.group_by("Type").agg(
            [
                pl.len().alias("Count"),
                pl.col("Length").sum().alias("TotalLength"),
                pl.col("Area").sum().alias("TotalArea"),
            ]
        )
    )

    lengths = df["Length"].to_numpy()
    print()
    print("=== NumPy length stats ===")
    print(f"count={lengths.size}  mean={lengths.mean():.3f}  std={lengths.std():.3f}  max={lengths.max():.3f}")

    merged = unary_union(geometries)
    envelope = merged.envelope
    print()
    print("=== Shapely envelope ===")
    print(f"union area={merged.area:.3f}  envelope bounds={envelope.bounds}")
    print(f"envelope WKT: {envelope.wkt}")


def main() -> None:
    print("=== AutoCAD entity analysis ===")
    doc = _active_document()
    print(f"Document: {doc.Name}")

    loc = doc.LockDocument()
    try:
        rows = collect_model_space(doc)
    finally:
        loc.Dispose()

    if not rows:
        print("No LINE / CIRCLE / ARC / LWPOLYLINE found in Model Space.")
        return

    print(f"Collected {len(rows)} entities")
    analyze(rows)
    print()
    print("Analysis complete.")


if __name__ == "__main__":
    main()
