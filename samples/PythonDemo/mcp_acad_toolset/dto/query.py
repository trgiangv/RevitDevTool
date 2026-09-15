"""Query and drawing-intelligence DTOs."""

from pydantic import BaseModel, Field


class StatusResult(BaseModel):
    healthy: bool
    document_name: str | None = None
    file_path: str | None = None
    version: str | None = None
    document_count: int | None = None
    entity_count: int | None = None
    layer_count: int | None = None
    selection_count: int | None = None

    def summary_text(self) -> str:
        if not self.healthy:
            return "No active AutoCAD document"
        return "Drawing '{}': {} entities, {} layers".format(
            self.document_name or "(unnamed)",
            self.entity_count or 0,
            self.layer_count or 0,
        )


class LayerItem(BaseModel):
    name: str
    is_off: bool
    is_frozen: bool
    is_locked: bool
    color: str
    color_index: int | None = None


class ListLayersResult(BaseModel):
    count: int
    layers: list[LayerItem]


class EntitySummary(BaseModel):
    handle: str
    type: str
    layer: str
    color: str | None = None
    length: float | None = None
    area: float | None = None
    bbox: list[float] | None = Field(
        default=None,
        description="[minX, minY, minZ, maxX, maxY, maxZ]",
    )


class FindEntitiesResult(BaseModel):
    count: int
    truncated: bool
    entities: list[EntitySummary]


class GetSelectionResult(BaseModel):
    count: int
    entities: list[EntitySummary]
