"""Visualization and annotation DTOs."""
from __future__ import annotations

from pydantic import BaseModel, Field


class ColorByParameterResult(BaseModel):
    groups_colored: int
    element_count: int
    warning: str | None = None


class ClearOverridesResult(BaseModel):
    cleared: int


class TagPlacement(BaseModel):
    view_id: int = Field(alias="viewId")
    element_ids: list[int] = Field(alias="elementIds")

    model_config = {"populate_by_name": True}


class PlaceTagsResult(BaseModel):
    tags_placed: int


class OverrideColorsResult(BaseModel):
    overridden_count: int
