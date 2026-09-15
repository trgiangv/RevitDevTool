"""Helpers for consistent MCP CallToolResult construction in tool modules."""

from typing import Any

from mcp.types import CallToolResult, TextContent
from pydantic import BaseModel


def structured_tool_result(
    summary: str,
    result: BaseModel,
    *,
    by_alias: bool = True,
) -> CallToolResult:
    return CallToolResult(
        content=[TextContent(type="text", text=summary)],
        structured_content=result.model_dump(by_alias=by_alias),
    )


def structured_payload_result(summary: str, payload: dict[str, Any]) -> CallToolResult:
    return CallToolResult(
        content=[TextContent(type="text", text=summary)],
        structured_content=payload,
    )
