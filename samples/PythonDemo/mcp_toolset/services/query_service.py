"""Model intelligence: find, read, list, summary."""

from Autodesk.Revit import DB

from dto.filters import FilterSpec
from dto.query import (
    CategoryCount,
    ElementParametersResult,
    FindElementsResult,
    LevelSummary,
    LinkItem,
    ListCategoryParametersResult,
    ListLinksResult,
    ListRoomsResult,
    ListTypesResult,
    ModelSummaryResult,
    ParameterEntry,
    PhaseSummary,
    ReadParametersResult,
    RoomItem,
    TypeInfo,
    WorksetSummary,
)
from services.filter_service import FilterService
from shared.element_helpers import (
    category_display_name,
    require_category,
    require_doc,
)
from shared.element_projection import project_element_fields
from shared.parameter_accessor import get_parameter_value, parameter_entry
from shared.responses import ToolError
from shared.transactions import run_transaction

_DEFAULT_FIELDS = [
    "id",
    "category",
    "family",
    "type",
    "level",
    "name",
    "workset",
    "bbox",
]


class QueryService:
    def __init__(self) -> None:
        self._filters = FilterService()

    def get_model_summary(self) -> ModelSummaryResult:
        doc = require_doc()
        return ModelSummaryResult(
            project=_project_summary(doc),
            categories=_category_counts(doc),
            warnings_count=_warning_count(doc),
            levels=_level_summaries(doc),
            phases=_phase_summaries(doc),
            worksets=_workset_summaries(doc),
            links=[LinkItem(**item) for item in self._collect_links(doc)],
        )

    def find_elements(
        self,
        filters: FilterSpec | None = None,
        *,
        selected_only: bool = False,
        include_types: bool = False,
        include_instances: bool = True,
        max_results: int = 500,
        offset: int = 0,
        fields: list[str] | None = None,
    ) -> FindElementsResult:
        if not include_types and not include_instances:
            raise ToolError(
                "At least one of includeTypes or includeInstances must be true"
            )
        if max_results <= 0:
            max_results = 500
        offset = max(offset, 0)

        doc = require_doc()
        requested = [f.lower() for f in (fields or _DEFAULT_FIELDS)]
        elements = self._filters.collect_elements(
            filters,
            selected_only=selected_only,
            include_types=include_types,
            include_instances=include_instances,
        )
        count = len(elements)
        page = elements[offset : offset + max_results]
        truncated = offset + len(page) < count
        items = [project_element_fields(doc, elem, requested) for elem in page]
        return FindElementsResult(count=count, truncated=truncated, elements=items)

    @staticmethod
    def read_parameters(
        element_ids: list[int],
        param_names: list[str] | None = None,
    ) -> ReadParametersResult:
        if not element_ids:
            raise ToolError("At least one element ID is required")
        doc = require_doc()
        name_filter = _build_param_name_filter(param_names)
        results = []
        for eid in element_ids:
            elem = doc.GetElement(DB.ElementId(eid))
            if elem is None:
                raise ToolError(f"Element with ID {eid} not found")
            params = []
            for param in elem.Parameters:
                try:
                    name = (param.Definition.Name or "")
                    if name_filter is not None and name.lower() not in name_filter:
                        continue
                    entry = parameter_entry(param, doc)
                    params.append(entry)
                except Exception:
                    pass
            results.append(
                ElementParametersResult(
                    id=eid,
                    params=[ParameterEntry(**entry) for entry in params],
                )
            )
        return ReadParametersResult(elements=results)

    def list_types(self, kind: str, category: str | None = None) -> ListTypesResult:
        doc = require_doc()
        normalized = kind.strip().lower()
        if normalized == "family":
            types = self._list_family_types(doc, category)
        elif normalized == "mep_system":
            types = self._list_mep_system_types(doc)
        elif normalized == "view_template":
            types = self._list_view_templates(doc)
        elif normalized == "title_block":
            types = self._list_title_blocks(doc)
        else:
            raise ToolError(
                f"Invalid kind '{kind}'. Use family, mep_system, view_template, or title_block"
            )
        return ListTypesResult(types=[TypeInfo(**t) for t in types])

    @staticmethod
    def list_category_parameters(
        category_name: str,
    ) -> ListCategoryParametersResult:
        if not category_name.strip():
            raise ToolError("Category name cannot be empty")
        doc = require_doc()
        category = require_category(doc, category_name)

        sample = (
            DB.FilteredElementCollector(doc)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .FirstElement()
        )

        def _operation() -> list[dict]:
            temp_schedule = DB.ViewSchedule.CreateSchedule(doc, category.Id)
            schedulable = temp_schedule.Definition.GetSchedulableFields()
            parameters = []
            for field in schedulable:
                name = field.GetName(doc)
                storage_type = "Unknown"
                sample_value = ""
                if sample is not None:
                    param = sample.LookupParameter(name)
                    if param is not None:
                        storage_type = str(param.StorageType)
                        sample_value = get_parameter_value(param, doc)
                parameters.append(
                    {
                        "name": name,
                        "storageType": storage_type,
                        "sampleValue": sample_value,
                    }
                )
            parameters.sort(key=lambda item: item["name"].lower())
            return parameters

        params = run_transaction(
            doc, "Temporary Schedule for Parameter Discovery", _operation
        )
        return ListCategoryParametersResult(parameters=params)

    @staticmethod
    def list_rooms() -> ListRoomsResult:
        doc = require_doc()
        collector = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        rooms: list[RoomItem] = []
        for room in collector:
            item = _room_item_from_element(doc, room)
            if item is not None:
                rooms.append(item)
        return ListRoomsResult(rooms=rooms)

    def list_links(self) -> ListLinksResult:
        doc = require_doc()
        return ListLinksResult(
            links=[LinkItem(**item) for item in self._collect_links(doc)]
        )

    @staticmethod
    def _list_family_types(
        doc: DB.Document, category_name: str | None
    ) -> list[dict]:
        collector = (
            DB.FilteredElementCollector(doc)
            .WhereElementIsElementType()
            .OfClass(DB.FamilySymbol)
        )
        if category_name:
            category = require_category(doc, category_name)
            collector = collector.OfCategoryId(category.Id)
        types = []
        for symbol in collector.ToElements():
            types.append(
                {
                    "id": int(symbol.Id.Value),
                    "name": (symbol.Name or ""),
                    "family": (symbol.Family.Name or ""),
                    "category": category_display_name(symbol),
                }
            )
        types.sort(key=lambda item: (item["family"], item["name"]))
        return types

    @staticmethod
    def _list_mep_system_types(doc: DB.Document) -> list[dict]:
        types = []
        for system in (
            DB.FilteredElementCollector(doc).OfClass(DB.MEPSystemType).ToElements()
        ):
            family = "Electrical System"
            try:
                if isinstance(system, DB.Mechanical.MechanicalSystemType):
                    family = "Mechanical System"
                elif isinstance(system, DB.Plumbing.PipingSystemType):
                    family = "Piping System"
            except Exception:
                pass
            types.append(
                {
                    "id": int(system.Id.Value),
                    "name": (system.Name or ""),
                    "family": family,
                    "category": category_display_name(system),
                }
            )
        types.sort(key=lambda item: item["name"].lower())
        return types

    @staticmethod
    def _list_view_templates(doc: DB.Document) -> list[dict]:
        types = []
        for view in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            if not view.IsTemplate:
                continue
            types.append(
                {
                    "id": int(view.Id.Value),
                    "name": (view.Name or ""),
                    "family": "View Template",
                    "category": str(view.ViewType),
                }
            )
        types.sort(key=lambda item: item["name"])
        return types

    @staticmethod
    def _list_title_blocks(doc: DB.Document) -> list[dict]:
        types = []
        for symbol in (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .ToElements()
        ):
            types.append(
                {
                    "id": int(symbol.Id.Value),
                    "name": (symbol.Name or ""),
                    "family": (symbol.Family.Name or ""),
                    "category": category_display_name(symbol),
                }
            )
        types.sort(key=lambda item: (item["family"], item["name"]))
        return types

    @staticmethod
    def _collect_links(doc: DB.Document) -> list[dict]:
        links = []
        for link_type in (
            DB.FilteredElementCollector(doc).OfClass(DB.RevitLinkType).ToElements()
        ):
            links.append(
                {
                    "id": link_type.Id.Value,
                    "name": (link_type.Name or ""),
                    "type": "Revit",
                    "path": _external_path(doc, link_type.Id),
                    "loaded": _is_revit_link_loaded(doc, link_type),
                }
            )
        for import_inst in (
            DB.FilteredElementCollector(doc).OfClass(DB.ImportInstance).ToElements()
        ):
            links.append(
                {
                    "id": int(import_inst.Id.Value),
                    "name": (import_inst.Name or "" or ""),
                    "type": "CAD",
                    "path": _import_path(doc, import_inst),
                    "loaded": True,
                }
            )
        return links


