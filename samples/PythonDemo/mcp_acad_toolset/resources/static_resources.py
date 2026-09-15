"""Static embedded markdown resources."""

import os

from shared.mcp_registry import McpRegistry

_CONTENT_DIR = os.path.join(os.path.dirname(__file__), "content")


def _load(filename: str) -> str:
    path = os.path.join(_CONTENT_DIR, filename)
    with open(path, encoding="utf-8") as f:
        return f.read()


def register_static_resources(mcp: McpRegistry) -> None:
    """Register static markdown MCP resources."""

    @mcp.resource("acad://toolset/capabilities")
    async def get_capabilities() -> str:
        """Full tool catalog with usage guide."""
        return _load("capabilities.md")
