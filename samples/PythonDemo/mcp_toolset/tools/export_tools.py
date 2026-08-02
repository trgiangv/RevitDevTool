"""Export and reporting tools."""
from __future__ import annotations
from typing import Annotated

from mcp.server.mcpserver import MCPServer
from mcp.types import CallToolResult, TextContent, ToolAnnotations
from pydantic import Field

from dto.export import ExportResult, ScheduleExportResult
from dto.filters import FilterSpec
from services.export_service import ExportService
from shared.mcp_task_execution_meta import McpTaskExecutionMeta


def register_export_tools(mcp: MCPServer) -> None:
    service = ExportService()

    @mcp.tool(
        annotations=ToolAnnotations(title="Export PDF", readOnlyHint=True),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_export_pdf(
        view_ids: Annotated[list[int] | None, Field(description="View ids; null = active view")] = None,
        directory: Annotated[str | None, Field(description="Output directory")] = None,
        combine_into_single: Annotated[bool, Field(description="Combine views into one PDF")] = False,
    ) -> CallToolResult:
        """Export views to PDF."""
        result = service.export_pdf(view_ids, directory, combine_into_single)
        structured = result.model_dump(by_alias=True)
        return CallToolResult(
            content=[
                TextContent(
                    type="text",
                    text="Exported {} PDF file(s)".format(len(result.file_paths)),
                )
            ],
            structured_content=structured,
        )

    @mcp.tool(
        annotations=ToolAnnotations(title="Export Image", readOnlyHint=True),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_export_image(
        view_ids: Annotated[list[int] | None, Field(description="View ids; null = active view")] = None,
        format: Annotated[str, Field(description="png, jpg, or bmp")] = "png",
        directory: Annotated[str | None, Field()] = None,
        resolution: Annotated[int, Field(description="DPI")] = 150,
    ) -> CallToolResult:
        """Export views to image."""
        result = service.export_image(view_ids, format, directory, resolution)
        structured = result.model_dump(by_alias=True)
        return CallToolResult(
            content=[
                TextContent(
                    type="text",
                    text="Exported {} image file(s)".format(len(result.file_paths)),
                )
            ],
            structured_content=structured,
        )

    @mcp.tool(annotations=ToolAnnotations(title="Export to Excel", readOnlyHint=True), structured_output=True)
    async def revit_export_to_excel(
        filters: Annotated[FilterSpec | None, Field(description="Composable filter specification")] = None,
        parameters: Annotated[list[str] | None, Field(description="Parameter columns")] = None,
        output_path: Annotated[str | None, Field(description="Output file path")] = None,
    ) -> ExportResult:
        """Export element data with filters."""
        return service.export_to_excel(filters, parameters, output_path)

    @mcp.tool(
        annotations=ToolAnnotations(title="Export Schedule", readOnlyHint=True),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_export_schedule(
        schedule_id: Annotated[int, Field(description="Schedule element id")],
        format: Annotated[str, Field(description="csv or xlsx")] = "xlsx",
        output_path: Annotated[str | None, Field(description="Output file path")] = None,
    ) -> ScheduleExportResult:
        """Export schedule to CSV/xlsx."""
        return service.export_schedule(schedule_id, format, output_path)
