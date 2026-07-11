"""Model intelligence: find, read, list, summary."""
from __future__ import annotations

from Autodesk.Revit import DB

from dto.filters import FilterSpec
from dto.query import (
    CategoryCount,
    ElementSummaryItem,
    ElementParametersResult,
    FindElementsResult,
    LevelSummary,
    LinkItem,
    ListCategoryParametersResult,
    ListLinksResult,
    ListRoomsResult,
    ListTypesResult,
    ModelSummaryResult,
    PhaseSummary,
    ReadParametersResult,
    ParameterEntry,
    RoomItem,
    TypeInfo,
    WorksetSummary,
)
from services.filter_service import FilterService
from shared.element_helpers import (
    category_display_name,
    element_id_value,
    find_category_by_name,
    normalize_string,
    require_doc,
)
from shared.parameter_accessor import get_parameter_value, parameter_entry
from shared.responses import ToolError
from shared.transactions import run_transaction

_DEFAULT_FIELDS = ["id", "category", "family", "type", "level", "name", "workset", "bbox"]


class QueryService:
    def __init__(self) -> None:
        self._filters = FilterService()

    def get_model_summary(self) -> ModelSummaryResult:
        doc = require_doc()
        info = doc.ProjectInformation

        categories = []
        counts: dict[str, int] = {}
        for elem in DB.FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements():
            cat_name = category_display_name(elem)
            counts[cat_name] = counts.get(cat_name, 0) + 1
        for name, count in sorted(counts.items()):
            categories.append(CategoryCount(name=name, count=count))

        levels = []
        for level in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
            levels.append(
                LevelSummary(
                    id=element_id_value(level.Id),
                    name=normalize_string(level.Name),
                    elevation=round(float(level.Elevation), 4),
                )
            )
        levels.sort(key=lambda item: item.elevation)

        phases = []
        try:
            for phase in doc.Phases:
                phases.append(PhaseSummary(id=element_id_value(phase.Id), name=normalize_string(phase.Name)))
        except Exception:
            pass

        worksets = []
        try:
            if doc.IsWorkshared:
                for workset in DB.FilteredWorksetCollector(doc).ToWorksets():
                    worksets.append(
                        WorksetSummary(
                            id=int(workset.Id.IntegerValue),
                            name=normalize_string(workset.Name),
                            kind=str(workset.Kind),
                        )
                    )
        except Exception:
            pass

        links = [LinkItem(**item) for item in self._collect_links(doc)]

        try:
            warnings_count = len(list(doc.GetWarnings()))
        except Exception:
            warnings_count = 0

        return ModelSummaryResult(
            project={
                "name": normalize_string(info.Name) if info else normalize_string(doc.Title),
                "number": normalize_string(info.Number) if info else "",
                "address": normalize_string(info.Address) if info else "",
                "client": normalize_string(info.ClientName) if info else "",
                "title": normalize_string(doc.Title),
            },
            categories=categories,
            warnings_count=warnings_count,
            levels=levels,
            phases=phases,
            worksets=worksets,
            links=links,
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
            raise ToolError("At least one of includeTypes or includeInstances must be true")
        if max_results <= 0:
            max_results = 500
        if offset < 0:
            offset = 0

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
        items = [self._project_fields(doc, elem, requested) for elem in page]
        return FindElementsResult(count=count, truncated=truncated, elements=items)

    def read_parameters(
        self,
        element_ids: list[int],
        param_names: list[str] | None = None,
    ) -> ReadParametersResult:
        if not element_ids:
            raise ToolError("At least one element ID is required")
        doc = require_doc()
        name_filter = {normalize_string(n).lower() for n in param_names} if param_names else None
        results = []
        for eid in element_ids:
            elem = doc.GetElement(DB.ElementId(eid))
            if elem is None:
                raise ToolError("Element with ID {} not found".format(eid))
            params = []
            for param in elem.Parameters:
                try:
                    name = normalize_string(param.Definition.Name)
                    if name_filter is not None and name.lower() not in name_filter:
                        continue
                    entry = parameter_entry(param, doc)
                    params.append(entry)
                except Exception:
                    continue
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
            raise ToolError("Invalid kind '{}'. Use family, mep_system, view_template, or title_block".format(kind))
        return ListTypesResult(types=[TypeInfo(**t) for t in types])

    def list_category_parameters(self, category_name: str) -> ListCategoryParametersResult:
        if not category_name.strip():
            raise ToolError("Category name cannot be empty")
        doc = require_doc()
        category = find_category_by_name(doc, category_name)
        if category is None:
            raise ToolError("Category '{}' not found".format(category_name))

        sample = (
            DB.FilteredElementCollector(doc)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .FirstElement()
        )

        def _operation():
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
                    {"name": name, "storageType": storage_type, "sampleValue": sample_value}
                )
            parameters.sort(key=lambda item: item["name"].lower())
            return parameters

        params = run_transaction(doc, "Temporary Schedule for Parameter Discovery", _operation)
        return ListCategoryParametersResult(parameters=params)

    def list_rooms(self) -> ListRoomsResult:
        doc = require_doc()
        rooms = []
        collector = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        for room in collector:
            try:
                if float(room.Area) <= 0:
                    continue
                level_name = ""
                if room.LevelId and room.LevelId != DB.ElementId.InvalidElementId:
                    level = doc.GetElement(room.LevelId)
                    if level:
                        level_name = normalize_string(level.Name)
                location = None
                loc = room.Location
                if isinstance(loc, DB.LocationPoint):
                    pt = loc.Point
                    location = [pt.X, pt.Y, pt.Z]
                dept = ""
                p = room.LookupParameter("Department")
                if p and p.HasValue:
                    dept = normalize_string(p.AsString() or "")
                rooms.append(
                    RoomItem(
                        id=element_id_value(room.Id),
                        name=normalize_string(room.Name),
                        number=normalize_string(room.Number),
                        area=round(float(room.Area), 4),
                        level=level_name,
                        department=dept,
                        location=location,
                    )
                )
            except Exception:
                continue
        return ListRoomsResult(rooms=rooms)

    def list_links(self) -> ListLinksResult:
        doc = require_doc()
        return ListLinksResult(links=[LinkItem(**item) for item in self._collect_links(doc)])

    def _project_fields(self, doc: DB.Document, element: DB.Element, fields: list[str]) -> ElementSummaryItem:
        data: dict = {"id": element_id_value(element.Id)}
        for field in fields:
            if field == "name":
                data["name"] = normalize_string(element.Name)
            elif field == "category":
                data["category"] = category_display_name(element)
            elif field == "family":
                data["family"] = element.Symbol.FamilyName if isinstance(element, DB.FamilyInstance) else ""
            elif field == "type":
                if isinstance(element, DB.FamilyInstance):
                    data["type"] = normalize_string(element.Symbol.Name)
                else:
                    data["type"] = normalize_string(element.Name)
            elif field == "level":
                if element.LevelId and element.LevelId != DB.ElementId.InvalidElementId:
                    level = doc.GetElement(element.LevelId)
                    data["level"] = normalize_string(level.Name) if level else ""
                else:
                    data["level"] = ""
            elif field == "workset":
                data["workset"] = self._workset_name(doc, element)
            elif field == "bbox":
                bb = element.get_BoundingBox(None)
                if bb is not None:
                    data["bbox"] = {
                        "min": [bb.Min.X, bb.Min.Y, bb.Min.Z],
                        "max": [bb.Max.X, bb.Max.Y, bb.Max.Z],
                    }
        return ElementSummaryItem(**data)

    @staticmethod
    def _workset_name(doc: DB.Document, element: DB.Element) -> str:
        try:
            if element.WorksetId == DB.WorksetId.InvalidWorksetId:
                return ""
            return normalize_string(doc.GetWorksetTable().GetWorkset(element.WorksetId).Name)
        except Exception:
            return ""

    def _list_family_types(self, doc: DB.Document, category_name: str | None) -> list[dict]:
        collector = DB.FilteredElementCollector(doc).WhereElementIsElementType().OfClass(DB.FamilySymbol)
        if category_name:
            category = find_category_by_name(doc, category_name)
            if category is None:
                raise ToolError("Category '{}' not found".format(category_name))
            collector = collector.OfCategoryId(category.Id)
        types = []
        for symbol in collector.ToElements():
            types.append(
                {
                    "id": element_id_value(symbol.Id),
                    "name": normalize_string(symbol.Name),
                    "family": normalize_string(symbol.Family.Name),
                    "category": category_display_name(symbol),
                }
            )
        types.sort(key=lambda item: (item["family"], item["name"]))
        return types

    def _list_mep_system_types(self, doc: DB.Document) -> list[dict]:
        types = []
        for system in DB.FilteredElementCollector(doc).OfClass(DB.MEPSystemType).ToElements():
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
                    "id": element_id_value(system.Id),
                    "name": normalize_string(system.Name),
                    "family": family,
                    "category": category_display_name(system),
                }
            )
        types.sort(key=lambda item: item["name"].lower())
        return types

    def _list_view_templates(self, doc: DB.Document) -> list[dict]:
        types = []
        for view in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            if not view.IsTemplate:
                continue
            types.append(
                {
                    "id": element_id_value(view.Id),
                    "name": normalize_string(view.Name),
                    "family": "View Template",
                    "category": str(view.ViewType),
                }
            )
        types.sort(key=lambda item: item["name"])
        return types

    def _list_title_blocks(self, doc: DB.Document) -> list[dict]:
        types = []
        for symbol in (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .ToElements()
        ):
            types.append(
                {
                    "id": element_id_value(symbol.Id),
                    "name": normalize_string(symbol.Name),
                    "family": normalize_string(symbol.Family.Name),
                    "category": category_display_name(symbol),
                }
            )
        types.sort(key=lambda item: (item["family"], item["name"]))
        return types

    @staticmethod
    def _collect_links(doc: DB.Document) -> list[dict]:
        links = []
        for link_type in DB.FilteredElementCollector(doc).OfClass(DB.RevitLinkType).ToElements():
            links.append(
                {
                    "id": element_id_value(link_type.Id),
                    "name": normalize_string(link_type.Name),
                    "type": "Revit",
                    "path": _external_path(doc, link_type.Id),
                    "loaded": _is_revit_link_loaded(doc, link_type),
                }
            )
        for import_inst in DB.FilteredElementCollector(doc).OfClass(DB.ImportInstance).ToElements():
            links.append(
                {
                    "id": element_id_value(import_inst.Id),
                    "name": normalize_string(import_inst.Name or ""),
                    "type": "CAD",
                    "path": _import_path(doc, import_inst),
                    "loaded": True,
                }
            )
        return links


def _external_path(doc: DB.Document, element_id: DB.ElementId) -> str:
    try:
        reference = DB.ExternalFileUtils.GetExternalFileReference(doc, element_id)
        return DB.ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath())
    except Exception:
        return ""


def _import_path(doc: DB.Document, import_inst: DB.ImportInstance) -> str:
    try:
        type_id = import_inst.GetTypeId()
        if type_id and type_id != DB.ElementId.InvalidElementId:
            path = _external_path(doc, type_id)
            if path:
                return path
        return normalize_string(import_inst.Name or "")
    except Exception:
        return normalize_string(import_inst.Name or "")


def _is_revit_link_loaded(doc: DB.Document, link_type: DB.RevitLinkType) -> bool:
    try:
        return DB.RevitLinkType.IsLoaded(doc, link_type.Id)
    except Exception:
        return False
