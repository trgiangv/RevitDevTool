"""Element helpers shared by services."""

from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext
from System.Collections.Generic import List

from shared.constants import DEFAULT_UNKNOWN
from shared.responses import ToolError

_EXCLUDED_BUILTIN_CATEGORIES = [
    DB.ElementId(DB.BuiltInCategory.OST_HVAC_Zones),
    DB.ElementId(DB.BuiltInCategory.OST_Lines),
    DB.ElementId(DB.BuiltInCategory.OST_DetailComponents),
]


def require_doc() -> DB.Document:
    doc = RevitContext.ActiveDocument
    if doc is None:
        raise ToolError("No active Revit document")
    return doc # noqa


def get_physical_element_filter(doc: DB.Document) -> DB.ElementMulticategoryFilter:
    """Creates a filter for physical model elements, excluding HVAC Zones,
    Lines, Detail Components, and System categories."""
    category_ids = List[DB.ElementId]()
    for cat in doc.Settings.Categories:
        if cat.CategoryType != DB.CategoryType.Model:
            continue
        if not cat.CanAddSubcategory:
            continue
        if cat.Id in _EXCLUDED_BUILTIN_CATEGORIES:
            continue
        if "System" in str(cat):
            continue
        category_ids.Add(cat.Id)
    return DB.ElementMulticategoryFilter(category_ids)


def find_family_symbol_safely(
    doc: DB.Document,
    target_family_name: str,
    target_type_name: str | None = None,
) -> DB.FamilySymbol | None:
    collector = DB.FilteredElementCollector(doc).OfClass(DB.FamilySymbol)
    for symbol in collector:
        if symbol.Family.Name != target_family_name:
            continue
        if not target_type_name or symbol.Name == target_type_name:
            return symbol
    return None


def find_category_by_name(doc: DB.Document, category_name: str) -> DB.Category | None:
    """Look up a Revit category by display name."""
    target = (category_name or "")
    for cat in doc.Settings.Categories:
        if (cat.Name or "") == target:
            return cat
    return None


def require_category(doc: DB.Document, category_name: str) -> DB.Category:
    """Return a category by name or raise ToolError."""
    category = find_category_by_name(doc, category_name)
    if category is None:
        raise ToolError(f"Category '{category_name}' not found")
    return category


def category_display_name(element: DB.Element) -> str:
    """Return the category name of an element, or 'Unknown'."""
    try:
        if element.Category is not None:
            return element.Category.Name or DEFAULT_UNKNOWN
    except Exception:
        pass
    return DEFAULT_UNKNOWN


def param_value_as_string(
    param: DB.Parameter, doc: DB.Document, default: str = ""
) -> str:
    """Convert a Revit parameter value to a display string."""
    storage = param.StorageType
    if storage == DB.StorageType.String:
        return param.AsString() or default
    if storage == DB.StorageType.Integer:
        return param.AsValueString() or str(param.AsInteger())
    if storage == DB.StorageType.Double:
        return param.AsValueString() or str(round(param.AsDouble(), 6))
    if storage == DB.StorageType.ElementId:
        return element_id_param_display(param, doc, default)
    return param.AsValueString() or default


def element_id_param_display(
    param: DB.Parameter, doc: DB.Document, default: str = ""
) -> str:
    """Resolve an ElementId parameter to the referenced element's name."""
    eid = param.AsElementId()
    if not eid or eid == DB.ElementId.InvalidElementId:
        return default
    ref_elem = doc.GetElement(eid)
    if ref_elem is None:
        return default
    try:
        return ref_elem.Name or default
    except Exception:
        return str(eid)


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
        return param.AsString() or default
    except Exception:
        return default


def element_workset_name(doc: DB.Document, element: DB.Element) -> str | None:
    """Return the element workset name, or None when unavailable."""
    if not doc.IsWorkshared:
        return None
    try:
        workset_id = element.WorksetId
        if workset_id == DB.WorksetId.InvalidWorksetId:
            return None
        workset = doc.GetWorksetTable().GetWorkset(workset_id)
        return workset.Name if workset is not None else None
    except Exception:
        return None
