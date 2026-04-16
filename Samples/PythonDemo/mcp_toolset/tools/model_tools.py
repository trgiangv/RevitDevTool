"""Model hierarchy tools."""
from __future__ import annotations

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations

from dto.elements import LevelsResult
from services.model_service import ModelService


def register_model_tools(mcp: FastMCP) -> None:
    model_service = ModelService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Levels",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_levels() -> LevelsResult:
        """
        List levels available in the active Revit model.

        Workflow:
        - Use to discover level names and elevations before placing elements or filtering by level.
        - Call before place_family to get valid level_name values.
        - Elevations are in project units (typically feet).
        """
        data = model_service.list_levels()
        return LevelsResult.model_validate(data)
