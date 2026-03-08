"""Service for model-level information and hierarchy."""

from __future__ import annotations

from shared.constants import DEFAULT_NOT_SET
from shared.context import get_doc
from shared.element_helpers import element_id_value, normalize_string
from shared.responses import ToolError


class ModelService:
    _element_categories = {
        "Walls": "OST_Walls",
        "Floors": "OST_Floors",
        "Ceilings": "OST_Ceilings",
        "Roofs": "OST_Roofs",
        "Doors": "OST_Doors",
        "Windows": "OST_Windows",
        "Stairs": "OST_Stairs",
        "Railings": "OST_Railings",
        "Columns": "OST_Columns",
        "Structural_Framing": "OST_StructuralFraming",
        "Furniture": "OST_Furniture",
        "Lighting_Fixtures": "OST_LightingFixtures",
        "Plumbing_Fixtures": "OST_PlumbingFixtures",
    }

    def _require_doc(self):
        doc = get_doc()
        if doc is None:
            raise ToolError("No active Revit document")
        return doc

    def list_levels(self) -> dict:
        from Autodesk.Revit import DB

        doc = self._require_doc()

        levels = []
        collector = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        for level in collector:
            try:
                levels.append(
                    {
                        "name": normalize_string(level.Name),
                        "elevation": round(float(level.Elevation), 2),
                        "element_id": element_id_value(level.Id),
                    }
                )
            except Exception:
                continue

        levels.sort(key=lambda item: item.get("elevation", 0))
        return {"levels": levels, "count": len(levels)}

    def _project_info(self, doc) -> dict:
        info = {
            "name": normalize_string(doc.Title),
            "number": DEFAULT_NOT_SET,
            "client": DEFAULT_NOT_SET,
            "file_name": normalize_string(doc.Title),
        }
        try:
            project_info = doc.ProjectInformation
            if project_info is None:
                return info
            return {
                "name": normalize_string(project_info.Name if project_info else doc.Title),
                "number": normalize_string(project_info.Number if project_info else DEFAULT_NOT_SET),
                "client": normalize_string(project_info.ClientName if project_info else DEFAULT_NOT_SET),
                "file_name": normalize_string(doc.Title),
            }
        except Exception:
            return info

    def _element_summary(self, doc):
        from Autodesk.Revit import DB

        by_category = {}
        total = 0
        for display_name, enum_name in self._element_categories.items():
            try:
                category = getattr(DB.BuiltInCategory, enum_name)
                count = (
                    DB.FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .GetElementCount()
                )
                by_category[display_name] = count
                total += count
            except Exception:
                by_category[display_name] = 0
        return {"total_elements": total, "by_category": by_category}

    def _model_health(self, doc):
        try:
            warnings_count = len(list(doc.GetWarnings()))
        except Exception:
            warnings_count = 0
        unplaced_rooms = self._count_unplaced_rooms(doc)
        return {
            "total_warnings": warnings_count,
            "critical_warnings": 0,
            "unplaced_rooms": unplaced_rooms,
        }

    def _collect_rooms(self, doc):
        from Autodesk.Revit import DB

        rooms = []
        collector = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        for room in collector:
            room_info = self._room_info(doc, room)
            if room_info is not None:
                rooms.append(room_info)
        return rooms

    def _room_info(self, doc, room):
        try:
            room_name = normalize_string(room.LookupParameter("Name").AsString() if room.LookupParameter("Name") else "Unnamed Room")
            room_number = normalize_string(room.LookupParameter("Number").AsString() if room.LookupParameter("Number") else "")
            level_name = "Unknown Level"
            try:
                level = doc.GetElement(room.LevelId)
                if level:
                    level_name = normalize_string(level.Name)
            except Exception:
                pass

            area = None
            is_placed = False
            try:
                area = float(room.Area)
                is_placed = area > 0
            except Exception:
                pass

            info = {
                "name": room_name,
                "number": room_number,
                "level": level_name,
                "is_placed": is_placed,
            }
            if is_placed and area is not None:
                info["area"] = round(area, 2)
            return info
        except Exception:
            return None

    def _count_unplaced_rooms(self, doc) -> int:
        rooms = self._collect_rooms(doc)
        return sum(1 for room in rooms if not room.get("is_placed"))

    def _documentation_summary(self, doc):
        from Autodesk.Revit import DB

        all_views = DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements()
        valid_views = [
            view
            for view in all_views
            if hasattr(view, "IsTemplate")
            and not view.IsTemplate
            and view.ViewType != DB.ViewType.Internal
            and view.ViewType != DB.ViewType.ProjectBrowser
        ]
        sheets_count = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Sheets)
            .WhereElementIsNotElementType()
            .GetElementCount()
        )
        return {
            "total_views": len(valid_views),
            "view_breakdown": {
                "floor_plans": sum(1 for view in valid_views if view.ViewType == DB.ViewType.FloorPlan),
                "elevations": sum(1 for view in valid_views if view.ViewType == DB.ViewType.Elevation),
                "sections": sum(1 for view in valid_views if view.ViewType == DB.ViewType.Section),
                "3d_views": sum(1 for view in valid_views if view.ViewType == DB.ViewType.ThreeD),
                "schedules": sum(1 for view in valid_views if view.ViewType == DB.ViewType.Schedule),
            },
            "sheets_count": sheets_count,
        }

    def _linked_models(self, doc):
        from Autodesk.Revit import DB

        models = []
        try:
            link_instances = DB.FilteredElementCollector(doc).OfClass(DB.RevitLinkInstance).ToElements()
            for link_instance in link_instances:
                try:
                    link_doc = link_instance.GetLinkDocument()
                    models.append(
                        {
                            "name": normalize_string(link_instance.Name),
                            "is_loaded": link_doc is not None,
                            "is_pinned": bool(getattr(link_instance, "Pinned", False)),
                        }
                    )
                except Exception:
                    continue
        except Exception:
            pass
        return {"count": len(models), "models": models}

    def get_model_info(self) -> dict:
        doc = self._require_doc()

        levels_data = self.list_levels()
        levels = levels_data.get("levels", [])
        rooms = self._collect_rooms(doc)

        return {
            "project_info": self._project_info(doc),
            "element_summary": self._element_summary(doc),
            "model_health": self._model_health(doc),
            "spatial_organization": {"levels": levels, "rooms": rooms, "room_count": len(rooms)},
            "documentation": self._documentation_summary(doc),
            "linked_models": self._linked_models(doc),
        }
