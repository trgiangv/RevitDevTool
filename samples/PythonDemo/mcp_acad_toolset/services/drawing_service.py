"""Model-space drawing mutations: line, circle, highlight, erase."""

import math

from Autodesk.AutoCAD.DatabaseServices import (
    Circle,
    Entity,
    Line,
    ObjectId,
    OpenMode,
)
from Autodesk.AutoCAD.Geometry import Point3d, Vector3d
from System import Array

from dto.drawing import (
    DrawCircleResult,
    DrawLineResult,
    EraseEntitiesResult,
    HighlightEntitiesResult,
)
from shared.document import (
    acad_transaction,
    handle_str,
    object_id_from_handle,
    require_doc,
)
from shared.responses import ToolError


class DrawingService:
    def draw_line(
        self,
        start: list[float],
        end: list[float],
        layer: str | None = None,
    ) -> DrawLineResult:
        start_point = _point3d(start, "start")
        end_point = _point3d(end, "end")
        with acad_transaction(write_model_space=True) as (_, transaction, model_space):
            if model_space is None:
                raise ToolError("Model space is not writable")
            line = Line(start_point, end_point)
            _append_entity(transaction, model_space, line, layer)
            return DrawLineResult(
                handle=handle_str(line),
                layer=line.Layer or "",
                start=[start_point.X, start_point.Y, start_point.Z],
                end=[end_point.X, end_point.Y, end_point.Z],
                length=float(line.Length),
            )

    def draw_circle(
        self,
        center: list[float],
        radius: float,
        layer: str | None = None,
    ) -> DrawCircleResult:
        if radius <= 0:
            raise ToolError("Radius must be positive")
        center_point = _point3d(center, "center")
        with acad_transaction(write_model_space=True) as (_, transaction, model_space):
            if model_space is None:
                raise ToolError("Model space is not writable")
            circle = Circle(center_point, Vector3d.ZAxis, float(radius))
            _append_entity(transaction, model_space, circle, layer)
            return DrawCircleResult(
                handle=handle_str(circle),
                layer=circle.Layer or "",
                center=[center_point.X, center_point.Y, center_point.Z],
                radius=float(circle.Radius),
                area=float(circle.Radius * circle.Radius * math.pi),
            )

    def highlight_entities(self, handles: list[str]) -> HighlightEntitiesResult:
        if not handles:
            raise ToolError("At least one handle is required")
        doc = require_doc()
        found: list[ObjectId] = []
        missing: list[str] = []
        with acad_transaction(doc) as (current, _, _):
            db = current.Database
            for handle in handles:
                try:
                    found.append(object_id_from_handle(db, handle))
                except Exception:
                    missing.append(handle)
        if found:
            doc.Editor.SetImpliedSelection(Array[ObjectId](found))
        return HighlightEntitiesResult(selected_count=len(found), missing=missing)

    def erase_entities(self, handles: list[str]) -> EraseEntitiesResult:
        if not handles:
            raise ToolError("At least one handle is required")
        erased = 0
        missing: list[str] = []
        with acad_transaction() as (current, transaction, _):
            db = current.Database
            for handle in handles:
                try:
                    oid = object_id_from_handle(db, handle)
                    entity = transaction.GetObject(oid, OpenMode.ForWrite)
                    if not isinstance(entity, Entity):
                        missing.append(handle)
                        continue
                    entity.Erase()
                    erased += 1
                except Exception:
                    missing.append(handle)
        return EraseEntitiesResult(erased_count=erased, missing=missing)


def _point3d(values: list[float], name: str) -> Point3d:
    if values is None or len(values) < 2:
        raise ToolError("{} must be [x, y] or [x, y, z]".format(name))
    z = float(values[2]) if len(values) > 2 else 0.0
    return Point3d(float(values[0]), float(values[1]), z)


def _append_entity(
    transaction: object,
    model_space: object,
    entity: Entity,
    layer: str | None,
) -> None:
    model_space.AppendEntity(entity)
    transaction.AddNewlyCreatedDBObject(entity, True)
    if layer:
        try:
            entity.Layer = layer
        except Exception as exc:
            raise ToolError("Layer '{}' not found".format(layer)) from exc
