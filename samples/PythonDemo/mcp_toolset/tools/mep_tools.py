"""MEP engineering tools."""

from typing import Annotated

from pydantic import Field

from dto.mep import (
    ConduitSpec,
    DuctSpec,
    InsulateDuctResult,
    ListMepSystemsResult,
    PipeSpec,
    PlaceSegmentResult,
)
from services.mep_service import MepService
from shared.mcp_registry import McpRegistry
from shared.tool_annotations import destructive_tool, read_only_tool


def register_mep_tools(mcp: McpRegistry) -> None:
    """Register MEP placement and system MCP tools."""
    service = MepService()

    @mcp.tool(
        annotations=destructive_tool("Place Duct"),
        structured_output=True,
    )
    async def revit_place_duct(spec: DuctSpec) -> PlaceSegmentResult:
        """Create duct segment."""
        return service.place_duct(spec)

    @mcp.tool(
        annotations=destructive_tool("Place Pipe"),
        structured_output=True,
    )
    async def revit_place_pipe(spec: PipeSpec) -> PlaceSegmentResult:
        """Create pipe segment."""
        return service.place_pipe(spec)

    @mcp.tool(
        annotations=destructive_tool("Place Conduit"),
        structured_output=True,
    )
    async def revit_place_conduit(spec: ConduitSpec) -> PlaceSegmentResult:
        """Create conduit segment."""
        return service.place_conduit(spec)

    @mcp.tool(
        annotations=read_only_tool("List MEP Systems"),
        structured_output=True,
    )
    async def revit_list_mep_systems(
        kind: Annotated[
            str, Field(description="duct, pipe, electrical, or all")
        ] = "all",
    ) -> ListMepSystemsResult:
        """Enumerate systems/circuits."""
        return service.list_mep_systems(kind)

    @mcp.tool(
        annotations=destructive_tool("Insulate Duct System"),
        structured_output=True,
    )
    async def revit_insulate_duct_system(
        system_id: Annotated[int, Field(description="MEP system element id")],
        thickness_mm: Annotated[
            float, Field(description="Insulation thickness in millimeters")
        ],
    ) -> InsulateDuctResult:
        """Apply duct insulation."""
        return service.insulate_duct_system(system_id, thickness_mm)
