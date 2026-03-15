"""Excel and data export tools for Revit model data."""
from __future__ import annotations

from typing import Annotated, Literal

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.export import ExportResult, ScheduleExportResult
from dto.filters import FilteredExportResult, FilterRequest, FilterSpec, QueryElementsResult
from services.export_service import ExportService


def register_export_tools(mcp: FastMCP) -> None:
    service = ExportService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Export Elements to Excel",
            readOnlyHint=True,
            idempotentHint=False,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def export_elements_to_excel(
        categories: Annotated[
            list[str],
            Field(description=(
                "List of Revit category names to export, e.g. ['Walls', 'Doors', 'Windows']. "
                "Use 'list_family_categories' or 'get_revit_model_info' to discover available categories."
            )),
        ],
        parameters: Annotated[
            list[str] | None,
            Field(description=(
                "Optional list of parameter names to include as columns. "
                "If omitted, ALL instance + type parameters are exported via ParametersMap. "
                "Use 'list_category_parameters' to discover available parameters."
            )),
        ] = None,
        output_path: Annotated[
            str | None,
            Field(description=(
                "Absolute file path for the output .xlsx file. "
                "If omitted, saves to a temp directory and returns the path."
            )),
        ] = None,
    ) -> ExportResult:
        """Export Revit elements from one or more categories to an Excel (.xlsx) file.

        Each row represents one element. Columns include ElementId, Name, Category,
        and all requested parameters. When parameters is omitted, all instance and type
        parameters with values are included via ParametersMap (instance parameters take
        priority over type parameters with the same name).

        Typical workflow:
        1. Call 'get_revit_model_info' to see available categories and counts.
        2. Optionally call 'list_category_parameters' to choose specific parameters.
        3. Call this tool with the desired categories and parameters.
        """
        return service.export_elements_to_excel(
            categories=categories,
            parameters=parameters,
            output_path=output_path,
        )

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Export Schedule to Excel",
            readOnlyHint=True,
            idempotentHint=False,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def export_schedule_to_excel(
        schedule_name: Annotated[
            str,
            Field(description=(
                "Exact name of the Revit schedule view to export. "
                "Use 'list_revit_views' to discover available schedules."
            )),
        ],
        output_path: Annotated[
            str | None,
            Field(description=(
                "Absolute file path for the output .xlsx file. "
                "If omitted, saves to a temp directory and returns the path."
            )),
        ] = None,
    ) -> ScheduleExportResult:
        """Export a Revit schedule view to an Excel (.xlsx) file.

        Reads the schedule's header and body sections, constructs a Polars DataFrame,
        and writes it to Excel. The worksheet name matches the schedule name.
        """
        return service.export_schedule_to_excel(
            schedule_name=schedule_name,
            output_path=output_path,
        )

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Export Filtered Elements to Excel",
            readOnlyHint=True,
            idempotentHint=False,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def export_filtered_elements(
        filters: Annotated[
            list[FilterSpec],
            Field(description=(
                "List of element filters to apply. Each filter is an object with a 'type' field "
                "that determines its behaviour. Available filter types:\n"
                "  - category: {type:'category', names:['Walls','Doors'], inverted:false}\n"
                "  - parameter_string: {type:'parameter_string', parameter_name:'Mark', "
                "operator:'contains', value:'WA'}\n"
                "    operators: equals, not_equals, contains, not_contains, begins_with, "
                "not_begins_with, ends_with, not_ends_with\n"
                "  - parameter_numeric: {type:'parameter_numeric', parameter_name:'Area', "
                "operator:'greater', value:100.0, epsilon:1e-6}\n"
                "    operators: equals, not_equals, greater, greater_or_equal, less, less_or_equal\n"
                "  - parameter_has_value: {type:'parameter_has_value', parameter_name:'Comments', "
                "has_value:true}\n"
                "  - level: {type:'level', level_name:'Level 1'}\n"
                "  - class: {type:'class', class_names:['Wall','FamilyInstance']}\n"
                "    common classes: Wall, Floor, Ceiling, FamilyInstance, RoofBase, Group, Room, Area\n"
                "  - bounding_box: {type:'bounding_box', min_point:[0,0,0], max_point:[100,100,100]}\n"
                "  - view: {type:'view', view_name:'Level 1'} (null = active view)\n"
                "  - element_type: {type:'element_type', is_type:false}\n"
                "  - physical_model: {type:'physical_model'}\n"
                "  - exclusion: {type:'exclusion', element_ids:[12345,67890]}\n"
                "  - workset: {type:'workset', workset_name:'Shared Levels and Grids'}\n"
            )),
        ],
        logic: Annotated[
            Literal["and", "or"],
            Field(description="How to combine filters: 'and' = all must match, 'or' = any must match"),
        ] = "and",
        parameters: Annotated[
            list[str] | None,
            Field(description=(
                "Optional list of parameter names to include as columns. "
                "If omitted, ALL instance + type parameters are exported via ParametersMap."
            )),
        ] = None,
        output_path: Annotated[
            str | None,
            Field(description="Absolute file path for the output .xlsx file. If omitted, uses temp directory."),
        ] = None,
    ) -> FilteredExportResult:
        """Export elements matching flexible filter criteria to an Excel (.xlsx) file.

        This tool provides full control over which elements to export using
        a composable filter system. Filters can target categories, parameter values,
        levels, bounding boxes, element classes, and more.

        Typical workflows:
        1. Simple category export:
           filters=[{type:'category', names:['Walls']}]
        2. Walls on a specific level with Mark containing 'EXT':
           filters=[
             {type:'category', names:['Walls']},
             {type:'level', level_name:'Level 1'},
             {type:'parameter_string', parameter_name:'Mark', operator:'contains', value:'EXT'}
           ], logic='and'
        3. Preview first: call 'query_elements' with the same filters, then export.
        """
        request = FilterRequest(filters=filters, logic=logic)
        return service.export_filtered_elements(
            filter_request=request,
            parameters=parameters,
            output_path=output_path,
        )

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Query Elements by Filter",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=False,
        ),
        structured_output=True,
    )
    async def query_elements(
        filters: Annotated[
            list[FilterSpec],
            Field(description=(
                "List of element filters (same format as export_filtered_elements). "
                "See export_filtered_elements description for all filter types and examples."
            )),
        ],
        logic: Annotated[
            Literal["and", "or"],
            Field(description="How to combine filters: 'and' = all must match, 'or' = any must match"),
        ] = "and",
        sample_size: Annotated[
            int,
            Field(description="Number of sample elements to return in the result (max 100)", ge=1, le=100),
        ] = 20,
    ) -> QueryElementsResult:
        """Query elements matching filter criteria and return a summary without exporting.

        Use this tool to preview which elements a filter will select before committing
        to an export. Returns total count, per-category breakdown, and a sample of
        matching elements with their ElementId, Name, and Category.

        Recommended workflow:
        1. Call query_elements with your filters to verify the selection.
        2. If the result looks correct, call export_filtered_elements with the same filters.
        """
        request = FilterRequest(filters=filters, logic=logic)
        return service.query_elements(filter_request=request, sample_size=sample_size)
