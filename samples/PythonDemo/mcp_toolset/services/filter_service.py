"""Service for building Revit ElementFilters from declarative specifications."""

from typing import Callable, Iterable, cast

from Autodesk.Revit import DB, UI
from RevitDevTool.Core import RevitContext
from System.Collections.Generic import List

from dto.filters import (
    BoundingBoxFilter,
    CategoryFilter,
    ClassFilter,
    ElementTypeFilter,
    ExclusionFilter,
    FilterItem,
    FilterSpec,
    LevelFilter,
    ParameterHasValueFilter,
    ParameterNumericFilter,
    ParameterStringFilter,
    PhaseFilter,
    ViewFilter,
    WorksetFilter,
)
from shared.element_helpers import (
    find_category_by_name,
    require_doc,
)
from shared.responses import ToolError

_CLASS_MAP: dict[str, type] = {
    "wall": DB.Wall,
    "floor": DB.Floor,
    "ceiling": DB.Ceiling,
    "familyinstance": DB.FamilyInstance,
    "roofbase": DB.RoofBase,
    "group": DB.Group,
    "room": DB.Architecture.Room,
    "area": DB.Area,
    "familysymbol": DB.FamilySymbol,
    "material": DB.Material,
    "grid": DB.Grid,
    "level": DB.Level,
    "viewsheet": DB.ViewSheet,
    "viewschedule": DB.ViewSchedule,
    "revitlinkinstance": DB.RevitLinkInstance,
}


_STRING_RULE_BUILDERS: dict[str, str] = {
    "equals": "CreateEqualsRule",
    "not_equals": "CreateNotEqualsRule",
    "contains": "CreateContainsRule",
    "not_contains": "CreateNotContainsRule",
    "begins_with": "CreateBeginsWithRule",
    "not_begins_with": "CreateNotBeginsWithRule",
    "ends_with": "CreateEndsWithRule",
    "not_ends_with": "CreateNotEndsWithRule",
}


_NUMERIC_RULE_BUILDERS: dict[str, str] = {
    "equals": "CreateEqualsRule",
    "not_equals": "CreateNotEqualsRule",
    "greater_than": "CreateGreaterRule",
    "greater_or_equal": "CreateGreaterOrEqualRule",
    "less_than": "CreateLessRule",
    "less_or_equal": "CreateLessOrEqualRule",
}