def _project_summary(doc: DB.Document) -> dict[str, str]:
    info = doc.ProjectInformation
    return {
        "name": (info.Name or "") if info else (doc.Title or ""),
        "number": (info.Number or "") if info else "",
        "address": (info.Address or "") if info else "",
        "client": (info.ClientName or "") if info else "",
        "title": (doc.Title or ""),
    }


def _category_counts(doc: DB.Document) -> list[CategoryCount]:
    counts: dict[str, int] = {}
    for elem in (
        DB.FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements()
    ):
        cat_name = category_display_name(elem)
        counts[cat_name] = counts.get(cat_name, 0) + 1
    return [CategoryCount(name=name, count=count) for name, count in sorted(counts.items())]


def _level_summaries(doc: DB.Document) -> list[LevelSummary]:
    levels = [
        LevelSummary(
            id=int(level.Id.Value),
            name=(level.Name or ""),
            elevation=round(float(level.Elevation), 4),
        )
        for level in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements()
    ]
    levels.sort(key=lambda item: item.elevation)
    return levels


def _phase_summaries(doc: DB.Document) -> list[PhaseSummary]:
    try:
        return [
            PhaseSummary(
                id=int(phase.Id.Value),
                name=(phase.Name or ""),
            )
            for phase in doc.Phases
        ]
    except Exception:
        return []


