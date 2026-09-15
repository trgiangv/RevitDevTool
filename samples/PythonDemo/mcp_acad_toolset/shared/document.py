"""AutoCAD document, transaction, and handle helpers."""

from collections.abc import Iterator
from contextlib import contextmanager
from typing import Any

from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import (
    BlockTableRecord,
    Entity,
    Handle,
    OpenMode,
)

from shared.responses import ToolError


def require_doc() -> Any:
    doc = Application.DocumentManager.MdiActiveDocument
    if doc is None:
        raise ToolError("No active AutoCAD document")
    return doc


def host_version() -> str | None:
    try:
        return str(Application.Version)
    except Exception:
        return None


def handle_str(obj: Any) -> str:
    try:
        return format(int(obj.Handle.Value), "X")
    except Exception:
        return str(obj.Handle)


def parse_handle(text: str) -> Handle:
    raw = (text or "").strip()
    if raw.lower().startswith("0x"):
        raw = raw[2:]
    try:
        return Handle(int(raw, 16))
    except ValueError as exc:
        raise ToolError("Invalid handle '{}'".format(text)) from exc


def object_id_from_handle(db: Any, handle_text: str) -> Any:
    try:
        oid = db.GetObjectId(False, parse_handle(handle_text), 0)
    except Exception as exc:
        raise ToolError("Handle '{}' not found".format(handle_text)) from exc
    if oid is None or oid.IsNull:
        raise ToolError("Handle '{}' not found".format(handle_text))
    return oid


def entity_extents(entity: Entity) -> list[float] | None:
    try:
        extents = entity.GeometricExtents
        minimum = extents.MinPoint
        maximum = extents.MaxPoint
        return [minimum.X, minimum.Y, minimum.Z, maximum.X, maximum.Y, maximum.Z]
    except Exception:
        return None


@contextmanager
def locked_document(doc: Any | None = None) -> Iterator[Any]:
    current = doc or require_doc()
    with current.LockDocument():
        yield current


@contextmanager
def acad_transaction(
    doc: Any | None = None,
    *,
    write_model_space: bool = False,
) -> Iterator[tuple[Any, Any, Any | None]]:
    with locked_document(doc) as current:
        db = current.Database
        transaction = db.TransactionManager.StartTransaction()
        model_space = None
        try:
            if write_model_space:
                block_table = transaction.GetObject(db.BlockTableId, OpenMode.ForRead)
                model_space = transaction.GetObject(
                    block_table[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite,
                )
            yield current, transaction, model_space
            transaction.Commit()
        except Exception:
            transaction.Abort()
            raise
        finally:
            transaction.Dispose()
