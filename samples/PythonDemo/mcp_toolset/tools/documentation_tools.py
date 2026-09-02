"""Documentation tools: views, sheets, schedules."""

from typing import Annotated

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
from shared.mcp_registry import McpRegistry
from shared.tool_annotations import destructive_tool, read_only_tool


def register_documentation_tools(mcp: McpRegistry) -> None:
    """Register documentation and sheet workflow MCP tools."""
    service = DocumentationService()

    @mcp.tool(
        annotations=destructive_tool("Create View"),
        structured_output=True,
    )
    async def revit_create_view(
        view_type: Annotated[str, Field(description="floor_plan, section, or 3d")],
        level_name: Annotated[
            str | None, Field(description="Level name for plan views")
        ] = None,
        view_name: Annotated[str | None, Field(description="View name")] = None,
        template_name: Annotated[
            str | None, Field(description="View template name")
        ] = None,
        min_point: Annotated[
            list[float] | None, Field(description="Section box min [X,Y,Z] in feet")
        ] = None,
        max_point: Annotated[
            list[float] | None, Field(description="Section box max [X,Y,Z] in feet")
        ] = None,
        direction_angle: Annotated[
            float | None, Field(description="Section direction angle in degrees")
        ] = None,
        depth: Annotated[float | None, Field()] = None,
        is_bounding_box: Annotated[
            bool | None,
            Field(
                description="3D only: when false, create perspective; otherwise isometric"
            ),
        ] = None,
    ) -> CreateViewResult:
        """Create floor plan, section, or 3D view."""
        return service.create_view(
            view_type,
            level_name,
            view_name,
            template_name,
            min_point,
            max_point,
            direction_angle,
            depth,
            is_bounding_box,
        )

    @mcp.tool(
        annotations=destructive_tool("Create Sheet"),
        structured_output=True,
    )
    async def revit_create_sheet(
        title_block_id: Annotated[
            int | None, Field(description="Title block type id")
        ] = None,
    ) -> CreateSheetResult:
        """Create drawing sheet."""
        return service.create_sheet(title_block_id)

    @mcp.tool(
        annotations=destructive_tool("Place on Sheet"),
        structured_output=True,
    )
    async def revit_place_on_sheet(
        sheet_id: Annotated[int, Field(description="Sheet element id")],
        view_or_schedule_id: Annotated[
            int, Field(description="View or schedule element id")
        ],
        position: Annotated[
            list[float] | None, Field(description="[x, y] in feet")
        ] = None,
    ) -> PlaceOnSheetResult:
        """Place view/schedule on sheet."""
        return service.place_on_sheet(sheet_id, view_or_schedule_id, position)

    @mcp.tool(
        annotations=destructive_tool("Create Schedule"),
        structured_output=True,
    )
    async def revit_create_schedule(config: ScheduleConfig) -> CreateScheduleResult:
        """Create and configure schedule."""
        return service.create_schedule(config)

    @mcp.tool(
        annotations=destructive_tool("Apply View Template"),
        structured_output=True,
    )
    async def revit_apply_view_template(
        view_id: Annotated[int, Field(description="View element id")],
        template_name: Annotated[
            str | None, Field(description="Template name; null detaches")
        ] = None,
    ) -> ApplyViewTemplateResult:
        """Apply or detach view template."""
        return service.apply_view_template(view_id, template_name)

    @mcp.tool(
        annotations=read_only_tool("List Views"),
        structured_output=True,
    )
    async def revit_list_views(
        include_sheets: Annotated[
            bool | None, Field(description="Include sheets")
        ] = False,
        include_templates: Annotated[
            bool | None, Field(description="Include view templates")
        ] = False,
    ) -> ListViewsResult:
        """All views/sheets with metadata."""
        return service.list_views(
            include_sheets=bool(include_sheets),
            include_templates=bool(include_templates),
        )

    @mcp.tool(
        annotations=read_only_tool("List Schedule Fields"),
        structured_output=True,
    )
    async def revit_list_schedule_fields(
        category_name: Annotated[str, Field(description="Category display name")],
    ) -> ListScheduleFieldsResult:
        """Schedulable fields for a category."""
        return service.list_schedule_fields(category_name)

    @mcp.tool(
        annotations=destructive_tool("Activate View"),
        structured_output=True,
    )
    async def revit_activate_view(
        view_id: Annotated[int, Field(description="View element id")],
    ) -> ActivateViewResult:
        """Open view in UI (caution: disrupts engineer)."""
        return service.activate_view(view_id)
