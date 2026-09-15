"""Read-only drawing intelligence: status, layers, entities, selection."""

import math
from typing import Any

from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import (
    Arc,
    BlockTableRecord,
    Circle,
    Entity,
    LayerTableRecord,
    Line,
    OpenMode,
    Polyline,
)
from Autodesk.AutoCAD.EditorInput import PromptStatus

from dto.query import (
    EntitySummary,
    FindEntitiesResult,
    GetSelectionResult,
    LayerItem,
    ListLayersResult,
    StatusResult,
)
from shared.document import (
    acad_transaction,
    entity_extents,
    handle_str,
    host_version,
    locked_document,
    object_id_from_handle,
    require_doc,
)
from shared.responses import ToolError


class QueryService:
    def get_status(self) -> StatusResult:
        try:
            doc = require_doc()
        except Exception:
            return StatusResult(healthy=False)

        with acad_transaction(doc) as (current, transaction, _):
            db = current.Database
            entity_count = _model_space_count(transaction, db)
            layer_count = _layer_count(transaction, db)
            selection_count = _implied_selection_count(current)

        return StatusResult(
            healthy=True,
            document_name=current.Name or "",
            file_path=db.Filename or None,
            version=host_version(),
            document_count=int(Application.DocumentManager.Count),
            entity_count=entity_count,
            layer_count=layer_count,
            selection_count=selection_count,
        )

    def list_layers(self) -> ListLayersResult:
        doc = require_doc()
        layers: list[LayerItem] = []
        with acad_transaction(doc) as (_, transaction, _):
            layer_table = transaction.GetObject(
                doc.Database.LayerTableId, OpenMode.ForRead
            )
            for layer_id in layer_table:
                record = transaction.GetObject(layer_id, OpenMode.ForRead)
                if not isinstance(record, LayerTableRecord):
                    continue
                layers.append(_layer_item(record))
        layers.sort(key=lambda item: item.name.lower())
        return ListLayersResult(count=len(layers), layers=layers)

    def find_entities(
        self,
        *,
        type_name: str | None = None,
        layer: str | None = None,
        max_results: int = 200,
        offset: int = 0,
    ) -> FindEntitiesResult:
        max_results = max(max_results, 1)
        offset = max(offset, 0)
        matched: list[EntitySummary] = []
        total = 0
        with acad_transaction() as (current, transaction, _):
            for entity in _iter_model_entities(transaction, current.Database):
                if not _matches(entity, type_name, layer):
                    continue
                if total >= offset and len(matched) < max_results:
                    matched.append(_entity_summary(entity))
                total += 1
        return FindEntitiesResult(
            count=total,
            truncated=offset + len(matched) < total,
            entities=matched,
        )

    def get_selection(self) -> GetSelectionResult:
        doc = require_doc()
        entities: list[EntitySummary] = []
        with locked_document(doc) as current:
            result = current.Editor.SelectImplied()
            if result.Status != PromptStatus.OK or result.Value is None:
                return GetSelectionResult(count=0, entities=[])
            transaction = current.Database.TransactionManager.StartTransaction()
            try:
                for selected in result.Value:
                    if selected is None:
                        continue
                    obj = transaction.GetObject(selected.ObjectId, OpenMode.ForRead)
                    if isinstance(obj, Entity):
                        entities.append(_entity_summary(obj))
                transaction.Commit()
            except Exception:
                transaction.Abort()
                raise
            finally:
                transaction.Dispose()
        return GetSelectionResult(count=len(entities), entities=entities)

    def get_entity_by_handle(self, handle: str) -> EntitySummary:
        with acad_transaction() as (current, transaction, _):
            oid = object_id_from_handle(current.Database, handle)
            obj = transaction.GetObject(oid, OpenMode.ForRead)
            if not isinstance(obj, Entity):
                raise ToolError("Handle '{}' is not an entity".format(handle))
            return _entity_summary(obj)


def _model_space_count(transaction: Any, db: Any) -> int:
    return sum(1 for _ in _iter_model_entities(transaction, db))


def _layer_count(transaction: Any, db: Any) -> int:
    layer_table = transaction.GetObject(db.LayerTableId, OpenMode.ForRead)
    return sum(1 for _ in layer_table)


def _implied_selection_count(doc: Any) -> int:
    result = doc.Editor.SelectImplied()
    if result.Status != PromptStatus.OK or result.Value is None:
        return 0
    return int(result.Value.Count)


def _iter_model_entities(transaction: Any, db: Any) -> Any:
    block_table = transaction.GetObject(db.BlockTableId, OpenMode.ForRead)
    model_space = transaction.GetObject(
        block_table[BlockTableRecord.ModelSpace],
        OpenMode.ForRead,
    )
    for object_id in model_space:
        obj = transaction.GetObject(object_id, OpenMode.ForRead)
        if isinstance(obj, Entity):
            yield obj


def _matches(entity: Entity, type_name: str | None, layer: str | None) -> bool:
    if type_name and entity.GetType().Name.lower() != type_name.strip().lower():
        return False
    if layer and (entity.Layer or "").lower() != layer.strip().lower():
        return False
    return True


def _layer_item(record: LayerTableRecord) -> LayerItem:
    color = record.Color
    return LayerItem(
        name=record.Name or "",
        is_off=bool(record.IsOff),
        is_frozen=bool(record.IsFrozen),
        is_locked=bool(record.IsLocked),
        color=str(color),
        color_index=int(color.ColorIndex) if color is not None else None,
    )


def _entity_summary(entity: Entity) -> EntitySummary:
    return EntitySummary(
        handle=handle_str(entity),
        type=entity.GetType().Name,
        layer=entity.Layer or "",
        color=str(entity.Color) if entity.Color is not None else None,
        length=_entity_length(entity),
        area=_entity_area(entity),
        bbox=entity_extents(entity),
    )


def _entity_length(entity: Entity) -> float | None:
    if isinstance(entity, (Line, Arc, Circle, Polyline)):
        try:
            if isinstance(entity, Circle):
                return float(entity.Circumference)
            return float(entity.Length)
        except Exception:
            return None
    return None


def _entity_area(entity: Entity) -> float | None:
    if isinstance(entity, Circle):
        return float(entity.Radius * entity.Radius * math.pi)
    if isinstance(entity, Polyline) and bool(entity.Closed):
        try:
            return float(entity.Area)
        except Exception:
            return None
    return None
