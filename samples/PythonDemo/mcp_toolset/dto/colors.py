from __future__ import annotations
from pydantic import BaseModel, Field


class ParameterInfo(BaseModel):
    name: str
    storage_type: str
    has_value: bool
    sample_value: str


class CategoryParametersResult(BaseModel):
    category: str
    parameter_count: int
    parameters: list[ParameterInfo]


class ColorAssignment(BaseModel):
    color: str = Field(description="Hex color code, e.g. '#FF0000'")
    element_count: int


class ColorSplashStatistics(BaseModel):
    total_elements: int
    elements_colored: int
    unique_parameter_values: int
    use_gradient: bool


class ColorSplashResult(BaseModel):
    category: str
    parameter: str
    color_assignments: dict[str, ColorAssignment]
    statistics: ColorSplashStatistics


class ClearColorsResult(BaseModel):
    category: str
    elements_processed: int
