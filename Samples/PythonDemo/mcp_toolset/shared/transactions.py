"""Transaction helpers for Revit API operations."""
from __future__ import annotations

from typing import Callable, TypeVar

from Autodesk.Revit import DB

T = TypeVar("T")


def run_transaction(doc: DB.Document, name: str, operation: Callable[[], T]) -> T:
    with DB.Transaction(doc, name) as tx:
        tx.Start()
        try:
            result = operation()
            tx.Commit()
            return result
        except Exception:
            tx.RollBack()
            raise
