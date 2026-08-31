"""Single source of truth for schema, sentinel values, and configuration.

Every module references these constants — zero hardcoded strings in business logic.
"""

from dataclasses import dataclass

import polars as pl


# ---------------------------------------------------------------------------
# Sentinel values for missing data
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class Sentinel:
    """Missing-value placeholder for a data column."""

    label: str
    column: str


MISSING_CATEGORY = Sentinel("Unassigned", "category")
MISSING_FAMILY = Sentinel("Unassigned", "family")
MISSING_TYPE = Sentinel("Unassigned", "type")
MISSING_LEVEL = Sentinel("Unassigned", "level")
MISSING_PHASE = Sentinel("Unassigned", "phase")
MISSING_WORKSET = Sentinel("Unassigned", "workset")

ALL_SENTINELS: tuple[Sentinel, ...] = (
    MISSING_CATEGORY,
    MISSING_FAMILY,
    MISSING_TYPE,
    MISSING_LEVEL,
    MISSING_PHASE,
    MISSING_WORKSET,
)

_SENTINEL_MAP: dict[str, Sentinel] = {s.column: s for s in ALL_SENTINELS}


def sentinel_for(column: str) -> Sentinel:
    """Return the sentinel for *column*, or raise ``KeyError``."""
    return _SENTINEL_MAP[column]


# ---------------------------------------------------------------------------
# Column schema (drives DataFrame creation, frontend types, table columns)
# ---------------------------------------------------------------------------

COLUMN_SCHEMA: dict[str, pl.DataType] = {
    "element_id": pl.Int64,
    "unique_id": pl.String,
    "name": pl.String,
    "class_name": pl.String,
    "category": pl.String,
    "family": pl.String,
    "type": pl.String,
    "level": pl.String,
    "phase": pl.String,
    "workset": pl.String,
    "is_view_specific": pl.Boolean,
    "is_pinned": pl.Boolean,
    "has_material_quantities": pl.Boolean,
}

# ---------------------------------------------------------------------------
# Filterable columns — drives Python filter logic AND frontend FilterSidebar
# ---------------------------------------------------------------------------

FILTERABLE_COLUMNS: tuple[str, ...] = (
    "category",
    "family",
    "type",
    "level",
    "phase",
    "workset",
)

# ---------------------------------------------------------------------------
# Extra parameters — user-configurable list of Revit parameter names to collect
# ---------------------------------------------------------------------------

EXTRA_PARAMETERS: list[str] = [
    # Add parameter names here to include them as extra columns, e.g.:
    # "Mark",
    # "Comments",
    # "Area",
    # "Volume",
]

# ---------------------------------------------------------------------------
# Payload schema version
# ---------------------------------------------------------------------------

SCHEMA_VERSION = "2.0.0"
