# /// script
# dependencies = [
#   "tabulate>=0.9",
# ]
# ///
"""Civil 3D test suite — shared fixtures for AutoCAD/Civil3D API testing.

AutoCAD does not inject a global builtin like Revit's ``__revit__``.
Instead, use the static ``Application`` class from
``Autodesk.AutoCAD.ApplicationServices.Core``.

Civil 3D-specific APIs require ``clr.AddReference('AeccDbMgd')`` and
live under ``Autodesk.Civil.DatabaseServices``.
"""

import pytest


@pytest.fixture(scope="session")
def acad_app():
    """Provide the AutoCAD Application object (static class)."""
    from Autodesk.AutoCAD.ApplicationServices.Core import Application  # noqa: F811

    return Application


@pytest.fixture(scope="session")
def acad_doc(acad_app):
    """Provide the active Document (MdiActiveDocument)."""
    doc = acad_app.DocumentManager.MdiActiveDocument
    if doc is None:
        pytest.skip("No active document in Civil 3D")
    return doc


@pytest.fixture(scope="session")
def acad_db(acad_doc):
    """Provide the active Database."""
    return acad_doc.Database


@pytest.fixture(scope="session")
def acad_editor(acad_doc):
    """Provide the active Editor."""
    return acad_doc.Editor


@pytest.fixture
def acad_transaction(acad_db):
    """Start a transaction and commit on success, abort on failure."""
    tr = acad_db.TransactionManager.StartTransaction()
    try:
        yield tr
        tr.Commit()
    except Exception:
        tr.Abort()
        raise
    finally:
        tr.Dispose()


@pytest.fixture
def acad_auto_rollback(acad_db):
    """Start a transaction and always abort — undo all changes after test."""
    tr = acad_db.TransactionManager.StartTransaction()
    try:
        yield tr
    finally:
        tr.Abort()
        tr.Dispose()
