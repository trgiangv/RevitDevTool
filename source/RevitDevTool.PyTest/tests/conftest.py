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

RVT_PATH = r"F:\Project1.rvt"


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
    """Open Project1.rvt with OpenAndActivateDocument and provide its Document."""
    if not os.path.isfile(RVT_PATH):
        pytest.skip(f"{RVT_PATH} not found on disk")

    target = os.path.normcase(os.path.abspath(RVT_PATH))
    current_uidoc = revit_uiapp.ActiveUIDocument
    current_doc = current_uidoc.Document if current_uidoc else None

    opened_here = False
    if current_doc is None:
        current_uidoc = revit_uiapp.OpenAndActivateDocument(RVT_PATH)
        current_doc = current_uidoc.Document
        opened_here = True
    else:
        current_path = os.path.normcase(os.path.abspath(current_doc.PathName or ""))
        if current_path != target:
            current_uidoc = revit_uiapp.OpenAndActivateDocument(RVT_PATH)
            current_doc = current_uidoc.Document
            opened_here = True

    doc = current_doc
    yield doc

    if not opened_here or doc is None or not doc.IsValidObject or doc.IsLinked:
        return

    active_uidoc = revit_uiapp.ActiveUIDocument
    if active_uidoc is not None and active_uidoc.Document.Equals(doc):
        from RevitDevTool.Utils import UiAppExtension

        UiAppExtension.CloseActiveUiDocument(revit_uiapp, False)
        return

    doc.Close(False)


@pytest.fixture(scope="session")
def humanize_mod():
    """Import humanize (auto-installed via PEP 723) and expose to tests."""
    import humanize
    return humanize
