# -*- coding: utf-8 -*-
"""Sentinels and column names for collected element rows."""

from __future__ import division, print_function


class Sentinel(object):
    """Missing-value placeholder for a data column."""

    def __init__(self, label, column):
        # type: (str, str) -> None
        self.label = label  # type: str
        self.column = column  # type: str


MISSING_CATEGORY = Sentinel("Unassigned", "category")
MISSING_FAMILY = Sentinel("Unassigned", "family")
MISSING_TYPE = Sentinel("Unassigned", "type")
MISSING_LEVEL = Sentinel("Unassigned", "level")
MISSING_PHASE = Sentinel("Unassigned", "phase")
MISSING_WORKSET = Sentinel("Unassigned", "workset")

ALL_SENTINELS = (
    MISSING_CATEGORY,
    MISSING_FAMILY,
    MISSING_TYPE,
    MISSING_LEVEL,
    MISSING_PHASE,
    MISSING_WORKSET,
)

_SENTINEL_MAP = {}  # type: dict[str, Sentinel]
for _sentinel in ALL_SENTINELS:
    _SENTINEL_MAP[_sentinel.column] = _sentinel


def sentinel_for(column):
    # type: (str) -> Sentinel
    """Return the sentinel for *column*, or raise ``KeyError``."""
    return _SENTINEL_MAP[column]


COLUMN_SCHEMA = {
    "element_id": "str",
    "unique_id": "str",
    "name": "str",
    "class_name": "str",
    "category": "str",
    "family": "str",
    "type": "str",
    "level": "str",
    "phase": "str",
    "workset": "str",
    "is_view_specific": "bool",
    "is_pinned": "bool",
    "has_material_quantities": "bool",
}

FILTERABLE_COLUMNS = (
    "category",
    "family",
    "type",
    "level",
    "phase",
    "workset",
)

# Extra Revit parameter names collected onto each row. Empty means none.
EXTRA_PARAMETERS = []

SCHEMA_VERSION = "2.0.0"
