"""Live drawing JSON resources."""

from services.model_resource_service import ModelResourceService
from shared.mcp_registry import McpRegistry


def register_model_resources(mcp: McpRegistry) -> None:
    """Register live AutoCAD drawing MCP resources."""
    service = ModelResourceService()

    @mcp.resource("acad://drawing/context")
    async def get_drawing_context() -> str:
        """Active drawing name, path, entity/layer counts, selection size."""
        return service.get_context()

    @mcp.resource("acad://drawing/layers")
    async def get_drawing_layers() -> str:
        """Layer table with off/frozen/locked state and color."""
        return service.get_layers()

    @mcp.resource("acad://drawing/selection")
    async def get_drawing_selection() -> str:
        """Currently implied/selected entities."""
        return service.get_selection()

    @mcp.resource("acad://entity/{handle}")
    async def get_entity_resource(handle: str) -> str:
        """Compact entity summary: type, layer, color, length/area, bbox."""
        return service.get_entity(handle)
