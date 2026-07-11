from __future__ import annotations
from pydantic import BaseModel, Field


class LevelInfo(BaseModel):
    name: str
    elevation: float = Field(description="Level elevation in project units")
    element_id: int


class RoomInfo(BaseModel):
    name: str
    number: str
    level: str
    is_placed: bool
    area: float | None = Field(default=None, description="Room area (only if placed)")


class ElementSummary(BaseModel):
    total_elements: int
    by_category: dict[str, int]


class ModelHealthInfo(BaseModel):
    total_warnings: int
    critical_warnings: int
    unplaced_rooms: int


class ProjectInfo(BaseModel):
    name: str
    number: str
    client: str
    file_name: str


class LinkedModelInfo(BaseModel):
    name: str
    is_loaded: bool
    is_pinned: bool


class DocumentationSummary(BaseModel):
    total_views: int
    view_breakdown: dict[str, int]
    sheets_count: int


class SpatialOrganization(BaseModel):
    levels: list[LevelInfo]
    rooms: list[RoomInfo]
    room_count: int


class LinkedModelsInfo(BaseModel):
    count: int
    models: list[LinkedModelInfo]


class ModelInfoResult(BaseModel):
    project_info: ProjectInfo
    element_summary: ElementSummary
    model_health: ModelHealthInfo
    spatial_organization: SpatialOrganization
    documentation: DocumentationSummary
    linked_models: LinkedModelsInfo


class LevelsResult(BaseModel):
    levels: list[LevelInfo]
    count: int
