"""Revit API handler for bi-directional dashboard communication.

Each public method corresponds to a frontend ``revitApi.*`` call.  The
``handle`` dispatcher uses a registry — new methods are added by writing
a ``_cmd_*`` method, not by editing an ``if/elif`` chain.

IMPORTANT: 
- Never use `with Transaction` - it does not work with pythonnet.
- Always use HOST_APP.doc to access document, never cache it.
- All commands execute inside RevitTask.RunAsync (via RevitActionDispatcher).
- TemporaryViewMode operations (IsolateElementsTemporary, DisableTemporaryViewMode)
  don't modify the model, but still require a Transaction in the Revit API.
"""

from collections.abc import Callable
from typing import TYPE_CHECKING, Any

import Autodesk.Revit.DB as DB
from Autodesk.Revit.DB import (
    Color,
    ElementId,
    FilteredElementCollector,
    OverrideGraphicSettings,
)

from revit_dashboard.context import HOST_APP

if TYPE_CHECKING:
    from revit_dashboard.contracts.payload import DashboardPayload


def _run_transaction(doc: DB.Document, name: str, action: Callable[[], None]) -> None:
    """Execute an action within a transaction.
    
    Args:
        doc: The Revit document (should be HOST_APP.doc, not cached).
        name: Transaction name.
        action: The action to execute within the transaction.
    """
    with DB.Transaction(doc, name) as t:
        t.Start()
        try:
            action()
            t.Commit()
        except Exception as e:
            print(f"[RevitAPI] Transaction error in '{name}': {e}")
            t.RollBack()


