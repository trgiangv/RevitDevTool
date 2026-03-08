"""Family and placement tools."""

from typing import Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations

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
        family_name: str,
        type_name: str | None = None,
        x: float = 0.0,
        y: float = 0.0,
        z: float = 0.0,
        rotation: float = 0.0,
        level_name: str | None = None,
        properties: dict[str, Any] | None = None,
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
    async def list_families(contains: str | None = None, limit: int = 50, ctx: Context | None = None) -> dict[str, Any]:
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
