"""Live model resource queries returning JSON."""

import json

from Autodesk.Revit import DB, UI
from RevitDevTool.Core import RevitContext

from services.content_service import ContentService
from services.query_service import (
    QueryService,
)
from shared.element_helpers import (
    category_display_name,
    element_workset_name,
    require_doc,
)
from shared.responses import ToolError


class ModelResourceService:
    def __init__(self) -> None:
        self._query = QueryService()

    def get_types(self) -> str:
        result = self._query.list_types("family")
        mep = self._query.list_types("mep_system")
        templates = self._query.list_types("view_template")
        title_blocks = self._query.list_types("title_block")
        return _serialize(
            {
                "familyTypes": [t.model_dump() for t in result.types],
                "mepSystemTypes": [t.model_dump() for t in mep.types],
                "viewTemplates": [t.model_dump() for t in templates.types],
                "titleBlocks": [t.model_dump() for t in title_blocks.types],
            }
        )

    @staticmethod
    def get_levels() -> str:
        doc = require_doc()
        views_by_level: dict[int, list] = {}
        for view in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            if view.IsTemplate or view.GenLevel is None:
                continue
            lid = int(view.GenLevel.Id.Value)
            views_by_level.setdefault(lid, []).append(
                {
                    "id": int(view.Id.Value),
                    "name": (view.Name or ""),
                    "viewType": str(view.ViewType),
                }
            )
        levels = []
        for level in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
            lid = int(level.Id.Value)
            levels.append(
                {
                    "id": lid,
                    "name": (level.Name or ""),
                    "elevation": level.Elevation,
                    "associatedViews": views_by_level.get(lid, []),
                }
            )
        levels.sort(key=lambda item: item["elevation"])
        return _serialize({"levels": levels})

    @staticmethod
    def get_views() -> str:
        from services.documentation_service import DocumentationService

        views = DocumentationService().list_views().views
        return _serialize({"views": [v.model_dump(by_alias=True) for v in views]})

    @staticmethod
    def get_worksets() -> str:
        doc = require_doc()
        if not doc.IsWorkshared:
            return _serialize({"worksharingEnabled": False, "worksets": []})
        table = doc.GetWorksetTable()
        active_id = table.GetActiveWorksetId()
        worksets = []
        for workset in DB.FilteredWorksetCollector(doc).ToWorksets():
            count = (
                DB.FilteredElementCollector(doc)
                .WherePasses(DB.ElementWorksetFilter(workset.Id))
                .GetElementCount()
            )
            worksets.append(
                {
                    "id": int(workset.Id.Value),
                    "name": (workset.Name or ""),
                    "kind": str(workset.Kind),
                    "owner": workset.Owner,
                    "isEditable": bool(workset.IsEditable),
                    "isOpen": bool(workset.IsOpen),
                    "isActive": workset.Id == active_id,
                    "elementCount": count,
                }
            )
        return _serialize({"worksharingEnabled": True, "worksets": worksets})

    def get_links(self) -> str:
        links = self._query.list_links().links
        return _serialize({"links": [link.model_dump() for link in links]})

    @staticmethod
    def get_selection() -> str:
        doc = require_doc()
        ui_doc : UI.UIDocument | None = RevitContext.ActiveUiDocument # noqa
        if ui_doc is None:
            return _serialize({"count": 0, "elements": []})

        elements = []
        for eid in ui_doc.Selection.GetElementIds():
            elem = doc.GetElement(eid)
            if elem is not None:
                elements.append(_selection_element_summary(doc, elem))
        return _serialize({"count": len(elements), "elements": elements})

    @staticmethod
    def get_grids() -> str:
        doc = require_doc()
        grids = []
        for grid in DB.FilteredElementCollector(doc).OfClass(DB.Grid).ToElements():
            geometry = None
            curve = grid.Curve
            if isinstance(curve, DB.Line):
                geometry = {
                    "kind": "line",
                    "start": [
                        curve.GetEndPoint(0).X,
                        curve.GetEndPoint(0).Y,
                        curve.GetEndPoint(0).Z,
                    ],
                    "end": [
                        curve.GetEndPoint(1).X,
                        curve.GetEndPoint(1).Y,
                        curve.GetEndPoint(1).Z,
                    ],
                }
            elif curve is not None:
                geometry = {"kind": str(type(curve).__name__)}
            grids.append(
                {
                    "id": int(grid.Id.Value),
                    "name": (grid.Name or ""),
                    "geometry": geometry,
                }
            )
        grids.sort(key=lambda item: item["name"].lower())
        return _serialize({"grids": grids})

    @staticmethod
    def get_element(element_id: int) -> str:
        doc = require_doc()
        element = doc.GetElement(DB.ElementId(element_id))
        if element is None:
            raise ToolError(f"Element {element_id} not found")

        family, type_name = _element_family_info(element)
        category = element.Category.Name if element.Category is not None else ""

        return _serialize(
            {
                "id": int(element.Id.Value),
                "name": element.Name or "",
                "category": category or "",
                "family": family,
                "type": type_name,
                "level": _element_level_name(doc, element),
                "pinned": bool(element.Pinned),
                "workset": element_workset_name(doc, element),
                "boundingBox": _element_bounding_box(element),
            }
        )

    @staticmethod
    def get_schedule_preview(schedule_id: int) -> str:
        preview = ContentService.preview_schedule(schedule_id, max_rows=30)
        return preview.csv_text


def _serialize(data: object) -> str:
    return json.dumps(data, indent=2)


def _selection_element_summary(doc: DB.Document, elem: DB.Element) -> dict:
    family, type_name = _element_family_info(elem)
    return {
        "id": int(elem.Id.Value),
        "name": elem.Name or "",
        "category": category_display_name(elem),
        "family": family or "",
        "type": type_name or (elem.Name or ""),
        "level": _element_level_name(doc, elem) or "",
    }


def _element_family_info(element: DB.Element) -> tuple[str | None, str | None]:
    if not isinstance(element, DB.FamilyInstance):
        return None, None
    return element.Symbol.FamilyName, element.Symbol.Name


def _element_level_name(doc: DB.Document, element: DB.Element) -> str | None:
    if not element.LevelId or element.LevelId == DB.ElementId.InvalidElementId:
        return None
    level = doc.GetElement(element.LevelId)
    return level.Name if isinstance(level, DB.Level) else None


def _element_bounding_box(element: DB.Element) -> dict | None:
    box = element.get_BoundingBox(None) # noqa
    if box is None:
        return None
    return {
        "min": [box.Min.X, box.Min.Y, box.Min.Z],
        "max": [box.Max.X, box.Max.Y, box.Max.Z],
    }