class RevitApiHandler:
    """Routes Revit API calls from the dashboard frontend.
    
    IMPORTANT: This handler uses HOST_APP.doc to always get the current document.
    Never cache the document reference.
    All _cmd_* methods run inside RevitTask.RunAsync (via RevitActionDispatcher).
    """

    def __init__(
        self,
        refresh_callback: Callable[[], DashboardPayload],
    ) -> None:
        self._refresh = refresh_callback

        # Auto-discover _cmd_* methods as handler registry
        self._commands: dict[str, Callable[[dict], Any]] = {}
        for name in dir(self):
            if name.startswith("_cmd_"):
                method_name = name[5:]  # strip "_cmd_"
                self._commands[method_name] = getattr(self, name)

    def handle(self, method: str, params: dict[str, Any]) -> dict[str, Any]:
        """Dispatch *method* to the matching ``_cmd_*`` handler."""
        cmd = self._commands.get(method)
        if cmd is None:
            return {"ok": False, "error": f"Unknown method: {method}"}
        try:
            result = cmd(params)
            return {"ok": True, "data": result}
        except Exception as exc:
            print(f"[RevitAPI] Error in {method}: {exc}")
            return {"ok": False, "error": str(exc)}

    # -- commands (auto-registered via naming convention) --------------------

    def _cmd_select(self, params: dict[str, Any]) -> None:
        ids = self._resolve_ids(params)
        if ids:
            HOST_APP.uidoc.Selection.SetElementIds(ids)
            print(f"[RevitAPI] Selected {ids.Count} elements")

    def _cmd_zoom(self, params: dict[str, Any]) -> None:
        ids = self._resolve_ids(params)
        if ids:
            HOST_APP.uidoc.Selection.SetElementIds(ids)
            HOST_APP.uidoc.ShowElements(ids)
            print(f"[RevitAPI] Zoomed to {ids.Count} elements")

    def _cmd_isolate(self, params: dict[str, Any]) -> None:
        ids = self._resolve_ids(params)
        view = self._active_view()
        if ids:
            def action():
                # Reset previous temp view state before applying new isolation
                view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)
                view.IsolateElementsTemporary(ids)

            _run_transaction(HOST_APP.doc, "Isolate Elements", action)
            print(f"[RevitAPI] Isolated {ids.Count} elements")

    def _cmd_resetIsolation(self, _params: dict[str, Any]) -> None:  # noqa: N802
        view = self._active_view()

        def action():
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)

        _run_transaction(HOST_APP.doc, "Reset Isolation", action)
        print("[RevitAPI] Reset isolation")

    def _cmd_colorOverride(self, params: dict[str, Any]) -> None:  # noqa: N802
        ids = self._resolve_ids(params)
        view = self._active_view()
        r, g, b = params.get("color", [255, 0, 0])
        override = self._build_color_override(Color(int(r), int(g), int(b)))

        if ids:
            def action():
                for i in range(ids.Count):
                    view.SetElementOverrides(ids[i], override)

            _run_transaction(HOST_APP.doc, "Color Override", action)
            print(f"[RevitAPI] Color override on {ids.Count} elements")

    def _cmd_clearOverrides(self, _params: dict[str, Any]) -> None:  # noqa: N802
        view = self._active_view()
        doc = HOST_APP.doc
        collector = FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType()
        default = OverrideGraphicSettings()

        def action():
            for elem in collector:
                if elem.Id.IntegerValue > 0:
                    view.SetElementOverrides(elem.Id, default)

        _run_transaction(doc, "Clear Overrides", action)
        print("[RevitAPI] Cleared all overrides")

    def _cmd_isolateByLevelCategory(self, params: dict[str, Any]) -> None:  # noqa: N802
        """Isolate elements matching a given level + category in the active view."""
        level_name = params.get("level", "")
        category_name = params.get("category", "")
        doc = HOST_APP.doc
        view = self._active_view()

        from System.Collections.Generic import List

        # STEP 1: Reset previous isolation FIRST so ALL elements become visible
        # for the collector. Without this, FilteredElementCollector(doc, view.Id)
        # only sees the currently isolated subset.
        def reset_action():
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)

        _run_transaction(doc, "Reset Before Isolate", reset_action)

        # STEP 2: Now collect from the fully-visible view
        all_elements = (
            FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
        )

        id_list = List[ElementId]()
        for elem in all_elements:
            # Check category
            elem_cat = getattr(elem, "Category", None)
            if elem_cat is None:
                continue
            if category_name and (elem_cat.Name or "") != category_name:
                continue

            # Check level (via LevelId or FAMILY_LEVEL_PARAM)
            if level_name:
                matched_level = False
                level_param = elem.get_Parameter(DB.BuiltInParameter.FAMILY_LEVEL_PARAM)
                if level_param and level_param.HasValue:
                    level_elem = doc.GetElement(level_param.AsElementId())
                    if level_elem and (level_elem.Name or "") == level_name:
                        matched_level = True
                if not matched_level:
                    elem_level_id = getattr(elem, "LevelId", None)
                    if elem_level_id and elem_level_id.IntegerValue > 0:
                        level_elem = doc.GetElement(elem_level_id)
                        if level_elem and (level_elem.Name or "") == level_name:
                            matched_level = True
                if not matched_level:
                    continue

            id_list.Add(elem.Id)

        # STEP 3: Apply new isolation
        if id_list.Count > 0:
            def isolate_action():
                view.IsolateElementsTemporary(id_list)

            _run_transaction(doc, "Isolate By Level/Category", isolate_action)
            print(f"[RevitAPI] Isolated {id_list.Count} elements for {level_name}/{category_name}")
        else:
            print(f"[RevitAPI] No elements found for {level_name}/{category_name}")

    def _cmd_createWarningView(self, params: dict[str, Any]) -> None:  # noqa: N802
        """Create a temporary isolation view highlighting warning elements."""
        ids = self._resolve_ids(params)
        if not ids:
            print("[RevitAPI] No valid warning element IDs")
            return

        view = self._active_view()

        def action():
            # Reset previous temp view state before applying new isolation
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)
            view.IsolateElementsTemporary(ids)

        _run_transaction(HOST_APP.doc, "Warning View", action)
        HOST_APP.uidoc.Selection.SetElementIds(ids)
        print(f"[RevitAPI] Created warning view isolating {ids.Count} elements")

    def _cmd_applyGroupOverrides(self, params: dict[str, Any]) -> None:  # noqa: N802
        """Batch color override: clear old overrides, apply group colors, optionally isolate.

        params:
          groups: [{ element_ids: [...], color: [r,g,b] }, ...]
          isolate_ids: [...] | null  -- if provided, isolate only these
        """
        from System.Collections.Generic import List

        doc = HOST_APP.doc
        view = self._active_view()
        groups = params.get("groups", [])
        isolate_ids_raw = params.get("isolate_ids")

        def action():
            # 1. Reset temp view mode
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)

            # 2. Clear existing overrides on all visible model elements
            default = OverrideGraphicSettings()
            for elem in FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType():
                if elem.Id.IntegerValue > 0:
                    view.SetElementOverrides(elem.Id, default)

            # 3. Apply group color overrides
            for group in groups:
                r, g, b = group["color"]
                override = self._build_color_override(Color(int(r), int(g), int(b)))
                for eid in group["element_ids"]:
                    view.SetElementOverrides(ElementId(int(eid)), override)

            # 4. Optionally isolate
            if isolate_ids_raw:
                id_list = List[ElementId]()
                for eid in isolate_ids_raw:
                    id_list.Add(ElementId(int(eid)))
                if id_list.Count > 0:
                    view.IsolateElementsTemporary(id_list)

        _run_transaction(doc, "Schedule Color Overrides", action)
        total_overridden = sum(len(g.get("element_ids", [])) for g in groups)
        print(f"[RevitAPI] Applied group overrides to {total_overridden} elements across {len(groups)} groups")

    def _cmd_resetScheduleMode(self, _params: dict[str, Any]) -> None:  # noqa: N802
        """Reset temp view and clear all graphic overrides — used when leaving Schedule tab."""
        view = self._active_view()
        doc = HOST_APP.doc

        def action():
            view.DisableTemporaryViewMode(DB.TemporaryViewMode.TemporaryHideIsolate)
            default = OverrideGraphicSettings()
            for elem in FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType():
                if elem.Id.IntegerValue > 0:
                    view.SetElementOverrides(elem.Id, default)

        _run_transaction(doc, "Reset Schedule Mode", action)
        print("[RevitAPI] Reset schedule mode — cleared overrides and temp view")

    def _cmd_getElementParameters(self, params: dict[str, Any]) -> dict:  # noqa: N802
        """Fetch ALL parameters for a single element, grouped by ParameterGroup."""
        eid = params.get("element_id")
        if eid is None:
            return {"parameters": {}}

        doc = HOST_APP.doc
        elem = doc.GetElement(ElementId(int(eid)))
        if elem is None:
            return {"parameters": {}}

        grouped = _group_parameters(elem)
        return {"parameters": grouped}

    def _cmd_refresh(self, _params: dict[str, Any]) -> dict[str, Any]:
        print("[RevitAPI] Refreshing data…")
        payload = self._refresh()
        print(f"[RevitAPI] Refreshed: {payload['kpis'].get('total_elements', 0)} elements")
        return {"payload": payload}

    # -- helpers ------------------------------------------------------------

    def _resolve_ids(self, params: dict[str, Any]):
        """Return a .NET ``List[ElementId]`` of valid element IDs."""
        from System.Collections.Generic import List

        raw = params.get("element_ids", [])
        if not raw:
            return None

        doc = HOST_APP.doc
        id_list = List[ElementId]()
        for eid in raw:
            eid_obj = ElementId(int(eid))
            if doc.GetElement(eid_obj) is not None:
                id_list.Add(eid_obj)
        return id_list if id_list.Count > 0 else None

    def _active_view(self) -> DB.View:
        view = HOST_APP.uidoc.ActiveView
        if view is None:
            raise ValueError("No active view")
        return view

    def _build_color_override(self, color: Color) -> OverrideGraphicSettings:
        override = OverrideGraphicSettings()
        override.SetSurfaceForegroundPatternColor(color)
        override.SetProjectionLineColor(color)

        doc = HOST_APP.doc
        solid = next(
            (
                p
                for p in FilteredElementCollector(doc).OfClass(DB.FillPatternElement).ToElements()
                if p.GetFillPattern().IsSolidFill
            ),
            None,
        )
        if solid:
            override.SetSurfaceForegroundPatternId(solid.Id)
            override.SetSurfaceForegroundPatternVisible(True)
        return override


