"""Service for building Revit ElementFilters from declarative specifications."""
from __future__ import annotations

from System.Collections.Generic import List
from Autodesk.Revit import DB

from dto.filters import (
    BoundingBoxFilter,
    CategoryFilter,
    ClassFilter,
    ElementTypeFilter,
    ExclusionFilter,
    FilterRequest,
    FilterSpec,
    LevelFilter,
    ParameterHasValueFilter,
    ParameterNumericFilter,
    ParameterStringFilter,
    PhysicalModelFilter,
    ViewFilter,
    WorksetFilter,
)
from shared.element_helpers import (
    find_category_by_name,
    get_physical_element_filter,
    normalize_string,
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
    "greater": "CreateGreaterRule",
    "greater_or_equal": "CreateGreaterOrEqualRule",
    "less": "CreateLessRule",
    "less_or_equal": "CreateLessOrEqualRule",
}


class FilterService:
    """Translates declarative FilterRequest specs into Revit ElementFilter objects."""

    def collect_elements(self, request: FilterRequest) -> list[DB.Element]:
        """Collect elements from the active document matching the filter request."""
        doc = require_doc()
        collector = DB.FilteredElementCollector(doc)

        element_filter = self._build_composite(doc, request)
        if element_filter is not None:
            collector.WherePasses(element_filter)

        view_spec = self._find_view_spec(request)
        if view_spec is not None:
            view = self._resolve_view(doc, view_spec)
            collector = DB.FilteredElementCollector(doc, view.Id)
            if element_filter is not None:
                collector.WherePasses(element_filter)

        type_spec = self._find_type_spec(request)
        if type_spec is not None:
            if type_spec.is_type:
                collector.WhereElementIsElementType()
            else:
                collector.WhereElementIsNotElementType()

        return list(collector.ToElements())

    def describe_filters(self, request: FilterRequest) -> str:
        """Return a human-readable summary of the applied filters."""
        parts: list[str] = []
        for spec in request.filters:
            parts.append(self._describe_single(spec))
        joiner = " AND " if request.logic == "and" else " OR "
        return joiner.join(parts) if parts else "No filters applied"

    def _build_composite(self, doc: DB.Document, request: FilterRequest) -> DB.ElementFilter | None:
        sub_filters: list[DB.ElementFilter] = []
        for spec in request.filters:
            built = self._build_single(doc, spec)
            if built is not None:
                sub_filters.append(built)

        if not sub_filters:
            return None
        if len(sub_filters) == 1:
            return sub_filters[0]

        if request.logic == "or":
            return DB.LogicalOrFilter(List[DB.ElementFilter](sub_filters))
        return DB.LogicalAndFilter(List[DB.ElementFilter](sub_filters))

    def _build_single(self, doc: DB.Document, spec: FilterSpec) -> DB.ElementFilter | None:
        if isinstance(spec, CategoryFilter):
            return self._build_category(doc, spec)
        if isinstance(spec, ParameterStringFilter):
            return self._build_param_string(doc, spec)
        if isinstance(spec, ParameterNumericFilter):
            return self._build_param_numeric(doc, spec)
        if isinstance(spec, ParameterHasValueFilter):
            return self._build_param_has_value(doc, spec)
        if isinstance(spec, LevelFilter):
            return self._build_level(doc, spec)
        if isinstance(spec, ClassFilter):
            return self._build_class(spec)
        if isinstance(spec, BoundingBoxFilter):
            return self._build_bounding_box(spec)
        if isinstance(spec, PhysicalModelFilter):
            return get_physical_element_filter(doc)
        if isinstance(spec, ExclusionFilter):
            return self._build_exclusion(spec)
        if isinstance(spec, WorksetFilter):
            return self._build_workset(doc, spec)
        # ViewFilter and ElementTypeFilter are handled at the collector level
        return None

    # ------------------------------------------------------------------
    # Individual filter builders
    # ------------------------------------------------------------------

    @staticmethod
    def _build_category(doc: DB.Document, spec: CategoryFilter) -> DB.ElementFilter | None:
        cat_ids = List[DB.ElementId]()
        for name in spec.names:
            cat = find_category_by_name(doc, name)
            if cat is not None:
                cat_ids.Add(cat.Id)
        if cat_ids.Count == 0:
            return None
        return DB.ElementMulticategoryFilter(cat_ids, spec.inverted)

    @staticmethod
    def _build_param_string(doc: DB.Document, spec: ParameterStringFilter) -> DB.ElementParameterFilter | None:
        param_id = _find_parameter_id(doc, spec.parameter_name)
        if param_id is None:
            return None
        factory_method = _STRING_RULE_BUILDERS.get(spec.operator)
        if factory_method is None:
            return None
        rule = getattr(DB.ParameterFilterRuleFactory, factory_method)(param_id, spec.value, False)
        return DB.ElementParameterFilter(rule)

    @staticmethod
    def _build_param_numeric(doc: DB.Document, spec: ParameterNumericFilter) -> DB.ElementParameterFilter | None:
        param_id = _find_parameter_id(doc, spec.parameter_name)
        if param_id is None:
            return None
        factory_method = _NUMERIC_RULE_BUILDERS.get(spec.operator)
        if factory_method is None:
            return None
        builder = getattr(DB.ParameterFilterRuleFactory, factory_method)
        if spec.operator in ("equals", "not_equals"):
            rule = builder(param_id, spec.value, spec.epsilon)
        else:
            rule = builder(param_id, spec.value)
        return DB.ElementParameterFilter(rule)

    @staticmethod
    def _build_param_has_value(doc: DB.Document, spec: ParameterHasValueFilter) -> DB.ElementParameterFilter | None:
        param_id = _find_parameter_id(doc, spec.parameter_name)
        if param_id is None:
            return None
        if spec.has_value:
            rule = DB.ParameterFilterRuleFactory.CreateHasValueParameterRule(param_id)
        else:
            rule = DB.ParameterFilterRuleFactory.CreateHasNoValueParameterRule(param_id)
        return DB.ElementParameterFilter(rule)

    @staticmethod
    def _build_level(doc: DB.Document, spec: LevelFilter) -> DB.ElementLevelFilter | None:
        target = normalize_string(spec.level_name)
        levels = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        for level in levels:
            if normalize_string(level.Name) == target:
                return DB.ElementLevelFilter(level.Id)
        return None

    @staticmethod
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

    @staticmethod
    def _build_bounding_box(spec: BoundingBoxFilter) -> DB.BoundingBoxIntersectsFilter:
        outline = DB.Outline(
            DB.XYZ(spec.min_point[0], spec.min_point[1], spec.min_point[2]),
            DB.XYZ(spec.max_point[0], spec.max_point[1], spec.max_point[2]),
        )
        return DB.BoundingBoxIntersectsFilter(outline)

    @staticmethod
    def _build_exclusion(spec: ExclusionFilter) -> DB.ExclusionFilter:
        ids = List[DB.ElementId]()
        for eid in spec.element_ids:
            ids.Add(DB.ElementId(eid))
        return DB.ExclusionFilter(ids)

    @staticmethod
    def _build_workset(doc: DB.Document, spec: WorksetFilter) -> DB.ElementWorksetFilter | None:
        try:
            workset_table = doc.GetWorksetTable()
            for workset_id in workset_table.GetWorksetIds():
                workset = workset_table.GetWorkset(workset_id)
                if normalize_string(workset.Name) == normalize_string(spec.workset_name):
                    return DB.ElementWorksetFilter(workset_id)
        except Exception:
            pass
        return None

    # ------------------------------------------------------------------
    # Helpers for view/type specs (handled at collector level, not filter level)
    # ------------------------------------------------------------------

    @staticmethod
    def _find_view_spec(request: FilterRequest) -> ViewFilter | None:
        for spec in request.filters:
            if isinstance(spec, ViewFilter):
                return spec
        return None

    @staticmethod
    def _find_type_spec(request: FilterRequest) -> ElementTypeFilter | None:
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
        target = normalize_string(spec.view_name)
        for v in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            try:
                if normalize_string(v.Name) == target:
                    return v
            except Exception:
                continue
        raise ToolError("View '{}' not found".format(spec.view_name))

    # ------------------------------------------------------------------
    # Description helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _describe_single(spec: FilterSpec) -> str:
        describer = _FILTER_DESCRIBERS.get(type(spec))
        if describer is not None:
            return describer(spec)
        return str(spec)


