"""Query and model intelligence DTOs."""
from __future__ import annotations

from pydantic import BaseModel, Field


class BoundingBoxResult(BaseModel):
    min: list[float]
    max: list[float]


class ElementSummaryItem(BaseModel):
    id: int
    category: str | None = None
    family: str | None = None
    type: str | None = None
    level: str | None = None
    name: str | None = None
    workset: str | None = None
    bbox: BoundingBoxResult | None = None


class FindElementsResult(BaseModel):
    count: int
    truncated: bool
    elements: list[ElementSummaryItem]


class ParameterEntry(BaseModel):
    name: str
    value: str
    storage: str
    writable: bool
    builtin: bool = Field(alias="builtin")
    is_shared: bool = Field(alias="isShared")

    model_config = {"populate_by_name": True}


class ElementParametersResult(BaseModel):
    id: int
    params: list[ParameterEntry]


class ReadParametersResult(BaseModel):
    elements: list[ElementParametersResult]


class TypeInfo(BaseModel):
    id: int
    name: str
    family: str
    category: str


class ListTypesResult(BaseModel):
    types: list[TypeInfo]


class CategoryParameterInfo(BaseModel):
    name: str
    storage_type: str = Field(alias="storageType")
    sample_value: str = Field(alias="sampleValue")

    model_config = {"populate_by_name": True}


class ListCategoryParametersResult(BaseModel):
    parameters: list[CategoryParameterInfo]


class RoomItem(BaseModel):
    id: int
    name: str
    number: str
    area: float
    level: str
    department: str
    location: list[float] | None = None


class ListRoomsResult(BaseModel):
    rooms: list[RoomItem]


class LinkItem(BaseModel):
    id: int
    name: str
    type: str
    path: str
    loaded: bool


class ListLinksResult(BaseModel):
    links: list[LinkItem]


class CategoryCount(BaseModel):
    name: str
    count: int


class LevelSummary(BaseModel):
    id: int
    name: str
    elevation: float


class PhaseSummary(BaseModel):
    id: int
    name: str


class WorksetSummary(BaseModel):
    id: int
    name: str
    kind: str


class ModelSummaryResult(BaseModel):
    project: dict
    categories: list[CategoryCount]
    warnings_count: int
    levels: list[LevelSummary]
    phases: list[PhaseSummary]
    worksets: list[WorksetSummary]
    links: list[LinkItem]