# ---------------------------------------------------------------------------
# Module-level helpers for parameter collection
# ---------------------------------------------------------------------------


def _clean_group_name(raw: str) -> str:
    """Convert Revit ParameterGroup enum name to readable label.
    
    e.g. 'PG_IDENTITY_DATA' -> 'Identity Data'
    """
    if not raw or raw == "INVALID":
        return "Other"
    name = raw.replace("PG_", "").replace("_", " ").title()
    return name if name else "Other"


def _param_value_as_string(param: DB.Parameter) -> str:
    """Convert a Revit Parameter to a display string regardless of storage type."""
    try:
        # Prefer AsValueString for user-friendly display (includes units)
        vs = param.AsValueString()
        if vs:
            return vs
        st = param.StorageType
        if st == DB.StorageType.String:
            return param.AsString() or ""
        if st == DB.StorageType.Integer:
            return str(param.AsInteger())
        if st == DB.StorageType.Double:
            return str(round(param.AsDouble(), 6))
        if st == DB.StorageType.ElementId:
            return str(param.AsElementId().IntegerValue)
        return ""
    except Exception:
        return ""


def _get_group_label(defn) -> str:
    """Get the human-readable parameter group label.

    Strategy (Revit 2024+ first, then fallback):
      1. ``Definition.GetGroupTypeId()``  → ``LabelUtils.GetLabelForGroup()``
         This is the modern ForgeTypeId-based API.
      2. ``Definition.ParameterGroup``     → ``LabelUtils.GetLabelFor()``
         Legacy enum-based API (deprecated but still works).
      3. String cleanup of the raw enum name.
    """
    # --- Modern API: GetGroupTypeId (Revit 2024+) ---
    try:
        group_type_id = defn.GetGroupTypeId()
        if group_type_id is not None:
            label = DB.LabelUtils.GetLabelForGroup(group_type_id)
            if label:
                return label
    except Exception:
        pass  # Method not available in older Revit

    # --- Legacy API: ParameterGroup enum ---
    try:
        pg = defn.ParameterGroup
        # Try LabelUtils first (returns localised string)
        try:
            label = DB.LabelUtils.GetLabelFor(pg)
            if label:
                return label
        except Exception:
            pass
        # Raw enum name as last resort
        raw = str(pg)
        return _clean_group_name(raw)
    except Exception:
        pass

    return "Other"


def _group_parameters(element: DB.Element) -> dict[str, list[dict]]:
    """Iterate element.Parameters and group by ParameterGroup for the Properties Pane.

    Uses the modern ``GetGroupTypeId`` + ``LabelUtils.GetLabelForGroup`` API
    (Revit 2024+) with automatic fallback to the legacy ``ParameterGroup`` enum.
    """
    groups: dict[str, list[dict]] = {}
    for param in element.Parameters:
        defn = param.Definition
        if defn is None:
            continue
        name = defn.Name
        if not name:
            continue

        group_label = _get_group_label(defn)

        if group_label not in groups:
            groups[group_label] = []

        groups[group_label].append({
            "name": name,
            "value": _param_value_as_string(param) if param.HasValue else "",
            "is_readonly": bool(param.IsReadOnly),
            "storage_type": str(param.StorageType),
        })

    # Sort parameters within each group alphabetically
    for params_list in groups.values():
        params_list.sort(key=lambda p: p["name"])

    return groups
