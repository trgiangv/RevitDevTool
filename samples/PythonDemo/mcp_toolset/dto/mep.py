"""MEP engineering DTOs."""
from __future__ import annotations

from pydantic import BaseModel, Field


class DuctSpec(BaseModel):
    duct_type_id: int = Field(alias="ductTypeId")
    system_type_id: int = Field(alias="systemTypeId")
    level_id: int = Field(alias="levelId")
    start: list[float]
    end: list[float]
    width: float | None = None
    height: float | None = None
    diameter: float | None = None
    slope: float | None = None

    model_config = {"populate_by_name": True}


class PipeSpec(BaseModel):
    pipe_type_id: int = Field(alias="pipeTypeId")
    system_type_id: int = Field(alias="systemTypeId")
    level_id: int = Field(alias="levelId")
    start: list[float]
    end: list[float]
    diameter: float
    slope: float | None = None

    model_config = {"populate_by_name": True}


class ConduitSpec(BaseModel):
    conduit_type_id: int = Field(alias="conduitTypeId")
    system_type_id: int = Field(alias="systemTypeId")
    level_id: int = Field(alias="levelId")
    start: list[float]
    end: list[float]
    diameter: float

    model_config = {"populate_by_name": True}


class PlaceSegmentResult(BaseModel):
    element_id: int = Field(alias="elementId")
    length: float

    model_config = {"populate_by_name": True}


class MepSystemItem(BaseModel):
    id: int
    name: str
    type: str
    element_count: int = Field(alias="element_count")
    classification: str

    model_config = {"populate_by_name": True}


class ListMepSystemsResult(BaseModel):
    systems: list[MepSystemItem]


class InsulateDuctResult(BaseModel):
    insulated_count: int