class FilterService:
    """Translates declarative FilterSpec objects into Revit ElementFilter objects."""

    def build_collector(
        self,
        request: FilterSpec | None = None,
        *,
        selected_only: bool = False,
        include_types: bool = False,
        include_instances: bool = True,
    ) -> DB.FilteredElementCollector:
        doc = require_doc()
        view_spec = self._find_view_spec(request) if request else None
        type_spec = self._find_type_spec(request) if request else None

        collector = self._create_scope_collector(
            doc,
            selected_only=selected_only,
            view_spec=view_spec,
        )
        collector = self._apply_request_filter(
            doc, collector, request, view_spec=view_spec
        )
        return self._apply_instance_type_scope(
            collector,
            type_spec=type_spec,
            include_types=include_types,
            include_instances=include_instances,
        )

    def _create_scope_collector(
        self,
        doc: DB.Document,
        *,
        selected_only: bool,
        view_spec: ViewFilter | None,
    ) -> DB.FilteredElementCollector:
        if selected_only:
            ui_doc: UI.UIDocument = RevitContext.ActiveUiDocument # noqa
            if ui_doc is None:
                raise ToolError("No active UI document for selection")
            selected_ids = ui_doc.Selection.GetElementIds()
            if selected_ids is None or selected_ids.Count == 0:
                raise ToolError("No elements selected")
            return DB.FilteredElementCollector(doc, selected_ids)
        if view_spec is not None:
            view = self._resolve_view(doc, view_spec)
            return DB.FilteredElementCollector(doc, view.Id)
        return DB.FilteredElementCollector(doc)

    def _apply_request_filter(
        self,
        doc: DB.Document,
        collector: DB.FilteredElementCollector,
        request: FilterSpec | None,
        *,
        view_spec: ViewFilter | None,
    ) -> DB.FilteredElementCollector:
        if request is None:
            return collector
        element_filter = self._build_composite(
            doc, request, exclude_view=view_spec is not None, exclude_element_type=True
        )
        if element_filter is None:
            return collector
        return collector.WherePasses(element_filter)

    @staticmethod
    def _apply_instance_type_scope(
        collector: DB.FilteredElementCollector,
        *,
        type_spec: ElementTypeFilter | None,
        include_types: bool,
        include_instances: bool,
    ) -> DB.FilteredElementCollector:
        if type_spec is not None:
            if type_spec.is_type:
                return collector.WhereElementIsElementType()
            return collector.WhereElementIsNotElementType()
        if not include_types and include_instances:
            return collector.WhereElementIsNotElementType()
        if include_types and not include_instances:
            return collector.WhereElementIsElementType()
        return collector

    def collect_elements(
        self,
        request: FilterSpec | None = None,
        *,
        selected_only: bool = False,
        include_types: bool = False,
        include_instances: bool = True,
    ) -> list[DB.Element]:
        """Collect elements from the active document matching the filter request."""
        collector = self.build_collector(
            request,
            selected_only=selected_only,
            include_types=include_types,
            include_instances=include_instances,
        )
        return list(collector.ToElements())

    def describe_filters(self, request: FilterSpec | None) -> str:
        """Return a human-readable summary of the applied filters."""
        if request is None or not request.filters:
            return "No filters applied"
        parts: list[str] = []
        for spec in request.filters:
            parts.append(self._describe_single(spec))
        joiner = " AND " if request.logic == "and" else " OR "
        return joiner.join(parts) if parts else "No filters applied"

    @staticmethod
    def _build_composite(
        doc: DB.Document,
        request: FilterSpec,
        *,
        exclude_view: bool = False,
        exclude_element_type: bool = False,
    ) -> DB.ElementFilter | None:
        sub_filters: list[DB.ElementFilter] = []
        for spec in request.filters:
            if exclude_view and isinstance(spec, ViewFilter):
                continue
            if exclude_element_type and isinstance(spec, ElementTypeFilter):
                continue
            built = _build_filter_item(doc, spec)
            if built is not None:
                sub_filters.append(built)

        if not sub_filters:
            return None
        if len(sub_filters) == 1:
            return sub_filters[0]

        if request.logic == "or":
            return DB.LogicalOrFilter(List[DB.ElementFilter](sub_filters))
        return DB.LogicalAndFilter(List[DB.ElementFilter](sub_filters))

    # ------------------------------------------------------------------
    # Helpers for view/type specs (handled at collector level, not filter level)
    # ------------------------------------------------------------------

    @staticmethod
    def _find_view_spec(request: FilterSpec) -> ViewFilter | None:
        for spec in request.filters:
            if isinstance(spec, ViewFilter):
                return spec
        return None

    @staticmethod
    def _find_type_spec(request: FilterSpec) -> ElementTypeFilter | None:
        for spec in request.filters:
            if isinstance(spec, ElementTypeFilter):
                return spec
        return None

    @staticmethod
    def _resolve_view(doc: DB.Document, spec: ViewFilter) -> DB.View:
        if spec.view_name is None:
            view = doc.ActiveView
            if view is None:
                raise ToolError("No active view found")
            return view
        target = (spec.view_name or "")
        for v in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            try:
                if (v.Name or "") == target:
                    return v
            except Exception:
                pass
        raise ToolError(f"View '{spec.view_name}' not found")

    # ------------------------------------------------------------------
    # Description helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _describe_single(spec: FilterItem) -> str:
        return describe_filter_item(spec)


def _build_category(doc: DB.Document, spec: CategoryFilter) -> DB.ElementFilter | None:
    cat_ids = List[DB.ElementId]()
    for name in spec.names:
        cat = find_category_by_name(doc, name)
        if cat is not None:
            cat_ids.Add(cat.Id)
    if cat_ids.Count == 0:
        return None
    return DB.ElementMulticategoryFilter(cat_ids, spec.inverted)


def _build_param_string(
    doc: DB.Document, spec: ParameterStringFilter
) -> DB.ElementParameterFilter | None:
    param_id = _find_parameter_id(doc, spec.parameter_name)
    if param_id is None:
        return None
    if spec.operator not in _STRING_RULE_BUILDERS:
        return None
    method_name = _STRING_RULE_BUILDERS[spec.operator]
    builder = cast(
        Callable[[DB.ElementId, str, bool], object],
        getattr(DB.ParameterFilterRuleFactory, method_name),
    )
    rule = builder(param_id, spec.value, False)
    return DB.ElementParameterFilter(rule)


def _build_param_numeric(
    doc: DB.Document, spec: ParameterNumericFilter
) -> DB.ElementParameterFilter | None:
    param_id = _find_parameter_id(doc, spec.parameter_name)
    if param_id is None:
        return None
    if spec.operator not in _NUMERIC_RULE_BUILDERS:
        return None
    method_name = _NUMERIC_RULE_BUILDERS[spec.operator]
    builder = cast(
        Callable[..., object],
        getattr(DB.ParameterFilterRuleFactory, method_name),
    )
    if spec.operator in ("equals", "not_equals"):
        rule = builder(param_id, spec.value, spec.epsilon)
    else:
        rule = builder(param_id, spec.value)
    return DB.ElementParameterFilter(rule)


