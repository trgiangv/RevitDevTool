"""Export and reporting tools."""

from typing import Annotated

from mcp.types import CallToolResult
from pydantic import Field

from dto.export import ExportResult, ScheduleExportResult
from dto.filters import FilterSpec
from services.export_service import ExportService
from shared.mcp_registry import McpRegistry
from shared.mcp_task_execution_meta import McpTaskExecutionMeta
from shared.tool_annotations import read_only_tool
from shared.tool_results import structured_tool_result


def register_export_tools(mcp: McpRegistry) -> None:
    """Register export and reporting MCP tools."""
    service = ExportService()

    @mcp.tool(
        annotations=read_only_tool("Export PDF"),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_export_pdf(
        view_ids: Annotated[
            list[int] | None, Field(description="View ids; null = active view")
        ] = None,
        directory: Annotated[str | None, Field(description="Output directory")] = None,
        combine_into_single: Annotated[
            bool, Field(description="Combine views into one PDF")
        ] = False,
    ) -> CallToolResult:
        """Export views to PDF."""
        result = service.export_pdf(view_ids, directory, combine_into_single)
        return structured_tool_result(
            "Exported {} PDF file(s)".format(len(result.file_paths)),
            result,
        )

    @mcp.tool(
        annotations=read_only_tool("Export Image"),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_export_image(
        view_ids: Annotated[
            list[int] | None, Field(description="View ids; null = active view")
        ] = None,
        format: Annotated[str, Field(description="png, jpg, or bmp")] = "png",
        directory: Annotated[str | None, Field()] = None,
        resolution: Annotated[int, Field(description="DPI")] = 150,
    ) -> CallToolResult:
        """Export views to image."""
        result = service.export_image(
            view_ids, export_format=format, directory=directory, resolution=resolution
        )
        return structured_tool_result(
            "Exported {} image file(s)".format(len(result.file_paths)),
            result,
        )

    @mcp.tool(
        annotations=read_only_tool("Export to Excel"),
        structured_output=True,
    )
    async def revit_export_to_excel(
        filters: Annotated[
            FilterSpec | None, Field(description="Composable filter specification")
        ] = None,
        parameters: Annotated[
            list[str] | None, Field(description="Parameter columns")
        ] = None,
        output_path: Annotated[
            str | None, Field(description="Output file path")
        ] = None,
    ) -> ExportResult:
        """Export element data with filters."""
        return service.export_to_excel(filters, parameters, output_path)

    @mcp.tool(
        annotations=read_only_tool("Export Schedule"),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_export_schedule(
        schedule_id: Annotated[int, Field(description="Schedule element id")],
        format: Annotated[str, Field(description="csv or xlsx")] = "xlsx",
        output_path: Annotated[
            str | None, Field(description="Output file path")
        ] = None,
    ) -> ScheduleExportResult:
        """Export schedule to CSV/xlsx."""
        return service.export_schedule(
            schedule_id, export_format=format, output_path=output_path
        )