# ------------------------------------------------------------------
# Filter description dispatch table
# ------------------------------------------------------------------

def _desc_category(s: CategoryFilter) -> str:
    inv = " (inverted)" if s.inverted else ""
    return "Category in [{}]{}".format(", ".join(s.names), inv)


def _desc_param_str(s: ParameterStringFilter) -> str:
    return "Parameter '{}' {} '{}'".format(s.parameter_name, s.operator, s.value)


def _desc_param_num(s: ParameterNumericFilter) -> str:
    return "Parameter '{}' {} {}".format(s.parameter_name, s.operator, s.value)


def _desc_param_has(s: ParameterHasValueFilter) -> str:
    verb = "has value" if s.has_value else "has no value"
    return "Parameter '{}' {}".format(s.parameter_name, verb)


def _desc_level(s: LevelFilter) -> str:
    return "Level = '{}'".format(s.level_name)


def _desc_class(s: ClassFilter) -> str:
    return "Class in [{}]".format(", ".join(s.class_names))


def _desc_bbox(s: BoundingBoxFilter) -> str:
    return "BoundingBox intersects ({} -> {})".format(s.min_point, s.max_point)


def _desc_view(s: ViewFilter) -> str:
    return "View = '{}'".format(s.view_name or "active")


def _desc_etype(s: ElementTypeFilter) -> str:
    return "ElementTypes only" if s.is_type else "Instances only"


