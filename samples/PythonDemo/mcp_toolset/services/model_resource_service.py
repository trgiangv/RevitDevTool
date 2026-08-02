"""Live model resource queries returning JSON."""
from __future__ import annotations

import json

from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

from services.content_service import ContentService
from services.query_service import QueryService, _external_path, _import_path, _is_revit_link_loaded
from shared.element_helpers import category_display_name, element_id_value, normalize_string, require_doc
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

    def get_levels(self) -> str:
        doc = require_doc()
        views_by_level: dict[int, list] = {}
        for view in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            if view.IsTemplate or view.GenLevel is None:
                continue
            lid = element_id_value(view.GenLevel.Id)
            views_by_level.setdefault(lid, []).append(
                {"id": element_id_value(view.Id), "name": normalize_string(view.Name), "viewType": str(view.ViewType)}
            )
        levels = []
        for level in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
            lid = element_id_value(level.Id)
            levels.append(
                {
                    "id": lid,
                    "name": normalize_string(level.Name),
                    "elevation": level.Elevation,
                    "associatedViews": views_by_level.get(lid, []),
                }
            )
        levels.sort(key=lambda item: item["elevation"])
        return _serialize({"levels": levels})

    def get_views(self) -> str:
        from services.documentation_service import DocumentationService

        views = DocumentationService().list_views().views
        return _serialize({"views": [v.model_dump(by_alias=True) for v in views]})

    def get_worksets(self) -> str:
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
                    "id": int(workset.Id.IntegerValue),
                    "name": normalize_string(workset.Name),
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
        return _serialize({"links": [l.model_dump() for l in links]})

    def get_selection(self) -> str:
        doc = require_doc()
        ui_doc = RevitContext.ActiveUiDocument
        if ui_doc is None:
            return _serialize({"count": 0, "elements": []})
        elements = []
        for eid in ui_doc.Selection.GetElementIds():
            elem = doc.GetElement(eid)
            if elem is None:
                continue
            family = ""
            type_name = ""
            if isinstance(elem, DB.FamilyInstance):
                family = elem.Symbol.FamilyName
                type_name = elem.Symbol.Name
            level = ""
            if elem.LevelId and elem.LevelId != DB.ElementId.InvalidElementId:
                lvl = doc.GetElement(elem.LevelId)
                if lvl:
                    level = normalize_string(lvl.Name)
            elements.append(
                {
                    "id": element_id_value(elem.Id),
                    "name": normalize_string(elem.Name),
                    "category": category_display_name(elem),
                    "family": family,
                    "type": type_name or normalize_string(elem.Name),
                    "level": level,
                }
            )
        return _serialize({"count": len(elements), "elements": elements})

    def get_grids(self) -> str:
        doc = require_doc()
        grids = []
        for grid in DB.FilteredElementCollector(doc).OfClass(DB.Grid).ToElements():
            geometry = None
            curve = grid.Curve
            if isinstance(curve, DB.Line):
                geometry = {
                    "kind": "line",
                    "start": [curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, curve.GetEndPoint(0).Z],
                    "end": [curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, curve.GetEndPoint(1).Z],
                }
            elif curve is not None:
                geometry = {"kind": str(type(curve).__name__)}
            grids.append({"id": element_id_value(grid.Id), "name": normalize_string(grid.Name), "geometry": geometry})
        grids.sort(key=lambda item: item["name"].lower())
        return _serialize({"grids": grids})

    def get_element(self, element_id: int) -> str:
        doc = require_doc()
        element = doc.GetElement(DB.ElementId(element_id))
        if element is None:
            raise ToolError("Element {} not found".format(element_id))

        family: str | None = None
        type_name: str | None = None
        if isinstance(element, DB.FamilyInstance):
            family = element.Symbol.FamilyName
            type_name = element.Symbol.Name

        level_name: str | None = None
        if element.LevelId and element.LevelId != DB.ElementId.InvalidElementId:
            level = doc.GetElement(element.LevelId)
            if isinstance(level, DB.Level):
                level_name = level.Name

        workset_name: str | None = None
        if doc.IsWorkshared:
            try:
                workset_table = doc.GetWorksetTable()
                workset_id = workset_table.GetWorksetId(element.Id)
                workset = workset_table.GetWorkset(workset_id)
                if workset is not None:
                    workset_name = workset.Name
            except Exception:
                workset_name = None

        bounding_box: dict | None = None
        box = element.get_BoundingBox(None)
        if box is not None:
            bounding_box = {
                "min": [box.Min.X, box.Min.Y, box.Min.Z],
                "max": [box.Max.X, box.Max.Y, box.Max.Z],
            }

        category = ""
        if element.Category is not None:
            category = element.Category.Name or ""

        return _serialize(
            {
                "id": element_id_value(element.Id),
                "name": element.Name or "",
                "category": category,
                "family": family,
                "type": type_name,
                "level": level_name,
                "pinned": bool(element.Pinned),
                "workset": workset_name,
                "boundingBox": bounding_box,
            }
        )

    def get_schedule_preview(self, schedule_id: int) -> str:
        _, csv_text, _, _, _ = ContentService().preview_schedule(schedule_id, max_rows=30)
        return csv_text


def _serialize(data: object) -> str:
    return json.dumps(data, indent=2)
