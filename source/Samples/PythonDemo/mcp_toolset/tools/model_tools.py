"""Model hierarchy tools."""

from typing import Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations

from services.model_service import ModelService


def register_model_tools(mcp: FastMCP, model_service: ModelService) -> None:
    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Levels",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_levels(ctx: Context | None = None) -> dict[str, Any]:
        """List levels available in the active Revit model."""
        _ = ctx
        return model_service.list_levels()
