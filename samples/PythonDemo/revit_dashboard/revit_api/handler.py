# -*- coding: utf-8 -*-
"""Revit API command bodies for the dashboard.

Do not use a cached document. Read ``HOST_APP.doc`` at call time.
``with Transaction`` is the form this package already runs under pythonnet.
Temporary view modes still need a transaction.
"""

from __future__ import division, print_function

from Autodesk.Revit import DB
from pyrevit_webview.compat import Callable
from pyrevit_webview.serialize import JsonValue
from System.Collections.Generic import List

from revit_dashboard.context import HOST_APP
from revit_dashboard.log import get_logger

log = get_logger()
from revit_dashboard.data.collector import collect_model_elements, collect_model_info
from revit_dashboard.data.warnings import collect_warnings


def _run_transaction(doc, name, action):
    # type: (DB.Document, str, Callable[[], None]) -> None
    """Run ``action`` inside a transaction. Failures roll back and are logged."""
    with DB.Transaction(doc, name) as transaction:
        transaction.Start()
        try:
            action()
            transaction.Commit()
        except Exception as exc:
            log.error("transaction '{0}' rolled back: {1}".format(name, exc))
            transaction.RollBack()


def _element_level_name(doc, element_id):
    # type: (DB.Document, DB.ElementId) -> str
    if element_id.Equals(DB.ElementId.InvalidElementId):
        return ""
    level = doc.GetElement(element_id)  # type: DB.Element
    if level is None:
        return ""
    return level.Name or ""


def _matches_category(element, category_name):
    # type: (DB.Element, str) -> bool
    category = element.Category  # type: DB.Category
    if category is None:
        return False
    if not category_name:
        return True
    return (category.Name or "") == category_name


def _matches_level(doc, element, level_name):
    # type: (DB.Document, DB.Element, str) -> bool
    if not level_name:
        return True
    level_param = element.get_Parameter(DB.BuiltInParameter.FAMILY_LEVEL_PARAM)  # type: DB.Parameter
    if level_param is not None and level_param.HasValue:
        if _element_level_name(doc, level_param.AsElementId()) == level_name:
            return True
    return _element_level_name(doc, element.LevelId) == level_name


def _ids_for_level_category(doc, view, level_name, category_name):
    # type: (DB.Document, DB.View, str, str) -> List[DB.ElementId]
    id_list = List[DB.ElementId]()
    elements = DB.FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType()
    for element in elements:
        if not _matches_category(element, category_name):
            continue
        if not _matches_level(doc, element, level_name):
            continue
        id_list.Add(element.Id)
    return id_list


def _isolate_id_list(doc, view, id_list, level_name, category_name):
    # type: (DB.Document, DB.View, List[DB.ElementId], str, str) -> None
    if id_list.Count == 0:
        log.warning("no elements for {0}/{1}".format(level_name, category_name))
        return

    def isolate_action():
        # type: () -> None
        view.IsolateElementsTemporary(id_list)

    _run_transaction(doc, "Isolate By Level/Category", isolate_action)
    log.info(
        "isolated {0} elements for {1}/{2}".format(id_list.Count, level_name, category_name)
    )


