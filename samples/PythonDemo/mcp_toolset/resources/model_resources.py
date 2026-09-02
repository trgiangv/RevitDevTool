"""Live model JSON resources."""

from services.model_resource_service import ModelResourceService
from shared.mcp_registry import McpRegistry


def register_model_resources(mcp: McpRegistry) -> None:
    """Register live model JSON MCP resources."""
    service = ModelResourceService()

    @mcp.resource("revit://model/types")
    async def get_model_types() -> str:
        """Family types, MEP system types, view templates, title blocks."""
        return service.get_types()

    @mcp.resource("revit://model/levels")
    async def get_model_levels() -> str:
        """Levels with elevations and associated views."""
        return service.get_levels()

    @mcp.resource("revit://model/views")
    async def get_model_views() -> str:
        """Views and sheets with metadata."""
        return service.get_views()

    @mcp.resource("revit://model/worksets")
    async def get_model_worksets() -> str:
        """Worksets with editability and element counts."""
        return service.get_worksets()

    @mcp.resource("revit://model/links")
    async def get_model_links() -> str:
        """Revit links and CAD imports."""
        return service.get_links()

    @mcp.resource("revit://model/selection")
    async def get_model_selection() -> str:
        """Currently selected elements."""
        return service.get_selection()

    @mcp.resource("revit://model/grids")
    async def get_model_grids() -> str:
        """Grid names, IDs, and geometry."""
        return service.get_grids()

    @mcp.resource("revit://element/{element_id}")
    async def get_element_resource(element_id: str) -> str:
        """Compact element summary: category, family/type, level, pinned, workset, bounding box."""
        return service.get_element(int(element_id))

    @mcp.resource("revit://schedule/{schedule_id}/preview")
    async def get_schedule_preview_resource(schedule_id: str) -> str:
        """CSV preview of schedule body rows (default 30 rows)."""
        return service.get_schedule_preview(int(schedule_id))
