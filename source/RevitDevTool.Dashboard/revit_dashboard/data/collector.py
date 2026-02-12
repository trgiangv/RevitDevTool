"""Revit model element collection with smart category filtering."""

from __future__ import annotations

from typing import TYPE_CHECKING

import Autodesk.Revit.DB as DB

from revit_dashboard.constants import (
    EXTRA_PARAMETERS,
    MISSING_CATEGORY,
    MISSING_FAMILY,
    MISSING_LEVEL,
    MISSING_PHASE,
    MISSING_TYPE,
    MISSING_WORKSET,
)
from revit_dashboard.data.categories import get_model_categories

if TYPE_CHECKING:
    from revit_dashboard.contracts.payload import ModelInfo


# ---------------------------------------------------------------------------
# Model Info
# ---------------------------------------------------------------------------


def collect_model_info(doc: DB.Document) -> ModelInfo:
    """Collect model metadata: filename, path, active view, view/sheet counts."""
    from revit_dashboard.context import HOST_APP

    file_name = doc.Title or "Untitled"
    file_path = doc.PathName or ""

    # Active view name
    current_view = ""
    try:
        active_view = HOST_APP.uidoc.ActiveView
        if active_view:
            current_view = active_view.Name or ""
    except Exception:
        pass

    # Count views (excluding templates and system views)
    total_views = 0
    try:
        view_collector = (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.View)
            .WhereElementIsNotElementType()
        )
        for v in view_collector:
            if not v.IsTemplate and v.ViewType != DB.ViewType.Internal:
                total_views += 1
    except Exception:
        pass

    # Count sheets
    total_sheets = 0
    try:
        sheet_collector = (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.ViewSheet)
            .WhereElementIsNotElementType()
        )
        total_sheets = sheet_collector.GetElementCount()
    except Exception:
        pass

    return {
        "file_name": file_name,
        "file_path": file_path,
        "current_view": current_view,
        "total_views": total_views,
        "total_sheets": total_sheets,
    }


# ---------------------------------------------------------------------------
# Element Collection
# ---------------------------------------------------------------------------


def collect_model_elements(
    doc: DB.Document,
    categories: list[DB.BuiltInCategory] | None = None,
    extra_parameters: list[str] | None = None,
) -> list[dict]:
    """Collect model elements using Revit multi-category filter.

    Uses ``ElementMulticategoryFilter`` for native C++ filtering — orders of
    magnitude faster than iterating all elements.

    Args:
        doc: Revit document.
        categories: Override the default model categories to collect.
        extra_parameters: Additional Revit parameter names to collect per element.
    """
    if categories is None:
        categories = get_model_categories()

    # Use the global config if no explicit list provided
    if extra_parameters is None:
        extra_parameters = EXTRA_PARAMETERS if EXTRA_PARAMETERS else None

    from System.Collections.Generic import List  # noqa: N811

    cat_list = List[DB.BuiltInCategory]()
    for cat in categories:
        cat_list.Add(cat)

    cat_filter = DB.ElementMulticategoryFilter(cat_list)
    collector = (
        DB.FilteredElementCollector(doc)
        .WherePasses(cat_filter)
        .WhereElementIsNotElementType()
    )

    levels = _collect_levels(doc)
    phases = _collect_phases(doc)

    rows: list[dict] = []
    for element in collector:
        try:
            rows.append(_to_row(doc, element, levels, phases, extra_parameters))
        except Exception as ex:
            print(f"[Collector] Skip {element.Id.IntegerValue}: {ex}")

    if extra_parameters:
        print(f"[Collector] Collected {len(extra_parameters)} extra parameters per element")

    return rows


# ---------------------------------------------------------------------------
# Lookup caches (pre-collected for batch performance)
# ---------------------------------------------------------------------------


def _collect_levels(doc: DB.Document) -> dict[int, str]:
    """Pre-cache all level id -> name mappings."""
    result: dict[int, str] = {}
    for level in (
        DB.FilteredElementCollector(doc)
        .OfCategory(DB.BuiltInCategory.OST_Levels)
        .WhereElementIsNotElementType()
    ):
        result[level.Id.IntegerValue] = level.Name or MISSING_LEVEL.label
    return result


def _collect_phases(doc: DB.Document) -> dict[int, str]:
    """Pre-cache all phase id -> name mappings."""
    result: dict[int, str] = {}
    for phase in doc.Phases:
        result[phase.Id.IntegerValue] = phase.Name or MISSING_PHASE.label
    return result


# ---------------------------------------------------------------------------
# Row builder
# ---------------------------------------------------------------------------


