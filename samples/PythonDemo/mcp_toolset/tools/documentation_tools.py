"""Documentation tools: views, sheets, schedules."""
from __future__ import annotations

from typing import Annotated

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.documentation import (
    ActivateViewResult,
    ApplyViewTemplateResult,
    CreateScheduleResult,
    CreateSheetResult,
    CreateViewResult,
    ListScheduleFieldsResult,
    ListViewsResult,
    PlaceOnSheetResult,
    ScheduleConfig,
)
from services.documentation_service import DocumentationService


def register_documentation_tools(mcp: FastMCP) -> None:
    service = DocumentationService()

    @mcp.tool(annotations=ToolAnnotations(title="Create View", destructiveHint=True), structured_output=True)
    async def revit_create_view(
        view_type: Annotated[str, Field(alias="viewType", description="floor_plan, section, or 3d")],
        level_name: Annotated[str | None, Field(alias="levelName")] = None,
        view_name: Annotated[str | None, Field(alias="viewName")] = None,
        template_name: Annotated[str | None, Field(alias="templateName")] = None,
        min_point: Annotated[list[float] | None, Field(alias="min")] = None,
        max_point: Annotated[list[float] | None, Field(alias="max")] = None,
        direction_angle: Annotated[float | None, Field(alias="directionAngle")] = None,
        depth: Annotated[float | None, Field()] = None,
        is_bounding_box: Annotated[bool | None, Field(alias="isBoundingBox")] = None,
    ) -> CreateViewResult:
        """Create floor plan, section, or 3D view."""
        return service.create_view(
            view_type, level_name, view_name, template_name,
            min_point, max_point, direction_angle, depth, is_bounding_box,
        )

    @mcp.tool(annotations=ToolAnnotations(title="Create Sheet", destructiveHint=True), structured_output=True)
    async def revit_create_sheet(
        title_block_id: Annotated[int | None, Field(alias="titleBlockId")] = None,
    ) -> CreateSheetResult:
        """Create drawing sheet."""
        return service.create_sheet(title_block_id)

    @mcp.tool(annotations=ToolAnnotations(title="Place on Sheet", destructiveHint=True), structured_output=True)
    async def revit_place_on_sheet(
        sheet_id: Annotated[int, Field(alias="sheetId")],
        view_or_schedule_id: Annotated[int, Field(alias="viewOrScheduleId")],
        position: Annotated[list[float] | None, Field(description="[x, y] in feet")] = None,
    ) -> PlaceOnSheetResult:
        """Place view/schedule on sheet."""
        return service.place_on_sheet(sheet_id, view_or_schedule_id, position)

    @mcp.tool(annotations=ToolAnnotations(title="Create Schedule", destructiveHint=True), structured_output=True)
    async def revit_create_schedule(config: ScheduleConfig) -> CreateScheduleResult:
        """Create and configure schedule."""
        return service.create_schedule(config)

    @mcp.tool(annotations=ToolAnnotations(title="Apply View Template", destructiveHint=True), structured_output=True)
    async def revit_apply_view_template(
        view_id: Annotated[int, Field(alias="viewId")],
        template_name: Annotated[str | None, Field(alias="templateName")] = None,
    ) -> ApplyViewTemplateResult:
        """Apply or detach view template."""
        return service.apply_view_template(view_id, template_name)

    @mcp.tool(annotations=ToolAnnotations(title="List Views", readOnlyHint=True), structured_output=True)
    async def revit_list_views(
        include_sheets: Annotated[bool | None, Field(alias="includeSheets")] = True,
        include_templates: Annotated[bool | None, Field(alias="includeTemplates")] = False,
    ) -> ListViewsResult:
        """All views/sheets with metadata."""
        return service.list_views(include_sheets=bool(include_sheets), include_templates=bool(include_templates))

    @mcp.tool(annotations=ToolAnnotations(title="List Schedule Fields", readOnlyHint=True), structured_output=True)
    async def revit_list_schedule_fields(
        category_name: Annotated[str, Field(alias="categoryName")],
    ) -> ListScheduleFieldsResult:
        """Schedulable fields for a category."""
        return service.list_schedule_fields(category_name)

    @mcp.tool(annotations=ToolAnnotations(title="Activate View", destructiveHint=True), structured_output=True)
    async def revit_activate_view(
        view_id: Annotated[int, Field(alias="viewId")],
    ) -> ActivateViewResult:
        """Open view in UI (caution: disrupts engineer)."""
        return service.activate_view(view_id)
