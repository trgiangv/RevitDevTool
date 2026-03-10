"""Family and placement tools."""

from typing import Annotated, Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from services.family_service import FamilyService
from utils import try_log


def register_family_tools(mcp: FastMCP, family_service: FamilyService) -> None:
    @mcp.tool(
        annotations=ToolAnnotations(
            title="Place Family",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def place_family(
        family_name: Annotated[str, Field(description="Name of the family to place, e.g. 'Single-Flush'")],
        type_name: Annotated[str | None, Field(description="Family type name; uses first available type if omitted")] = None,
        x: Annotated[float, Field(description="X coordinate in feet")] = 0.0,
        y: Annotated[float, Field(description="Y coordinate in feet")] = 0.0,
        z: Annotated[float, Field(description="Z coordinate in feet")] = 0.0,
        rotation: Annotated[float, Field(description="Rotation angle in degrees")] = 0.0,
        level_name: Annotated[str | None, Field(description="Level name to place the family on; uses active level if omitted")] = None,
        properties: Annotated[dict[str, Any] | None, Field(description="Optional parameter overrides as key-value pairs")] = None,
        ctx: Context | None = None,
    ) -> dict[str, Any]:
        """Place a family instance in the model."""
        await try_log(ctx, "info", "Placing family '{}'".format(family_name))
        return family_service.place_family(
            family_name=family_name,
            type_name=type_name,
            x=x,
            y=y,
            z=z,
            rotation=rotation,
            level_name=level_name,
            properties=properties,
        )

    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Families",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_families(
        contains: Annotated[str | None, Field(description="Optional substring filter applied to family and type names")] = None,
        limit: Annotated[int, Field(description="Maximum number of family types to return", ge=1, le=500)] = 50,
        ctx: Context | None = None,
    ) -> dict[str, Any]:
        """List available family types, optionally filtered by substring."""
        _ = ctx
        return family_service.list_families(contains=contains, limit=limit)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Family Categories",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_family_categories(ctx: Context | None = None) -> dict[str, Any]:
        """List family categories present in the model."""
        _ = ctx
        return family_service.list_family_categories()
