"""Service for model-level information and hierarchy."""

from __future__ import annotations

from Autodesk.Revit import DB

from dto.elements import (
    DocumentationSummary,
    ElementSummary,
    LevelInfo,
    LevelsResult,
    LinkedModelInfo,
    LinkedModelsInfo,
    ModelHealthInfo,
    ModelInfoResult,
    ProjectInfo,
    RoomInfo,
    SpatialOrganization,
)
from shared.constants import DEFAULT_NOT_SET
from shared.element_helpers import (
    category_display_name,
    element_id_value,
    get_param_string,
    get_physical_element_filter,
    normalize_string,
    require_doc,
)


class ModelService:
    def list_levels(self) -> LevelsResult:
        doc = require_doc()

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
                    LevelInfo(
                        name=normalize_string(level.Name),
                        elevation=round(float(level.Elevation), 2),
                        element_id=element_id_value(level.Id),
                    )
                )
            except Exception:
                continue

        levels.sort(key=lambda item: item.elevation)
        return LevelsResult(levels=levels, count=len(levels))

    def _project_info(self, doc: DB.Document) -> ProjectInfo:
        info = ProjectInfo(
            name=normalize_string(doc.Title),
            number=DEFAULT_NOT_SET,
            client=DEFAULT_NOT_SET,
            file_name=normalize_string(doc.Title),
        )
        try:
            project_info = doc.ProjectInformation
            if project_info is None:
                return info
            return ProjectInfo(
                name=normalize_string(project_info.Name),
                number=normalize_string(project_info.Number),
                client=normalize_string(project_info.ClientName),
                file_name=normalize_string(doc.Title),
            )
        except Exception:
            return info

    @staticmethod
    def _element_summary(doc: DB.Document) -> ElementSummary:
        multi_filter = get_physical_element_filter(doc)
        elements = (
            DB.FilteredElementCollector(doc)
            .WherePasses(multi_filter)
            .WhereElementIsNotElementType()
            .ToElements()
        )

        by_category: dict[str, int] = {}
        for elem in elements:
            try:
                cat_name = category_display_name(elem)
                by_category[cat_name] = by_category.get(cat_name, 0) + 1
            except Exception:
                continue

        total = sum(by_category.values())
        return ElementSummary(total_elements=total, by_category=by_category)

    def _model_health(self, doc: DB.Document) -> ModelHealthInfo:
        try:
            warnings_count = len(list(doc.GetWarnings()))
        except Exception:
            warnings_count = 0
        unplaced_rooms = self._count_unplaced_rooms(doc)
        return ModelHealthInfo(
            total_warnings=warnings_count,
            critical_warnings=0,
            unplaced_rooms=unplaced_rooms,
        )

    def _collect_rooms(self, doc: DB.Document) -> list[RoomInfo]:
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

    def _room_info(self, doc: DB.Document, room: DB.Element) -> RoomInfo | None:
        try:
            room_name = get_param_string(room, "Name", default="Unnamed Room")
            room_number = get_param_string(room, "Number")
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
            return RoomInfo(
                name=info["name"],
                number=info["number"],
                level=info["level"],
                is_placed=info["is_placed"],
                area=info.get("area"),
            )
        except Exception:
            return None

    def _count_unplaced_rooms(self, doc: DB.Document) -> int:
        rooms = self._collect_rooms(doc)
        return sum(1 for room in rooms if not room.is_placed)

    def _documentation_summary(self, doc: DB.Document) -> DocumentationSummary:
        all_views = DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements()
        valid_views = []
        for view in all_views:
            try:
                if view.IsTemplate:
                    continue
                if view.ViewType in (DB.ViewType.Internal, DB.ViewType.ProjectBrowser):
                    continue
                valid_views.append(view)
            except Exception:
                continue
        sheets_count = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Sheets)
            .WhereElementIsNotElementType()
            .GetElementCount()
        )
        return DocumentationSummary(
            total_views=len(valid_views),
            view_breakdown={
                "floor_plans": sum(1 for view in valid_views if view.ViewType == DB.ViewType.FloorPlan),
                "elevations": sum(1 for view in valid_views if view.ViewType == DB.ViewType.Elevation),
                "sections": sum(1 for view in valid_views if view.ViewType == DB.ViewType.Section),
                "3d_views": sum(1 for view in valid_views if view.ViewType == DB.ViewType.ThreeD),
                "schedules": sum(1 for view in valid_views if view.ViewType == DB.ViewType.Schedule),
            },
            sheets_count=sheets_count,
        )

    def _linked_models(self, doc: DB.Document) -> LinkedModelsInfo:
        models = []
        try:
            link_instances = DB.FilteredElementCollector(doc).OfClass(DB.RevitLinkInstance).ToElements()
            for link_instance in link_instances:
                try:
                    link_doc = link_instance.GetLinkDocument()
                    models.append(
                        LinkedModelInfo(
                            name=normalize_string(link_instance.Name),
                            is_loaded=link_doc is not None,
                            is_pinned=bool(link_instance.Pinned),
                        )
                    )
                except Exception:
                    continue
        except Exception:
            pass
        return LinkedModelsInfo(count=len(models), models=models)

    def get_model_info(self) -> ModelInfoResult:
        doc = require_doc()

        levels_data = self.list_levels()
        levels = levels_data.levels
        rooms = self._collect_rooms(doc)

        return ModelInfoResult(
            project_info=self._project_info(doc),
            element_summary=self._element_summary(doc),
            model_health=self._model_health(doc),
            spatial_organization=SpatialOrganization(
                levels=levels,
                rooms=rooms,
                room_count=len(rooms),
            ),
            documentation=self._documentation_summary(doc),
            linked_models=self._linked_models(doc),
        )
