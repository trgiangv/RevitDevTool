"""Transaction helpers for Revit API operations."""

from collections.abc import Callable
from typing import TypeAlias

from Autodesk.Revit import DB

VoidOperation: TypeAlias = Callable[[], None]


def run_transaction[T](doc: DB.Document, name: str, operation: Callable[[], T]) -> T:
    with DB.Transaction(doc, name) as tx: # noqa
        tx.Start()
        try:
            result = operation()
            tx.Commit()
            return result
        except Exception:
            tx.RollBack()
            raise
