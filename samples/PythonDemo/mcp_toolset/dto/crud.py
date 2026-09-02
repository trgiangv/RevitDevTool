"""CRUD and element operation DTOs."""

from typing import Any

from pydantic import BaseModel, Field

from dto.common import ToolErrorEntry


class ParameterUpdate(BaseModel):
    param_name: str = Field(alias="param_name")
    value: Any

    model_config = {"populate_by_name": True}


class WriteParametersResult(BaseModel):
    success_count: int
    failure_count: int
    failures: list[ToolErrorEntry] | None = None


class PlacementSpec(BaseModel):
    x: float
    y: float
    z: float
    rotation: float = 0.0
    level_name: str | None = Field(default=None, alias="levelName")
    host_id: int | None = Field(default=None, alias="hostId")

    model_config = {"populate_by_name": True}


class CreatedInstance(BaseModel):
    id: int
    location: dict[str, float]


class PlaceFamilyResult(BaseModel):
    created: list[CreatedInstance]
    failures: list[ToolErrorEntry] | None = None


class MoveElementsResult(BaseModel):
    moved_count: int
    failures: list[ToolErrorEntry] | None = None


class RotateElementsResult(BaseModel):
    rotated_count: int
    failures: list[ToolErrorEntry] | None = None


class DeleteElementsResult(BaseModel):
    deleted_count: int
    warning: str | None = None
    failures: list[ToolErrorEntry] | None = None
    dry_run_results: list[dict] | None = Field(default=None, alias="dryRunResults")

    model_config = {"populate_by_name": True}


class CloneParametersResult(BaseModel):
    success_count: int
    skipped: list[dict]


class SwapTypeResult(BaseModel):
    success_count: int
    failure_count: int
    failures: list[ToolErrorEntry] | None = None


class HighlightElementsResult(BaseModel):
    selected_count: int
