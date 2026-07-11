from __future__ import annotations
from pydantic import BaseModel, Field


class FamilyTypeInfo(BaseModel):
    family_name: str
    type_name: str
    category: str
    is_active: bool


class FamilyListResult(BaseModel):
    families: list[FamilyTypeInfo]
    count: int
    filtered_by: str | None = None


class FamilyCategoryInfo(BaseModel):
    category: str
    family_count: int


class FamilyCategoriesResult(BaseModel):
    categories: list[FamilyCategoryInfo]
    count: int


class FamilyPlacementResult(BaseModel):
    element_id: int
    family_name: str
    type_name: str | None = None
    requested_location: dict[str, float]
    actual_location: dict[str, float]
    rotation_degrees: float
    level: str | None = None
    properties_set: list[str] = Field(default_factory=list)
    properties_failed: list[str] = Field(default_factory=list)
