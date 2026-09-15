"""Drawing mutation DTOs."""

from pydantic import BaseModel


class DrawLineResult(BaseModel):
    handle: str
    layer: str
    start: list[float]
    end: list[float]
    length: float


class DrawCircleResult(BaseModel):
    handle: str
    layer: str
    center: list[float]
    radius: float
    area: float


class HighlightEntitiesResult(BaseModel):
    selected_count: int
    missing: list[str]


class EraseEntitiesResult(BaseModel):
    erased_count: int
    missing: list[str]
