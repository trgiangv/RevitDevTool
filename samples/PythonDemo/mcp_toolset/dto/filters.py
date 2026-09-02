"""Pydantic models for declarative ElementFilter specifications."""

from typing import Annotated, Literal

from pydantic import BaseModel, Field


class CategoryFilter(BaseModel):
    """Filter elements by one or more Revit category display names."""

    type: Literal["category"] = "category"
    names: list[str] = Field(
        description="Category display names, e.g. ['Walls', 'Doors', 'Windows']"
    )
    inverted: bool = Field(
        default=False,
        description="If True, *exclude* elements in these categories instead of including them",
    )


class ParameterStringFilter(BaseModel):
    """Filter elements by a string parameter value."""

    type: Literal["parameter_string"] = "parameter_string"
    parameter_name: str = Field(
        description="Parameter name as it appears in Revit Properties"
    )
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
    parameter_name: str = Field(
        description="Parameter name as it appears in Revit Properties"
    )
    operator: Literal[
        "equals",
        "not_equals",
        "greater_than",
        "greater_or_equal",
        "less_than",
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
    """Filter elements whose bounding box matches a 3D region."""

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
    mode: Literal["inside", "intersecting"] = Field(
        default="inside",
        description="Spatial mode: inside (default) or intersecting",
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


class PhaseFilter(BaseModel):
    """Filter elements associated with a specific phase."""

    type: Literal["phase"] = "phase"
    phase_name: str = Field(description="Exact phase name, e.g. 'New Construction'")


class ExclusionFilter(BaseModel):
    """Exclude specific elements by their ElementId."""

    type: Literal["exclusion"] = "exclusion"
    element_ids: list[int] = Field(
        description="List of ElementId integer values to exclude"
    )


class WorksetFilter(BaseModel):
    """Filter elements belonging to a specific workset (workshared models only)."""

    type: Literal["workset"] = "workset"
    workset_name: str = Field(description="Exact workset name")


FilterItem = Annotated[
    CategoryFilter
    | ParameterStringFilter
    | ParameterNumericFilter
    | ParameterHasValueFilter
    | LevelFilter
    | ClassFilter
    | BoundingBoxFilter
    | ViewFilter
    | ElementTypeFilter
    | PhaseFilter
    | ExclusionFilter
    | WorksetFilter,
    Field(discriminator="type"),
]


class FilterSpec(BaseModel):
    """Top-level request describing which elements to select.

    Combine multiple filters with AND or OR logic.
    """

    filters: list[FilterItem] = Field(
        description="List of filter specifications to apply"
    )
    logic: Literal["and", "or"] = Field(
        default="and",
        description="How to combine filters: 'and' = all must match, 'or' = any must match",
    )