class RevitApiHandler(object):
    """Command bodies addressed by the bridge command name."""

    def __init__(self):
        # type: () -> None
        self._commands = {  # type: dict[str, Callable[[dict[str, JsonValue]], dict[str, JsonValue] | None]]
            "GetSnapshot": self._cmd_get_snapshot,
            "Select": self._cmd_select,
            "Zoom": self._cmd_zoom,
            "Isolate": self._cmd_isolate,
            "ResetIsolation": self._cmd_reset_isolation,
            "ColorOverride": self._cmd_color_override,
            "ClearOverrides": self._cmd_clear_overrides,
            "IsolateByLevelCategory": self._cmd_isolate_by_level_category,
            "CreateWarningView": self._cmd_create_warning_view,
            "ApplyGroupOverrides": self._cmd_apply_group_overrides,
            "ResetScheduleMode": self._cmd_reset_schedule_mode,
            "GetElementParameters": self._cmd_get_element_parameters,
        }

    def knows(self, command):
        # type: (str) -> bool
        return command in self._commands

    def run(self, command, payload):
        # type: (str, dict[str, JsonValue]) -> dict[str, JsonValue] | None
        """Run a known command. ``payload`` is the request object."""
        if payload is None:
            payload = {}
        return self._commands[command](payload)

    def _cmd_get_snapshot(self, _params):
        # type: (dict[str, JsonValue]) -> dict[str, JsonValue]
        log.info("collecting snapshot")
        rows = collect_model_elements(HOST_APP.doc)
        log.info("collected {0} model elements".format(len(rows)))
        model_info = collect_model_info(HOST_APP.doc)
        warnings = collect_warnings(HOST_APP.doc)
        log.info("found {0} warnings".format(len(warnings)))
        return {
            "model_info": model_info,
            "rows": rows,
            "warnings": warnings,
        }

    def _cmd_select(self, params):
        # type: (dict[str, JsonValue]) -> None
        ids = self._resolve_ids(params)
        if ids:
            HOST_APP.uidoc.Selection.SetElementIds(ids)
            log.info("selected {0} elements".format(ids.Count))

    def _cmd_zoom(self, params):
        # type: (dict[str, JsonValue]) -> None
        ids = self._resolve_ids(params)
        if ids:
            HOST_APP.uidoc.Selection.SetElementIds(ids)
            HOST_APP.uidoc.ShowElements(ids)
            log.info("zoomed to {0} elements".format(ids.Count))

    def _cmd_isolate(self, params):
        # type: (dict[str, JsonValue]) -> None
        ids = self._resolve_ids(params)
        view = self._active_view()
        if ids:

            def action():
                # type: () -> None
                view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)
                view.IsolateElementsTemporary(ids)

            _run_transaction(HOST_APP.doc, "Isolate Elements", action)
            log.info("isolated {0} elements".format(ids.Count))

    def _cmd_reset_isolation(self, _params):
        # type: (dict[str, JsonValue]) -> None
        view = self._active_view()

        def action():
            # type: () -> None
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)

        _run_transaction(HOST_APP.doc, "Reset Isolation", action)
        log.info("reset isolation")

    def _cmd_color_override(self, params):
        # type: (dict[str, JsonValue]) -> None
        ids = self._resolve_ids(params)
        view = self._active_view()
        red, green, blue = params.get("color", [255, 0, 0])
        override = self._build_color_override(DB.Color(int(red), int(green), int(blue)))

        if ids:

            def action():
                # type: () -> None
                for index in range(ids.Count):
                    view.SetElementOverrides(ids[index], override)

            _run_transaction(HOST_APP.doc, "Color Override", action)
            log.info("color override on {0} elements".format(ids.Count))

    def _cmd_clear_overrides(self, _params):
        # type: (dict[str, JsonValue]) -> None
        view = self._active_view()
        doc = HOST_APP.doc
        collector = DB.FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType()
        default = DB.OverrideGraphicSettings()

        def action():
            # type: () -> None
            for elem in collector:
                view.SetElementOverrides(elem.Id, default)

        _run_transaction(doc, "Clear Overrides", action)
        log.info("cleared graphic overrides")

    def _cmd_isolate_by_level_category(self, params):
        # type: (dict[str, JsonValue]) -> None
        """Isolate elements matching a level name and category name."""
        level_name = params.get("level", "")
        category_name = params.get("category", "")
        doc = HOST_APP.doc
        view = self._active_view()

        def reset_action():
            # type: () -> None
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)

        _run_transaction(doc, "Reset Before Isolate", reset_action)
        id_list = _ids_for_level_category(doc, view, level_name, category_name)
        _isolate_id_list(doc, view, id_list, level_name, category_name)

    def _cmd_create_warning_view(self, params):
        # type: (dict[str, JsonValue]) -> None
        """Temporarily isolate the warning elements in the active view."""
        ids = self._resolve_ids(params)
        if not ids:
            log.warning("no valid warning element ids")
            return

        view = self._active_view()

        def action():
            # type: () -> None
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)
            view.IsolateElementsTemporary(ids)

        _run_transaction(HOST_APP.doc, "Warning View", action)
        HOST_APP.uidoc.Selection.SetElementIds(ids)
        log.info("warning view isolates {0} elements".format(ids.Count))

    def _cmd_apply_group_overrides(self, params):
        # type: (dict[str, JsonValue]) -> None
        """Clear overrides, paint group colors, and optionally isolate.

        ``groups`` is a list of ``{element_ids, color}``.
        ``isolate_ids`` isolates those ids when it is a non-empty list.
        """
        doc = HOST_APP.doc
        view = self._active_view()
        groups = params.get("groups", [])
        isolate_ids_raw = params.get("isolate_ids")

        def action():
            # type: () -> None
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)

            default = DB.OverrideGraphicSettings()
            for elem in DB.FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType():
                view.SetElementOverrides(elem.Id, default)

            for group in groups:
                red, green, blue = group["color"]
                override = self._build_color_override(DB.Color(int(red), int(green), int(blue)))
                for eid in group["element_ids"]:
                    view.SetElementOverrides(DB.ElementId(int(eid)), override)

            if isolate_ids_raw:
                id_list = List[DB.ElementId]()
                for eid in isolate_ids_raw:
                    id_list.Add(DB.ElementId(int(eid)))
                if id_list.Count > 0:
                    view.IsolateElementsTemporary(id_list)

        _run_transaction(doc, "Schedule Color Overrides", action)
        total_overridden = sum(len(group.get("element_ids", [])) for group in groups)
        log.info(
            "applied group overrides to {0} elements across {1} groups".format(
                total_overridden, len(groups)
            )
        )

    def _cmd_reset_schedule_mode(self, _params):
        # type: (dict[str, JsonValue]) -> None
        """Clear temporary isolation and graphic overrides."""
        view = self._active_view()
        doc = HOST_APP.doc

        def action():
            # type: () -> None
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)
            default = DB.OverrideGraphicSettings()
            for elem in DB.FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType():
                view.SetElementOverrides(elem.Id, default)

        _run_transaction(doc, "Reset Schedule Mode", action)
        log.info("reset schedule mode")

    def _cmd_get_element_parameters(self, params):
        # type: (dict[str, JsonValue]) -> dict[str, JsonValue]
        """Return all parameters for one element, grouped by parameter group."""
        eid = params.get("element_id")
        if eid is None:
            return {"parameters": {}}

        doc = HOST_APP.doc
        elem = doc.GetElement(DB.ElementId(int(eid)))  # type: DB.Element
        if elem is None:
            return {"parameters": {}}

        return {"parameters": _group_parameters(elem)}

    def _resolve_ids(self, params):
        # type: (dict[str, JsonValue]) -> List[DB.ElementId] | None
        """Return a .NET ``List[DB.ElementId]`` of ids that still exist."""
        raw = params.get("element_ids", [])
        if not raw:
            return None

        doc = HOST_APP.doc
        id_list = List[DB.ElementId]()
        for eid in raw:
            eid_obj = DB.ElementId(int(eid))
            if doc.GetElement(eid_obj) is not None:
                id_list.Add(eid_obj)
        return id_list if id_list.Count > 0 else None

    def _active_view(self):
        # type: () -> DB.View
        view = HOST_APP.uidoc.ActiveView  # type: DB.View
        if view is None:
            raise ValueError("No active view")
        return view

    def _build_color_override(self, color):
        # type: (DB.Color) -> DB.OverrideGraphicSettings
        override = DB.OverrideGraphicSettings()
        override.SetSurfaceForegroundPatternColor(color)
        override.SetProjectionLineColor(color)

        doc = HOST_APP.doc
        patterns = DB.FilteredElementCollector(doc).OfClass(DB.FillPatternElement)
        solid = next(
            (
                pattern
                for pattern in patterns.ToElements()
                if pattern.GetFillPattern().IsSolidFill
            ),
            None,
        )  # type: DB.FillPatternElement | None
        if solid:
            override.SetSurfaceForegroundPatternId(solid.Id)
            override.SetSurfaceForegroundPatternVisible(True)
        return override


