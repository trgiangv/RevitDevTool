"""Pydantic models for declarative ElementFilter specifications."""
from __future__ import annotations

from typing import Annotated, Literal, Union

from pydantic import BaseModel, Field


class CategoryFilter(BaseModel):
    """Filter elements by one or more Revit category display names."""

    type: Literal["category"] = "category"
    names: list[str] = Field(description="Category display names, e.g. ['Walls', 'Doors', 'Windows']")
    inverted: bool = Field(
        default=False,
        description="If True, *exclude* elements in these categories instead of including them",
    )


class ParameterStringFilter(BaseModel):
    """Filter elements by a string parameter value."""

    type: Literal["parameter_string"] = "parameter_string"
    parameter_name: str = Field(description="Parameter name as it appears in Revit Properties")
    operator: Literal[
        "equals",
        "not_equals",
        "contains",
        "not_contains",
        "begins_with",
        "not_begins_with",
        "ends_with",
        "not_ends_with",
    ] = Field(description="String comparison operator")
    value: str = Field(description="Value to compare against")


class ParameterNumericFilter(BaseModel):
    """Filter elements by a numeric (double/integer) parameter value."""

    type: Literal["parameter_numeric"] = "parameter_numeric"
    parameter_name: str = Field(description="Parameter name as it appears in Revit Properties")
    operator: Literal[
        "equals",
        "not_equals",
        "greater",
        "greater_or_equal",
        "less",
        "less_or_equal",
    ] = Field(description="Numeric comparison operator")
    value: float = Field(description="Numeric value to compare against")
    epsilon: float = Field(
        default=1e-6,
        description="Tolerance for double-precision comparison (only used with equals/not_equals)",
    )


class ParameterHasValueFilter(BaseModel):
    """Filter elements that have (or lack) a value for a specific parameter."""

    type: Literal["parameter_has_value"] = "parameter_has_value"
    parameter_name: str = Field(description="Parameter name to check")
    has_value: bool = Field(
        default=True,
        description="True = keep elements where parameter has a value; False = keep elements where it is empty",
    )


class LevelFilter(BaseModel):
    """Filter elements assigned to a specific level."""

    type: Literal["level"] = "level"
    level_name: str = Field(description="Exact level name, e.g. 'Level 1'")


class ClassFilter(BaseModel):
    """Filter elements by Revit API class name (e.g. Wall, FamilyInstance, Floor)."""

    type: Literal["class"] = "class"
    class_names: list[str] = Field(
        description=(
            "Revit DB class names. Common values: Wall, Floor, Ceiling, "
            "FamilyInstance, RoofBase, Rebar, Group, Room, Area"
        ),
    )


class BoundingBoxFilter(BaseModel):
    """Filter elements whose bounding box intersects a 3D region."""

    type: Literal["bounding_box"] = "bounding_box"
    min_point: list[float] = Field(
        description="[x, y, z] minimum corner in Revit internal units (feet)",
        min_length=3,
        max_length=3,
    )
    max_point: list[float] = Field(
        description="[x, y, z] maximum corner in Revit internal units (feet)",
        min_length=3,
        max_length=3,
    )


class ViewFilter(BaseModel):
    """Filter elements visible in a specific view."""

    type: Literal["view"] = "view"
    view_name: str | None = Field(
        default=None,
        description="View name to scope to. None = active view.",
    )


class ElementTypeFilter(BaseModel):
    """Filter for element types vs element instances."""

    type: Literal["element_type"] = "element_type"
    is_type: bool = Field(
        default=False,
        description="True = return only ElementTypes; False = return only instances (default)",
    )


class PhysicalModelFilter(BaseModel):
    """Built-in filter for physical model elements.

    Excludes system categories, HVAC Zones, Lines, and Detail Components.
    """

    type: Literal["physical_model"] = "physical_model"


class ExclusionFilter(BaseModel):
    """Exclude specific elements by their ElementId."""

    type: Literal["exclusion"] = "exclusion"
    element_ids: list[int] = Field(description="List of ElementId integer values to exclude")


class WorksetFilter(BaseModel):
    """Filter elements belonging to a specific workset (workshared models only)."""

    type: Literal["workset"] = "workset"
    workset_name: str = Field(description="Exact workset name")


FilterSpec = Annotated[
    Union[
        CategoryFilter,
        ParameterStringFilter,
        ParameterNumericFilter,
        ParameterHasValueFilter,
        LevelFilter,
        ClassFilter,
        BoundingBoxFilter,
        ViewFilter,
        ElementTypeFilter,
        PhysicalModelFilter,
        ExclusionFilter,
        WorksetFilter,
    ],
    Field(discriminator="type"),
]


class FilterRequest(BaseModel):
    """Top-level request describing which elements to select.

    Combine multiple filters with AND or OR logic.
    """

    filters: list[FilterSpec] = Field(description="List of filter specifications to apply")
    logic: Literal["and", "or"] = Field(
        default="and",
        description="How to combine filters: 'and' = all must match, 'or' = any must match",
    )


class FilteredExportResult(BaseModel):
    """Result of a filtered element export to Excel."""

    file_path: str = Field(description="Absolute path to the exported file")
    format: str = Field(default="xlsx", description="Export format")
    row_count: int = Field(description="Number of data rows exported")
    column_count: int = Field(description="Number of columns exported")
    filter_summary: str = Field(description="Human-readable summary of the applied filters")
    file_size_bytes: int = Field(description="Size of the exported file in bytes")


class QueryElementsResult(BaseModel):
    """Result of a filtered element query (no file export)."""

    total_elements: int = Field(description="Total number of elements matching the filter")
    by_category: dict[str, int] = Field(description="Element count grouped by category name")
    sample_elements: list[dict] = Field(
        default_factory=list,
        description="First N elements with basic info (ElementId, Name, Category)",
    )
    filter_summary: str = Field(description="Human-readable summary of the applied filters")
