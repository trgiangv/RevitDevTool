from __future__ import annotations
from pydantic import BaseModel, Field


class ViewInfo(BaseModel):
    view_name: str
    view_type: str
    view_id: int
    is_template: bool
    scale: int | None = None
    crop_box_active: bool = False
    detail_level: str = "Unknown"
    discipline: str = "Unknown"
    view_family_type: str = "Unknown"


class ViewInfoResult(BaseModel):
    view_info: ViewInfo


class ViewListResult(BaseModel):
    views_by_type: dict[str, list[str]]
    total_exportable_views: int


class LocationPoint(BaseModel):
    type: str = "point"
    x: float
    y: float
    z: float


class LocationCurve(BaseModel):
    type: str = "curve"
    start: dict[str, float]
    end: dict[str, float]


class ViewElementInfo(BaseModel):
    element_id: int
    name: str
    category: str
    category_id: int | None = None
    level: str | None = None
    level_id: int | None = None
    location: dict | None = None


class ViewElementsResult(BaseModel):
    current_view: str
    total_elements: int
    returned_elements: int
    truncated: bool
    category_counts: dict[str, int]
    elements: list[ViewElementInfo]


class ViewImageResult(BaseModel):
    image_data: str = Field(description="Base64-encoded PNG image data")
    content_type: str = "image/png"
    view_name: str
    file_size_bytes: int
