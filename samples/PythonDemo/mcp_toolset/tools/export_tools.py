"""Export and reporting tools."""
from __future__ import annotations
from typing import Annotated

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.export import ExportImageResult, ExportPdfResult, ExportResult, ScheduleExportResult
from dto.filters import FilterSpec
from services.export_service import ExportService


def register_export_tools(mcp: FastMCP) -> None:
    service = ExportService()

    @mcp.tool(annotations=ToolAnnotations(title="Export PDF", readOnlyHint=True), structured_output=True)
    async def revit_export_pdf(
        view_ids: Annotated[list[int] | None, Field(alias="viewIds")] = None,
        directory: Annotated[str | None, Field(description="Output directory")] = None,
        combine_into_single: Annotated[bool, Field(alias="combineIntoSingle")] = False,
    ) -> ExportPdfResult:
        """Export views to PDF."""
        return service.export_pdf(view_ids, directory, combine_into_single)

    @mcp.tool(annotations=ToolAnnotations(title="Export Image", readOnlyHint=True), structured_output=True)
    async def revit_export_image(
        view_ids: Annotated[list[int] | None, Field(alias="viewIds")] = None,
        format: Annotated[str, Field(description="png, jpg, or bmp")] = "png",
        directory: Annotated[str | None, Field()] = None,
        resolution: Annotated[int, Field(description="DPI")] = 150,
    ) -> ExportImageResult:
        """Export views to image."""
        return service.export_image(view_ids, format, directory, resolution)

    @mcp.tool(annotations=ToolAnnotations(title="Export to Excel", readOnlyHint=True), structured_output=True)
    async def revit_export_to_excel(
        filters: Annotated[FilterSpec | None, Field(description="Composable filter specification")] = None,
        parameters: Annotated[list[str] | None, Field(description="Parameter columns")] = None,
        output_path: Annotated[str | None, Field(alias="outputPath")] = None,
    ) -> ExportResult:
        """Export element data with filters."""
        return service.export_to_excel(filters, parameters, output_path)

    @mcp.tool(annotations=ToolAnnotations(title="Export Schedule", readOnlyHint=True), structured_output=True)
    async def revit_export_schedule(
        schedule_id: Annotated[int, Field(alias="scheduleId")],
        format: Annotated[str, Field(description="csv or xlsx")] = "xlsx",
        output_path: Annotated[str | None, Field(alias="outputPath")] = None,
    ) -> ScheduleExportResult:
        """Export schedule to CSV/xlsx."""
        return service.export_schedule(schedule_id, format, output_path)