def _clean_group_name(raw):
    # type: (str) -> str
    """Turn a ParameterGroup enum name into a readable label."""
    if not raw or raw == "INVALID":
        return "Other"
    name = raw.replace("PG_", "").replace("_", " ").title()
    return name if name else "Other"


def _param_value_as_string(param):
    # type: (DB.Parameter) -> str
    return param.AsValueString() or ""


def _get_group_label(definition):
    # type: (DB.Definition) -> str
    """Group label via ForgeTypeId, then the legacy ParameterGroup enum."""
    try:
        group_type_id = definition.GetGroupTypeId()
        if group_type_id is not None:
            label = DB.LabelUtils.GetLabelForGroup(group_type_id)
            if label:
                return label
    except Exception:
        pass

    try:
        parameter_group = definition.ParameterGroup
        try:
            label = DB.LabelUtils.GetLabelFor(parameter_group)
            if label:
                return label
        except Exception:
            pass
        return _clean_group_name(parameter_group.ToString())
    except Exception:
        pass

    return "Other"


def _group_parameters(element):
    # type: (DB.Element) -> dict[str, list[dict[str, str | bool]]]
    """Group ``element.Parameters`` for the properties pane."""
    groups = {}  # type: dict[str, list[dict[str, str | bool]]]
    for param in element.Parameters:
        parameter = param  # type: DB.Parameter
        definition = parameter.Definition  # type: DB.Definition
        if definition is None:
            continue
        name = definition.Name
        if not name:
            continue

        group_label = _get_group_label(definition)
        if group_label not in groups:
            groups[group_label] = []

        groups[group_label].append(
            {
                "name": name,
                "value": _param_value_as_string(parameter) if parameter.HasValue else "",
                "is_readonly": bool(parameter.IsReadOnly),
                "storage_type": parameter.StorageType.ToString(),
            }
        )

    for params_list in groups.values():
        params_list.sort(key=lambda item: item["name"])

    return groups