def _build_param_has_value(
    doc: DB.Document, spec: ParameterHasValueFilter
) -> DB.ElementParameterFilter | None:
    param_id = _find_parameter_id(doc, spec.parameter_name)
    if param_id is None:
        return None
    if spec.has_value:
        rule = DB.ParameterFilterRuleFactory.CreateHasValueParameterRule(param_id)
    else:
        rule = DB.ParameterFilterRuleFactory.CreateHasNoValueParameterRule(param_id)
    return DB.ElementParameterFilter(rule)


def _build_level(doc: DB.Document, spec: LevelFilter) -> DB.ElementLevelFilter | None:
    target = (spec.level_name or "")
    levels = (
        DB.FilteredElementCollector(doc)
        .OfCategory(DB.BuiltInCategory.OST_Levels)
        .WhereElementIsNotElementType()
        .ToElements()
    )
    for level in levels:
        if (level.Name or "") == target:
            return DB.ElementLevelFilter(level.Id)
    return None


def _build_class(spec: ClassFilter) -> DB.ElementFilter | None:
    types: list[type] = []
    for name in spec.class_names:
        cls = _CLASS_MAP.get(name.lower())
        if cls is not None:
            types.append(cls)
    if not types:
        return None
    if len(types) == 1:
        return DB.ElementClassFilter(types[0])
    return DB.ElementMulticlassFilter(List[type](types))


def _build_bounding_box(spec: BoundingBoxFilter) -> DB.ElementFilter:
    outline = DB.Outline(
        DB.XYZ(spec.min_point[0], spec.min_point[1], spec.min_point[2]),
        DB.XYZ(spec.max_point[0], spec.max_point[1], spec.max_point[2]),
    )
    if spec.mode == "intersecting":
        return DB.BoundingBoxIntersectsFilter(outline)
    return DB.BoundingBoxIsInsideFilter(outline)


def _find_phase_id(doc: DB.Document, phase_name: str) -> DB.ElementId | None:
    target = (phase_name or "")
    try:
        for phase in doc.Phases:
            if (phase.Name or "").lower() == target.lower():
                return phase.Id
    except Exception:
        return None
    return None


def _build_phase(doc: DB.Document, spec: PhaseFilter) -> DB.ElementFilter | None:
    phase_id = _find_phase_id(doc, spec.phase_name)
    if phase_id is None:
        return None
    sub_filters = List[DB.ElementFilter](
        [
            DB.ElementPhaseStatusFilter(phase_id, DB.ElementOnPhaseStatus.New),
            DB.ElementPhaseStatusFilter(phase_id, DB.ElementOnPhaseStatus.Existing),
            DB.ElementPhaseStatusFilter(phase_id, DB.ElementOnPhaseStatus.Demolished),
        ]
    )
    return DB.LogicalOrFilter(sub_filters)


def _build_exclusion(spec: ExclusionFilter) -> DB.ExclusionFilter:
    ids = List[DB.ElementId]()
    for eid in spec.element_ids:
        ids.Add(DB.ElementId(eid))
    return DB.ExclusionFilter(ids)


def _build_workset(
    doc: DB.Document, spec: WorksetFilter
) -> DB.ElementWorksetFilter | None:
    target = (spec.workset_name or "").lower()
    if not target:
        return None
    try:
        for workset in DB.FilteredWorksetCollector(doc).ToWorksets():
            if (workset.Name or "").lower() == target:
                return DB.ElementWorksetFilter(workset.Id)
    except Exception:
        pass
    return None


def _build_filter_item(doc: DB.Document, spec: FilterItem) -> DB.ElementFilter | None:
    if isinstance(
        spec,
        (
            ParameterStringFilter,
            ParameterNumericFilter,
            ParameterHasValueFilter,
        ),
    ):
        return _build_parameter_filter(doc, spec)
    if isinstance(spec, CategoryFilter):
        return _build_category(doc, spec)
    if isinstance(spec, LevelFilter):
        return _build_level(doc, spec)
    if isinstance(spec, ClassFilter):
        return _build_class(spec)
    if isinstance(spec, BoundingBoxFilter):
        return _build_bounding_box(spec)
    if isinstance(spec, PhaseFilter):
        return _build_phase(doc, spec)
    if isinstance(spec, ExclusionFilter):
        return _build_exclusion(spec)
    if isinstance(spec, WorksetFilter):
        return _build_workset(doc, spec)
    return None