def _workset_summaries(doc: DB.Document) -> list[WorksetSummary]:
    try:
        if not doc.IsWorkshared:
            return []
        return [
            WorksetSummary(
                id=int(workset.Id.Value),
                name=(workset.Name or ""),
                kind=str(workset.Kind),
            )
            for workset in DB.FilteredWorksetCollector(doc).ToWorksets()
        ]
    except Exception:
        return []


def _warning_count(doc: DB.Document) -> int:
    try:
        return len(list(doc.GetWarnings()))
    except Exception:
        return 0


def _room_level_name(doc: DB.Document, room: DB.Element) -> str:
    if not room.LevelId or room.LevelId == DB.ElementId.InvalidElementId:
        return ""
    level = doc.GetElement(room.LevelId)
    return (level.Name or "") if level else ""


def _room_location(room: DB.Element) -> list[float] | None:
    loc = room.Location
    if not isinstance(loc, DB.LocationPoint):
        return None
    pt = loc.Point
    return [pt.X, pt.Y, pt.Z]


def _room_department(room: DB.Architecture.Room) -> str:
    param = room.LookupParameter("Department")
    if not param or not param.HasValue:
        return ""
    return (param.AsString() or "" or "")


def _room_item_from_element(doc: DB.Document, room: DB.Architecture.Room) -> RoomItem | None:
    try:
        area = float(room.Area)
        if area <= 0:
            return None
        return RoomItem(
            id=int(room.Id.Value),
            name=(room.Name or ""),
            number=(room.Number or ""),
            area=round(area, 4),
            level=_room_level_name(doc, room),
            department=_room_department(room),
            location=_room_location(room),
        )
    except Exception:
        return None


def _external_path(doc: DB.Document, element_id: DB.ElementId) -> str:
    try:
        reference = DB.ExternalFileUtils.GetExternalFileReference(doc, element_id)
        return DB.ModelPathUtils.ConvertModelPathToUserVisiblePath(
            reference.GetAbsolutePath()
        )
    except Exception:
        return ""


def _build_param_name_filter(param_names: list[str] | None) -> set[str] | None:
    if param_names is None:
        return None
    result: set[str] = set()
    for index in range(len(param_names)):
        param_name = param_names[index]
        result.add((param_name or "").lower())
    return result


def _import_path(doc: DB.Document, import_inst: DB.ImportInstance) -> str:
    try:
        type_id = import_inst.GetTypeId()
        if type_id and type_id != DB.ElementId.InvalidElementId:
            path = _external_path(doc, type_id)
            if path:
                return path
        return (import_inst.Name or "")
    except Exception:
        return (import_inst.Name or "")


def _is_revit_link_loaded(doc: DB.Document, link_type: DB.RevitLinkType) -> bool:
    try:
        return DB.RevitLinkType.IsLoaded(doc, link_type.Id)
    except Exception:
        return False
