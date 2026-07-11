"""MEP engineering tools."""
from __future__ import annotations

from typing import Annotated

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
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


def register_mep_tools(mcp: FastMCP) -> None:
    service = MepService()

    @mcp.tool(annotations=ToolAnnotations(title="Place Duct", destructiveHint=True), structured_output=True)
    async def revit_place_duct(spec: DuctSpec) -> PlaceSegmentResult:
        """Create duct segment."""
        return service.place_duct(spec)

    @mcp.tool(annotations=ToolAnnotations(title="Place Pipe", destructiveHint=True), structured_output=True)
    async def revit_place_pipe(spec: PipeSpec) -> PlaceSegmentResult:
        """Create pipe segment."""
        return service.place_pipe(spec)

    @mcp.tool(annotations=ToolAnnotations(title="Place Conduit", destructiveHint=True), structured_output=True)
    async def revit_place_conduit(spec: ConduitSpec) -> PlaceSegmentResult:
        """Create conduit segment."""
        return service.place_conduit(spec)

    @mcp.tool(annotations=ToolAnnotations(title="List MEP Systems", readOnlyHint=True), structured_output=True)
    async def revit_list_mep_systems(
        kind: Annotated[str, Field(description="duct, pipe, electrical, or all")] = "all",
    ) -> ListMepSystemsResult:
        """Enumerate systems/circuits."""
        return service.list_mep_systems(kind)

    @mcp.tool(annotations=ToolAnnotations(title="Insulate Duct System", destructiveHint=True), structured_output=True)
    async def revit_insulate_duct_system(
        system_id: Annotated[int, Field(alias="systemId")],
        thickness_mm: Annotated[float, Field(alias="thickness_mm")],
    ) -> InsulateDuctResult:
        """Apply duct insulation."""
        return service.insulate_duct_system(system_id, thickness_mm)
