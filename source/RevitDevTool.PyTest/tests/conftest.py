# /// script
# dependencies = [
#   "humanize>=4.0",
#   "tabulate>=0.9",
#   "numpy>=2.0",
#   "polars>=1.0",
#   "pydantic>=2.0",
#   "shapely>=2.0",
#   "networkx>=3.0",
#   "openpyxl>=3.1",
# ]
# ///
"""Test suite conftest — declares shared fixtures and PEP 723 dependencies.

PEP 723 dependencies above are auto-resolved by RevitDevTool's
PytestDependencyService (same mechanism as MCP/Execution scripts).
All test files in this suite share these pre-installed packages.
"""

import os

import pytest

DEFAULT_RVT_PATH = r"C:\Users\Giang.VuTruong\workspace\models\test_2025.rvt"
RVT_PATH = os.environ.get("REVIT_TEST_MODEL_PATH", DEFAULT_RVT_PATH)


def _normalize_path(path: str) -> str:
    return os.path.normcase(os.path.abspath(path))


@pytest.fixture(scope="session")
def revit_uiapp():
    """Provide the UIApplication object for the entire test session."""
    return __revit__  # noqa: F821


@pytest.fixture(scope="session")
def revit_app(revit_uiapp):
    """Provide the Revit Application object for the entire test session."""
    return revit_uiapp.Application


@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    """Return the target project document, opening it only if not already open."""
    if not os.path.isfile(RVT_PATH):
        pytest.skip(f"{RVT_PATH} not found on disk")

    target = _normalize_path(RVT_PATH)
    current_uidoc = revit_uiapp.ActiveUIDocument
    current_doc = current_uidoc.Document if current_uidoc else None

    if (
        current_doc is not None
        and _normalize_path(current_doc.PathName or "") == target
    ):
        return current_doc

    current_uidoc = revit_uiapp.OpenAndActivateDocument(RVT_PATH)
    return current_uidoc.Document

@pytest.fixture(scope="session")
def source_schedule(revit_doc):
    """Return the source schedule element for testing."""
    from tests.schedule.constants import SOURCE_SCHEDULE_ID
    from Autodesk.Revit import DB

    return revit_doc.GetElement(DB.ElementId(SOURCE_SCHEDULE_ID))


@pytest.fixture(scope="session")
def target_schedule(revit_doc):
    """Return the target schedule element for testing."""
    from tests.schedule.constants import TARGET_SCHEDULE_ID
    from Autodesk.Revit import DB

    return revit_doc.GetElement(DB.ElementId(TARGET_SCHEDULE_ID))

@pytest.fixture(scope="session")
def humanize_mod():
    """Import humanize (auto-installed via PEP 723) and expose to tests."""
    import humanize

    return humanize


@pytest.fixture
def revit_transaction_service():
    """Lazily import RevitTransactionService only in Revit-hosted tests."""
    from RevitDevTool.Core import RevitTransactionService

    return RevitTransactionService


@pytest.fixture
def revit_auto_rollback(revit_transaction_service):
    """Start undo tracking before a test and always revert after it finishes."""
    revit_transaction_service.StartChanges()
    try:
        yield revit_transaction_service
    finally:
        revit_transaction_service.RevertChanges()