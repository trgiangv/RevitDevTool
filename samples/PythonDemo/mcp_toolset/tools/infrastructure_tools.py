"""Infrastructure and document management tools."""

from typing import Annotated

from mcp.types import CallToolResult
from pydantic import Field

from dto.infrastructure import (
    CloseDocumentResult,
    GenerateGridsResult,
    GenerateLevelsResult,
    GridAxisSpec,
    LevelSpec,
    SaveDocumentResult,
    SyncResult,
)
from services.document_service import DocumentService
from services.infrastructure_service import InfrastructureService
from shared.mcp_registry import McpRegistry
from shared.mcp_task_execution_meta import McpTaskExecutionMeta
from shared.tool_annotations import destructive_tool, read_only_tool
from shared.tool_results import structured_tool_result


def register_infrastructure_tools(mcp: McpRegistry) -> None:
    """Register status, document lifecycle, and setup MCP tools."""
    infra = InfrastructureService()
    docs = DocumentService()

    @mcp.tool(
        annotations=read_only_tool("Get Status"),
        structured_output=True,
    )
    async def revit_get_status() -> CallToolResult:
        """Health + worksharing + selection info."""
        result = infra.get_status()
        return structured_tool_result(result.summary_text(), result)

    @mcp.tool(
        annotations=destructive_tool("Save Document"),
        structured_output=True,
    )
    async def revit_save_document(
        file_path: Annotated[
            str | None, Field(description="SaveAs path; omit for in-place")
        ] = None,
    ) -> SaveDocumentResult:
        """Save or SaveAs."""
        return docs.save_document(file_path)

    @mcp.tool(
        annotations=destructive_tool("Close Document"),
        structured_output=True,
    )
    async def revit_close_document(
        save: Annotated[bool, Field(description="Save before closing")] = False,
    ) -> CloseDocumentResult:
        """Close active document."""
        return docs.close_document(save)

    @mcp.tool(
        annotations=destructive_tool("Sync With Central"),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_sync_with_central(
        comment: Annotated[str, Field()] = "",
        compact: Annotated[bool, Field()] = False,
        relinquish_all: Annotated[
            bool, Field(description="Relinquish all borrowed worksets")
        ] = False,
        save_local_before: Annotated[
            bool, Field(description="Save local file before sync")
        ] = True,
    ) -> SyncResult:
        """Workshared sync."""
        return docs.sync_with_central(
            comment=comment,
            compact=compact,
            relinquish_all=relinquish_all,
            save_local_before=save_local_before,
        )

    @mcp.tool(
        annotations=destructive_tool("Generate Grids"),
        structured_output=True,
    )
    async def revit_generate_grids(
        vertical: GridAxisSpec,
        horizontal: GridAxisSpec,
        origin: Annotated[
            list[float] | None, Field(description="[X,Y,Z] in feet")
        ] = None,
    ) -> GenerateGridsResult:
        """Create grid system."""
        return infra.generate_grids(vertical, horizontal, origin)

    @mcp.tool(
        annotations=destructive_tool("Generate Levels"),
        structured_output=True,
    )
    async def revit_generate_levels(
        levels: Annotated[list[LevelSpec], Field(description="Level specifications")],
    ) -> GenerateLevelsResult:
        """Create levels batch."""
        return infra.generate_levels(levels)
