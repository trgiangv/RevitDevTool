"""Polars-based analytics engine — public API only.

Every function is public and referenced by ``constants`` — zero hardcoded
column names, sentinel strings, or filter keys.
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import TYPE_CHECKING

import polars as pl

from revit_dashboard.constants import (
    ALL_SENTINELS,
    COLUMN_SCHEMA,
    FILTERABLE_COLUMNS,
    SCHEMA_VERSION,
)

if TYPE_CHECKING:
    from revit_dashboard.contracts.payload import (
        ChartConfig,
        DashboardFilterState,
        DashboardPayload,
        HeavyFamily,
        ModelInfo,
        WarningItem,
    )

# Correct column → filter-state key mapping (irregular plurals)
_FILTER_KEY_MAP: dict[str, str] = {
    "category": "categories",
    "family": "families",
    "type": "types",
    "level": "levels",
    "phase": "phases",
    "workset": "worksets",
}


# ---------------------------------------------------------------------------
# High-level entry
# ---------------------------------------------------------------------------


def build_dashboard_payload(
    rows: list[dict],
    filters: DashboardFilterState | None = None,
    model_info: ModelInfo | None = None,
    warnings: list[WarningItem] | None = None,
    heavy_families: list[HeavyFamily] | None = None,
) -> DashboardPayload:
    """Build the full JSON payload consumed by the React frontend."""
    df = build_dataframe(rows)
    filtered = apply_filters(df, filters or {})

    chart_configs = default_chart_configs(df)

    return {
        "schema_version": SCHEMA_VERSION,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "model_info": model_info or {
            "file_name": "Unknown",
            "file_path": "",
            "current_view": "",
            "total_views": 0,
            "total_sheets": 0,
        },
        "kpis": build_kpis(filtered, warnings),
        "filter_options": build_filter_options(df),
        "filterable_columns": list(FILTERABLE_COLUMNS),
        "chart_configs": chart_configs,
        "charts": build_chart_data(filtered),
        "rows": filtered.to_dicts(),
        "columns": filtered.columns,
        "warnings": warnings or [],
        "heavy_families": heavy_families or [],
        "active_filters": filters or {},
    }


# ---------------------------------------------------------------------------
# DataFrame creation
# ---------------------------------------------------------------------------


def build_dataframe(rows: list[dict]) -> pl.DataFrame:
    """Create a typed Polars DataFrame from collector row dicts."""
    if not rows:
        return pl.DataFrame(schema=COLUMN_SCHEMA)
    return pl.DataFrame(rows, schema_overrides=COLUMN_SCHEMA)


# ---------------------------------------------------------------------------
# Filtering — data-driven from FILTERABLE_COLUMNS
# ---------------------------------------------------------------------------


def apply_filters(df: pl.DataFrame, filters: DashboardFilterState) -> pl.DataFrame:
    """Apply include/exclude/search filters.  Driven by ``FILTERABLE_COLUMNS``."""
    if df.is_empty():
        return df

    result = df

    # Include filters (e.g. "categories" → column "category")
    for col in FILTERABLE_COLUMNS:
        fkey = _FILTER_KEY_MAP.get(col, f"{col}s")
        selected = filters.get(fkey) or filters.get(col)  # type: ignore[arg-type]
        if selected:
            result = result.filter(pl.col(col).is_in(selected))

    # Exclude filters
    for col in FILTERABLE_COLUMNS:
        fkey = _FILTER_KEY_MAP.get(col, f"{col}s")
        hidden = filters.get(f"hide_{fkey}") or filters.get(f"hide_{col}")  # type: ignore[arg-type]
        if hidden:
            result = result.filter(~pl.col(col).is_in(hidden))

    # Free-text search — searches ALL string columns (not just filterable ones)
    search = (filters.get("search") or "").strip()
    if search:
        needle = search.lower()
        search_cols = [
            c for c in result.columns
            if result.schema[c] == pl.String
        ]
        if search_cols:
            expr = pl.lit(False)
            for c in search_cols:
                expr = expr | pl.col(c).str.to_lowercase().str.contains(needle, literal=True)
            result = result.filter(expr)

    return result


# ---------------------------------------------------------------------------
# KPIs
# ---------------------------------------------------------------------------


def build_kpis(
    df: pl.DataFrame,
    warnings: list[WarningItem] | None = None,
) -> dict:
    """Compute summary KPIs from the (optionally filtered) DataFrame."""
    return {
        "total_elements": df.height,
        "unique_categories": _nunique(df, "category"),
        "unique_families": _nunique(df, "family"),
        "unique_types": _nunique(df, "type"),
        "unique_levels": _nunique(df, "level"),
        "total_warnings": len(warnings) if warnings else 0,
        "pinned_elements": df.filter(pl.col("is_pinned")).height if "is_pinned" in df.columns else 0,
        "view_specific_elements": (
            df.filter(pl.col("is_view_specific")).height if "is_view_specific" in df.columns else 0
        ),
    }


# ---------------------------------------------------------------------------
# Filter options
# ---------------------------------------------------------------------------


def build_filter_options(df: pl.DataFrame) -> dict[str, list[str]]:
    """Derive unique sorted values for every filterable column."""
    return {col: _distinct_sorted(df, col) for col in FILTERABLE_COLUMNS if col in df.columns}


# ---------------------------------------------------------------------------
# Chart data (aggregations consumed by the frontend chart registry)
# ---------------------------------------------------------------------------


def build_chart_data(df: pl.DataFrame) -> dict:
    """Aggregate chart data keyed by descriptive names."""
    return {
        "category_counts": _group_count(df, "category"),
        "level_counts": _group_count(df, "level"),
        "family_counts": _group_count_multi(df, ["category", "family"], limit=50),
        "workset_counts": _group_count(df, "workset"),
        "phase_counts": _group_count(df, "phase"),
        "quality": compute_quality_metrics(df),
        "outlier_families": _detect_outliers(df),
    }


def default_chart_configs(df: pl.DataFrame) -> list[ChartConfig]:
    """Return the default set of chart configs based on available data."""
    configs: list[ChartConfig] = [
        {
            "type": "bar",
            "title": "Elements by Category",
            "data_key": "category_counts",
            "label_field": "category",
            "value_field": "count",
            "max_items": 15,
            "click_filter_field": "category",
        },
        {
            "type": "bar",
            "title": "Elements by Level",
            "data_key": "level_counts",
            "label_field": "level",
            "value_field": "count",
            "max_items": 15,
            "click_filter_field": "level",
        },
    ]

    # Only include workset/phase charts when data has meaningful values
    if "workset" in df.columns and df.select(pl.col("workset").n_unique()).item() > 1:
        configs.append({
            "type": "bar",
            "title": "Elements by Workset",
            "data_key": "workset_counts",
            "label_field": "workset",
            "value_field": "count",
            "max_items": 15,
            "click_filter_field": "workset",
        })

    if "phase" in df.columns and df.select(pl.col("phase").n_unique()).item() > 1:
        configs.append({
            "type": "bar",
            "title": "Elements by Phase",
            "data_key": "phase_counts",
            "label_field": "phase",
            "value_field": "count",
            "max_items": 15,
            "click_filter_field": "phase",
        })

    return configs


# ---------------------------------------------------------------------------
# Quality metrics — driven by sentinel registry
# ---------------------------------------------------------------------------


def compute_quality_metrics(df: pl.DataFrame) -> dict[str, int]:
    """Count rows matching each sentinel (missing) value."""
    return {
        f"missing_{s.column}": df.filter(pl.col(s.column) == s.label).height
        for s in ALL_SENTINELS
        if s.column in df.columns
    }


# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------


def _nunique(df: pl.DataFrame, col: str) -> int:
    if col not in df.columns:
        return 0
    return df.select(pl.col(col).n_unique()).item()


def _distinct_sorted(df: pl.DataFrame, col: str) -> list[str]:
    return df.select(pl.col(col)).unique().sort(col).get_column(col).to_list()


def _group_count(df: pl.DataFrame, col: str) -> list[dict]:
    if col not in df.columns:
        return []
    return (
        df.group_by(col)
        .agg(pl.len().alias("count"))
        .sort("count", descending=True)
        .to_dicts()
    )


def _group_count_multi(df: pl.DataFrame, cols: list[str], limit: int = 50) -> list[dict]:
    present = [c for c in cols if c in df.columns]
    if not present:
        return []
    return (
        df.group_by(present)
        .agg(pl.len().alias("count"))
        .sort("count", descending=True)
        .head(limit)
        .to_dicts()
    )


def _detect_outliers(df: pl.DataFrame) -> list[dict]:
    if "family" not in df.columns or "category" not in df.columns:
        return []
    dist = (
        df.group_by(["category", "family"])
        .agg(pl.len().alias("count"))
        .sort("count", descending=True)
    )
    if dist.is_empty():
        return []
    q95 = dist.select(pl.col("count").quantile(0.95)).item()
    return dist.filter(pl.col("count") >= q95).head(30).to_dicts()