def _desc_physical(_s: PhysicalModelFilter) -> str:
    return "Physical model elements"


def _desc_exclusion(s: ExclusionFilter) -> str:
    return "Excluding {} element(s)".format(len(s.element_ids))


def _desc_workset(s: WorksetFilter) -> str:
    return "Workset = '{}'".format(s.workset_name)


_FILTER_DESCRIBERS: dict[type, object] = {
    CategoryFilter: _desc_category,
    ParameterStringFilter: _desc_param_str,
    ParameterNumericFilter: _desc_param_num,
    ParameterHasValueFilter: _desc_param_has,
    LevelFilter: _desc_level,
    ClassFilter: _desc_class,
    BoundingBoxFilter: _desc_bbox,
    ViewFilter: _desc_view,
    ElementTypeFilter: _desc_etype,
    PhysicalModelFilter: _desc_physical,
    ExclusionFilter: _desc_exclusion,
    WorksetFilter: _desc_workset,
}


# ------------------------------------------------------------------
# Parameter ID resolution
# ------------------------------------------------------------------

def _find_parameter_id(doc: DB.Document, parameter_name: str) -> DB.ElementId | None:
    """Find the ElementId of a parameter definition by name."""
    target = normalize_string(parameter_name)
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
        type_elem = doc.GetElement(type_id) if type_id and type_id != DB.ElementId.InvalidElementId else None
    except Exception:
        type_elem = None
    if type_elem is not None:
        return _scan_params_map(type_elem.ParametersMap, target)
    return None


def _scan_params_map(params_map: object, target: str) -> DB.ElementId | None:
    for param in params_map:
        try:
            if normalize_string(param.Definition.Name) == target:
                return param.Definition.Id
        except Exception:
            continue
    return None


def _scan_shared_parameters(doc: DB.Document, target: str) -> DB.ElementId | None:
    for sp in DB.FilteredElementCollector(doc).OfClass(DB.SharedParameterElement):
        try:
            if normalize_string(sp.Name) == target:
                return sp.Id
        except Exception:
            continue
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
