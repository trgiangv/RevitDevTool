"""Factory helpers for MCP ToolAnnotations using Pydantic field names."""

from mcp.types import ToolAnnotations


def read_only_tool(
    title: str,
    *,
    idempotent: bool | None = None,
    open_world: bool | None = None,
) -> ToolAnnotations:
    return ToolAnnotations(
        title=title,
        read_only_hint=True,
        idempotent_hint=idempotent,
        open_world_hint=open_world,
    )


def destructive_tool(title: str, *, destructive: bool = True) -> ToolAnnotations:
    return ToolAnnotations(title=title, destructive_hint=destructive)
