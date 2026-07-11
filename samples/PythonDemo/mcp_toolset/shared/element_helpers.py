"""Element helpers shared by services."""
from __future__ import annotations

from System.Collections.Generic import List
from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

from shared.constants import DEFAULT_UNNAMED, DEFAULT_UNKNOWN
from shared.responses import ToolError

_EXCLUDED_BUILTIN_CATEGORIES = {
    DB.BuiltInCategory.OST_HVAC_Zones,
    DB.BuiltInCategory.OST_Lines,
    DB.BuiltInCategory.OST_DetailComponents,
}


def require_doc() -> DB.Document:
    doc = RevitContext.ActiveDocument
    if doc is None:
        raise ToolError("No active Revit document")
    return doc


def normalize_string(text: str | bytes | None) -> str:
    if text is None:
        return DEFAULT_UNNAMED
    if isinstance(text, bytes):
        try:
            return text.decode("utf-8").strip()
        except UnicodeDecodeError:
            return text.decode("latin-1", errors="replace").strip()
    try:
        return str(text).strip()
    except Exception:
        return DEFAULT_UNNAMED


def get_physical_element_filter(doc: DB.Document) -> DB.ElementMulticategoryFilter:
    """Creates a filter for physical model elements, excluding HVAC Zones,
    Lines, Detail Components, and System categories."""
    excluded_ids = {DB.ElementId(cat) for cat in _EXCLUDED_BUILTIN_CATEGORIES}
    category_ids = List[DB.ElementId]()
    for cat in doc.Settings.Categories:
        if cat.CategoryType != DB.CategoryType.Model:
            continue
        if not cat.CanAddSubcategory:
            continue
        if cat.Id in excluded_ids:
            continue
        builtin_name = DB.BuiltInCategory(element_id_value(cat.Id)).ToString()
        if "System" in builtin_name:
            continue
        category_ids.Add(cat.Id)
    return DB.ElementMulticategoryFilter(category_ids)


def element_id_value(element_id: DB.ElementId) -> int:
    try:
        return int(element_id.Value)
    except AttributeError:
        return int(element_id.IntegerValue)


def find_family_symbol_safely(
    doc: DB.Document, target_family_name: str, target_type_name: str | None = None,
) -> DB.FamilySymbol | None:
    collector = DB.FilteredElementCollector(doc).OfClass(DB.FamilySymbol)
    for symbol in collector:
        if symbol.Family.Name != target_family_name:
            continue
        if not target_type_name or symbol.Name == target_type_name:
            return symbol
    return None


def find_category_by_name(doc: DB.Document, category_name: str) -> DB.Category | None:
    """Look up a Revit category by display name (case-insensitive after normalization)."""
    target = normalize_string(category_name)
    for cat in doc.Settings.Categories:
        if normalize_string(cat.Name) == target:
            return cat
    return None


def category_display_name(element: DB.Element) -> str:
    """Return the normalized category name of an element, or 'Unknown'."""
    try:
        if element.Category is not None:
            return normalize_string(element.Category.Name)
    except Exception:
        pass
    return DEFAULT_UNKNOWN


def param_value_as_string(param: DB.Parameter, doc: DB.Document, default: str = "") -> str:
    """Convert a Revit parameter value to a display string."""
    storage = param.StorageType
    if storage == DB.StorageType.String:
        return normalize_string(param.AsString()) if param.AsString() else default
    if storage == DB.StorageType.Integer:
        return param.AsValueString() or str(param.AsInteger())
    if storage == DB.StorageType.Double:
        return param.AsValueString() or str(round(param.AsDouble(), 6))
    if storage == DB.StorageType.ElementId:
        return element_id_param_display(param, doc, default)
    return param.AsValueString() or default


def element_id_param_display(param: DB.Parameter, doc: DB.Document, default: str = "") -> str:
    """Resolve an ElementId parameter to the referenced element's name."""
    eid = param.AsElementId()
    if not eid or eid == DB.ElementId.InvalidElementId:
        return default
    ref_elem = doc.GetElement(eid)
    if ref_elem is None:
        return default
    try:
        return normalize_string(ref_elem.Name)
    except Exception:
        return str(element_id_value(eid))


def require_active_view(doc: DB.Document) -> DB.View:
    """Return the active view or raise ToolError."""
    view = doc.ActiveView
    if view is None:
        raise ToolError("No active view found")
    return view


def get_param_string(elem: DB.Element, param_name: str, default: str = "") -> str:
    """Look up a string parameter by name with a safe fallback."""
    param = elem.LookupParameter(param_name)
    if param is None or not param.HasValue:
        return default
    try:
        return normalize_string(param.AsString() or default)
    except Exception:
        return default
