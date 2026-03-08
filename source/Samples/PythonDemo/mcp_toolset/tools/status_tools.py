"""Status and model information tools."""

from typing import Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations

from services.model_service import ModelService
from services.status_service import StatusService


def register_status_tools(mcp: FastMCP, status_service: StatusService, model_service: ModelService) -> None:
    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Revit Status",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=False,
        ),
        structured_output=True,
    )
    async def get_revit_status(ctx: Context | None = None) -> dict[str, Any]:
        """Report whether Revit and an active document are currently available."""
        _ = ctx
        return status_service.get_status()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Revit Model Info",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def get_revit_model_info(ctx: Context | None = None) -> dict[str, Any]:
        """Return high-level information about the active Revit model."""
        _ = ctx
        return model_service.get_model_info()
