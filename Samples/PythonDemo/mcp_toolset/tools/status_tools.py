"""Status and model information tools."""
from __future__ import annotations

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations

from dto.elements import ModelInfoResult
from dto.status import StatusResult
from services.model_service import ModelService
from services.status_service import StatusService


def register_status_tools(mcp: FastMCP) -> None:
    status_service = StatusService()
    model_service = ModelService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Revit Status",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=False,
        ),
        structured_output=True,
    )
    async def get_revit_status() -> StatusResult:
        """
        Report whether Revit and an active document are currently available.

        Workflow:
        - Call this first to verify the Revit API bridge is connected and a document is open.
        - If health is 'healthy', proceed with other tools.
        - If health is 'no_document', use open_document or launch_revit with a file path.
        - If health is 'error', check the error message before retrying.
        """
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
    async def get_revit_model_info() -> ModelInfoResult:
        """
        Return high-level information about the active Revit model.

        Workflow:
        - Use after confirming status with get_revit_status.
        - Provides project structure, element counts, levels, rooms, and linked models.
        - Use element_summary and spatial_organization to understand model scope before operations.
        """
        data = model_service.get_model_info()
        return ModelInfoResult.model_validate(data)
