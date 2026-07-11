"""Live model JSON resources."""
from __future__ import annotations

from services.model_resource_service import ModelResourceService


def register_model_resources(mcp) -> None:
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