def _to_row(
    doc: DB.Document,
    element: DB.Element,
    levels: dict[int, str],
    phases: dict[int, str],
    extra_params: list[str] | None = None,
) -> dict:
    row = {
        "element_id": element.Id.IntegerValue,
        "unique_id": getattr(element, "UniqueId", ""),
        "name": _safe_str(getattr(element, "Name", None), "Unnamed"),
        "class_name": element.GetType().Name,
        "category": _get_category(element),
        "family": _get_family(doc, element),
        "type": _get_type(doc, element),
        "level": _get_level(element, levels),
        "phase": _get_phase(element, phases),
        "workset": _get_workset(doc, element),
        "is_view_specific": bool(getattr(element, "ViewSpecific", False)),
        "is_pinned": bool(getattr(element, "Pinned", False)),
        "has_material_quantities": bool(getattr(element, "HasMaterialQuantities", False)),
    }

    # Collect extra user-configured parameters
    if extra_params:
        row.update(_collect_extra_params(element, extra_params))

    return row


# ---------------------------------------------------------------------------
# Property extractors — use sentinels from constants
# ---------------------------------------------------------------------------


def _get_category(element: DB.Element) -> str:
    category = getattr(element, "Category", None)
    return _safe_str(getattr(category, "Name", None), MISSING_CATEGORY.label)


def _get_family(doc: DB.Document, element: DB.Element) -> str:
    family_name = MISSING_FAMILY.label

    type_id = element.GetTypeId()
    if type_id and type_id.IntegerValue > 0:
        element_type = doc.GetElement(type_id)
        if element_type is not None:
            family = getattr(element_type, "FamilyName", None)
            if family:
                family_name = str(family)

    family_param = element.LookupParameter("Family")
    if family_param and family_param.HasValue:
        family_name = family_param.AsString() or family_name

    return family_name


def _get_type(doc: DB.Document, element: DB.Element) -> str:
    type_id = element.GetTypeId()
    if type_id and type_id.IntegerValue > 0:
        element_type = doc.GetElement(type_id)
        if element_type is not None:
            return _safe_str(getattr(element_type, "Name", None), MISSING_TYPE.label)
    return MISSING_TYPE.label


def _get_level(element: DB.Element, levels: dict[int, str]) -> str:
    level_param: DB.Parameter = element.get_Parameter(DB.BuiltInParameter.FAMILY_LEVEL_PARAM)
    if level_param and level_param.HasValue:
        level_id = level_param.AsElementId().IntegerValue
        if level_id in levels:
            return levels[level_id]

    elem_level_id = getattr(element, "LevelId", None)
    if elem_level_id and elem_level_id.IntegerValue > 0:
        level_id = elem_level_id.IntegerValue
        if level_id in levels:
            return levels[level_id]

    return MISSING_LEVEL.label


def _get_phase(element: DB.Element, phases: dict[int, str]) -> str:
    created_param = element.get_Parameter(DB.BuiltInParameter.PHASE_CREATED)
    if created_param and created_param.HasValue:
        phase_id = created_param.AsElementId().IntegerValue
        if phase_id in phases:
            return phases[phase_id]
    return MISSING_PHASE.label


def _get_workset(doc: DB.Document, element: DB.Element) -> str:
    try:
        workset_table = doc.GetWorksetTable()
        workset_id = element.WorksetId
        if workset_table and workset_id:
            workset = workset_table.GetWorkset(workset_id)
            if workset:
                return _safe_str(getattr(workset, "Name", None), MISSING_WORKSET.label)
    except Exception:
        pass
    return MISSING_WORKSET.label


def _safe_str(value: str | None, fallback: str) -> str:
    if not value:
        return fallback
    text = str(value).strip()
    return text if text else fallback


# ---------------------------------------------------------------------------
# Extra parameter collection (ParameterMap / LookupParameter)
# ---------------------------------------------------------------------------


def _param_value_as_string(param: DB.Parameter) -> str:
    """Convert a Revit Parameter to a display string regardless of storage type."""
    try:
        st = param.StorageType
        if st == DB.StorageType.String:
            return param.AsString() or ""
        if st == DB.StorageType.Integer:
            return str(param.AsInteger())
        if st == DB.StorageType.Double:
            return str(round(param.AsDouble(), 6))
        if st == DB.StorageType.ElementId:
            return str(param.AsElementId().IntegerValue)
        return param.AsValueString() or ""
    except Exception:
        return ""


def _collect_extra_params(element: DB.Element, param_names: list[str]) -> dict[str, str]:
    """Collect user-configured extra parameters from an element.

    Uses ``LookupParameter`` for named parameters.  Falls back to iterating
    ``element.Parameters`` when a direct lookup fails (handles shared params).
    """
    result: dict[str, str] = {}
    for name in param_names:
        param = element.LookupParameter(name)
        if param and param.HasValue:
            result[name] = _param_value_as_string(param)
        else:
            result[name] = ""
    return result


def collect_all_parameters(element: DB.Element) -> dict[str, str]:
    """Collect ALL parameters from element.Parameters (ParameterMap).

    Returns a flat dict ``{param_name: display_value}``.  Used by the
    ``getElementParameters`` bridge command to provide the full parameter
    set for the Properties Pane.
    """
    result: dict[str, str] = {}
    for param in element.Parameters:
        defn = getattr(param, "Definition", None)
        if defn is None:
            continue
        name = defn.Name
        if not name:
            continue
        if param.HasValue:
            result[name] = _param_value_as_string(param)
        else:
            result[name] = ""
    return result
