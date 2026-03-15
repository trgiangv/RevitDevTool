"""Transaction helpers for Revit API operations."""
from __future__ import annotations

from typing import Callable, TypeVar

from Autodesk.Revit import DB

T = TypeVar("T")


def run_transaction(doc: DB.Document, name: str, operation: Callable[[], T]) -> T:
    tx = DB.Transaction(doc, name)
    tx.Start()
    try:
        result = operation()
        tx.Commit()
        return result
    except Exception:
        if tx.HasStarted() and not tx.HasEnded():
            tx.RollBack()
        raise
