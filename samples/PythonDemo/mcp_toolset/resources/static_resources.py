"""Static embedded markdown resources."""
from __future__ import annotations

import os

_CONTENT_DIR = os.path.join(os.path.dirname(__file__), "content")


def _load(filename: str) -> str:
    path = os.path.join(_CONTENT_DIR, filename)
    with open(path, encoding="utf-8") as f:
        return f.read()


def register_static_resources(mcp) -> None:
    @mcp.resource("revit://toolset/capabilities")
    async def get_capabilities() -> str:
        """Full tool catalog with usage guide."""
        return _load("capabilities.md")

    @mcp.resource("revit://toolset/patterns/query")
    async def get_query_patterns() -> str:
        """FilterSpec composition examples and performance tips."""
        return _load("patterns-query.md")

    @mcp.resource("revit://toolset/patterns/mep")
    async def get_mep_patterns() -> str:
        """MEP workflow patterns."""
        return _load("patterns-mep.md")

    @mcp.resource("revit://toolset/patterns/documentation")
    async def get_documentation_patterns() -> str:
        """Sheet package workflow patterns."""
        return _load("patterns-documentation.md")

    @mcp.resource("revit://toolset/patterns/export")
    async def get_export_patterns() -> str:
        """Export options and path conventions."""
        return _load("patterns-export.md")

    @mcp.resource("revit://toolset/errors")
    async def get_errors() -> str:
        """Standard error codes and recovery patterns."""
        return _load("errors.md")

    @mcp.resource("revit://toolset/units")
    async def get_units() -> str:
        """Unit conversion reference."""
        return _load("units.md")