def _build_parameter_filter(
    doc: DB.Document,
    spec: ParameterStringFilter | ParameterNumericFilter | ParameterHasValueFilter,
) -> DB.ElementFilter | None:
    if isinstance(spec, ParameterStringFilter):
        return _build_param_string(doc, spec)
    if isinstance(spec, ParameterNumericFilter):
        return _build_param_numeric(doc, spec)
    return _build_param_has_value(doc, spec)


# ------------------------------------------------------------------
# Filter description dispatch table
# ------------------------------------------------------------------


def _desc_category(s: CategoryFilter) -> str:
    inv = " (inverted)" if s.inverted else ""
    return "Category in [{}]{}".format(", ".join(s.names), inv)


def _desc_param_str(s: ParameterStringFilter) -> str:
    return f"Parameter '{s.parameter_name}' {s.operator} '{s.value}'"


def _desc_param_num(s: ParameterNumericFilter) -> str:
    return f"Parameter '{s.parameter_name}' {s.operator} {s.value}"


def _desc_param_has(s: ParameterHasValueFilter) -> str:
    verb = "has value" if s.has_value else "has no value"
    return f"Parameter '{s.parameter_name}' {verb}"


def _desc_level(s: LevelFilter) -> str:
    return f"Level = '{s.level_name}'"


def _desc_class(s: ClassFilter) -> str:
    return "Class in [{}]".format(", ".join(s.class_names))


def _desc_bbox(s: BoundingBoxFilter) -> str:
    return f"BoundingBox {s.mode} ({s.min_point} -> {s.max_point})"


def _desc_view(s: ViewFilter) -> str:
    return "View = '{}'".format(s.view_name or "active")


def _desc_etype(s: ElementTypeFilter) -> str:
    return "ElementTypes only" if s.is_type else "Instances only"


def _desc_phase(s: PhaseFilter) -> str:
    return f"Phase = '{s.phase_name}'"


def _desc_exclusion(s: ExclusionFilter) -> str:
    return f"Excluding {len(s.element_ids)} element(s)"


def _desc_workset(s: WorksetFilter) -> str:
    return f"Workset = '{s.workset_name}'"


_FILTER_DESCRIBERS: dict[type[FilterItem], Callable[[FilterItem], str]] = {
    CategoryFilter: _desc_category,
    ParameterStringFilter: _desc_param_str,
    ParameterNumericFilter: _desc_param_num,
    ParameterHasValueFilter: _desc_param_has,
    LevelFilter: _desc_level,
    ClassFilter: _desc_class,
    BoundingBoxFilter: _desc_bbox,
    ViewFilter: _desc_view,
    ElementTypeFilter: _desc_etype,
    PhaseFilter: _desc_phase,
    ExclusionFilter: _desc_exclusion,
    WorksetFilter: _desc_workset,
}


def describe_filter_item(spec: FilterItem) -> str:
    filter_type = type(spec)
    if filter_type not in _FILTER_DESCRIBERS:
        return str(spec)
    return _FILTER_DESCRIBERS[filter_type](spec)


# ------------------------------------------------------------------
# Parameter ID resolution
# ------------------------------------------------------------------


def _find_parameter_id(doc: DB.Document, parameter_name: str) -> DB.ElementId | None:
    """Find the ElementId of a parameter definition by name."""
    target = (parameter_name or "")
    result = _scan_sample_element_params(doc, target)
    if result is not None:
        return result
    return _scan_shared_parameters(doc, target)


def _scan_sample_element_params(doc: DB.Document, target: str) -> DB.ElementId | None:
    sample = _find_sample_element(doc)
    if sample is None:
        return None
    result = _scan_params_map(sample.ParametersMap, target)
    if result is not None:
        return result
    try:
        type_id = sample.GetTypeId()
        type_elem = (
            doc.GetElement(type_id)
            if type_id and type_id != DB.ElementId.InvalidElementId
            else None
        )
    except Exception:
        type_elem = None
    if type_elem is not None:
        return _scan_params_map(type_elem.ParametersMap, target)
    return None


def _scan_params_map(params_map: Iterable[object], target: str) -> DB.ElementId | None:
    for param in params_map:
        try:
            if (param.Definition.Name or "") == target:
                return param.Definition.Id
        except Exception:
            pass
    return None


def _scan_shared_parameters(doc: DB.Document, target: str) -> DB.ElementId | None:
    shared_params = (
        DB.FilteredElementCollector(doc).OfClass(DB.SharedParameterElement).ToElements()
    )
    for sp in shared_params:
        try:
            if (sp.Name or "") == target:
                return sp.Id
        except Exception:
            pass
    return None


def _find_sample_element(doc: DB.Document) -> DB.Element | None:
    """Get a single non-type element from the document for parameter scanning."""
    try:
        return (
            DB.FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .FirstElement()
        )
    except Exception:
        return None
