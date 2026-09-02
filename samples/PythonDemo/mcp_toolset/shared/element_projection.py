"""Project element fields into summary DTOs for query tools."""

from collections.abc import Callable

from Autodesk.Revit import DB

from dto.query import ElementSummaryItem
from shared.element_helpers import category_display_name

FieldProjector = Callable[[DB.Document, DB.Element, dict], None]


def project_element_fields(
    doc: DB.Document,
    element: DB.Element,
    fields: list[str],
) -> ElementSummaryItem:
    data: dict[str, object] = {"id": int(element.Id.Value)}
    for field in fields:
        if field not in _FIELD_PROJECTORS:
            continue
        _FIELD_PROJECTORS[field](doc, element, data)
    return ElementSummaryItem.model_validate(data)


def _project_name(_doc: DB.Document, element: DB.Element, data: dict) -> None:
    data["name"] = (element.Name or "")


def _project_category(_doc: DB.Document, element: DB.Element, data: dict) -> None:
    data["category"] = category_display_name(element)


def _project_family(_doc: DB.Document, element: DB.Element, data: dict) -> None:
    data["family"] = (
        element.Symbol.FamilyName if isinstance(element, DB.FamilyInstance) else ""
    )


def _project_type(_doc: DB.Document, element: DB.Element, data: dict) -> None:
    if isinstance(element, DB.FamilyInstance):
        data["type"] = (element.Symbol.Name or "")
    else:
        data["type"] = (element.Name or "")


def _project_level(doc: DB.Document, element: DB.Element, data: dict) -> None:
    if element.LevelId and element.LevelId != DB.ElementId.InvalidElementId:
        level = doc.GetElement(element.LevelId)
        data["level"] = (level.Name or "") if level else ""
    else:
        data["level"] = ""


def _project_workset(doc: DB.Document, element: DB.Element, data: dict) -> None:
    data["workset"] = _workset_name(doc, element)


def _project_bbox(_doc: DB.Document, element: DB.Element, data: dict) -> None:
    bb = element.get_BoundingBox(None) # noqa
    if bb is not None:
        data["bbox"] = {
            "min": [bb.Min.X, bb.Min.Y, bb.Min.Z],
            "max": [bb.Max.X, bb.Max.Y, bb.Max.Z],
        }


def _workset_name(doc: DB.Document, element: DB.Element) -> str:
    try:
        if element.WorksetId == DB.WorksetId.InvalidWorksetId:
            return ""
        return (
            doc.GetWorksetTable().GetWorkset(element.WorksetId).Name
         or "")
    except Exception:
        return ""


_FIELD_PROJECTORS: dict[str, FieldProjector] = {
    "name": _project_name,
    "category": _project_category,
    "family": _project_family,
    "type": _project_type,
    "level": _project_level,
    "workset": _project_workset,
    "bbox": _project_bbox,
}
