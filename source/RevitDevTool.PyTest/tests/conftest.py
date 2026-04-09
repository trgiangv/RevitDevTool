"""Sample conftest.py — user-defined fixtures for Revit testing.

The plugin only provides the connection to Revit and remote execution;
everything else is user-controlled.
"""

import pytest


@pytest.fixture(scope="session")
def revit_app():
    """Provide the Revit Application object for the entire test session."""
    app = __revit__.Application  # noqa: F821
    yield app


@pytest.fixture(scope="session")
def revit_doc():
    """Provide the active Document. Assumes a document is already open in Revit."""
    doc = __revit__.ActiveUIDocument.Document  # noqa: F821
    yield doc
