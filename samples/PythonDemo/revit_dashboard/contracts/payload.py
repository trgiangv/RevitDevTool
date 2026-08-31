"""Typed payload contracts for the dashboard backend/frontend bridge."""

from typing import NotRequired, TypedDict


class DashboardFilterState(TypedDict, total=False):
    """Filter state shared between Python analytics and the React frontend.

    Keys match :pydata:`~revit_dashboard.constants.FILTERABLE_COLUMNS` with an
    ``"s"`` suffix (e.g. ``"categories"`` for the ``"category"`` column).
    """

    categories: list[str]
    families: list[str]
    types: list[str]
    levels: list[str]
    phases: list[str]
    worksets: list[str]
    search: str
    hide_categories: list[str]
    hide_levels: list[str]


class ChartConfig(TypedDict):
    """Describes a single chart that the frontend should render."""

    type: str  # "bar" | "treemap" | "pie" …
    title: str
    data_key: str  # key inside ``charts`` dict
    label_field: str
    value_field: str
    max_items: NotRequired[int]
    click_filter_field: NotRequired[str]


class ModelInfo(TypedDict):
    """Model metadata displayed in the top bar breadcrumb."""

    file_name: str
    file_path: str
    current_view: str
    total_views: int
    total_sheets: int


class WarningItem(TypedDict):
    """A single Revit warning classified by severity."""

    id: int
    description: str
    severity: str  # "critical" | "moderate" | "info"
    element_ids: list[int]
    category: str


class HeavyFamily(TypedDict):
    """A family ranked by estimated geometry complexity."""

    family_name: str
    category: str
    instance_count: int
    type_count: int
    estimated_complexity: int


class DashboardPayload(TypedDict):
    """Top-level JSON payload injected into the WebView2 React app."""

    schema_version: str
    generated_at_utc: str
    model_info: ModelInfo
    kpis: dict
    filter_options: dict[str, list[str]]
    filterable_columns: list[str]
    chart_configs: list[ChartConfig]
    charts: dict
    rows: list[dict]
    columns: list[str]
    warnings: list[WarningItem]
    heavy_families: list[HeavyFamily]
    active_filters: NotRequired[DashboardFilterState]
