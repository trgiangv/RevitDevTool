"""Element CRUD operations: parameters, transforms, placement, highlight."""
from __future__ import annotations

import math

from System.Collections.Generic import List
from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

from dto.crud import (
    CloneParametersResult,
    CreatedInstance,
    DeleteElementsResult,
    HighlightElementsResult,
    MoveElementsResult,
    ParameterUpdate,
    PlaceFamilyResult,
    PlacementSpec,
    RotateElementsResult,
    SwapTypeResult,
    WriteParametersResult,
)
from dto.common import ToolErrorEntry
from shared.element_helpers import (
    element_id_value,
    find_family_symbol_safely,
    normalize_string,
    require_doc,
)
from shared.operation_outcome import OperationOutcome
from shared.parameter_accessor import change_element_type, get_parameter_value, set_parameter_value
from shared.responses import ToolError
from shared.transactions import run_transaction

_DELETE_THRESHOLD = 50


class ElementService:
    def write_parameters(self, element_ids: list[int], updates: list[ParameterUpdate]) -> WriteParametersResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        if not updates:
            raise ToolError("No parameter updates provided")
        doc = require_doc()
        outcome = OperationOutcome()

        def _operation():
            for eid in element_ids:
                elem = doc.GetElement(DB.ElementId(eid))
                if elem is None:
                    outcome.record_failure(eid, "Element {} not found".format(eid))
                    continue
                for update in updates:
                    success, message = set_parameter_value(elem, update.param_name, str(update.value))
                    outcome.record(success, message, eid)

        run_transaction(doc, "MCP: revit_write_parameters", _operation)
        result = outcome.summarize()
        return WriteParametersResult(**result)

    def place_family(
        self,
        family_name: str,
        type_name: str | None,
        placements: list[PlacementSpec],
        properties: dict | None = None,
    ) -> PlaceFamilyResult:
        if not family_name.strip():
            raise ToolError("Family name is required")
        if not placements:
            raise ToolError("At least one placement is required")
        doc = require_doc()
        symbol = find_family_symbol_safely(doc, family_name, type_name)
        if symbol is None:
            raise ToolError("Family '{}' type '{}' not found".format(family_name, type_name or "Any"))

        created: list[CreatedInstance] = []
        failures = []

        def _operation():
            if not symbol.IsActive:
                symbol.Activate()
                doc.Regenerate()
            for i, placement in enumerate(placements):
                try:
                    point = DB.XYZ(placement.x, placement.y, placement.z)
                    level = self._resolve_level(doc, placement.level_name)
                    instance = self._create_instance(doc, symbol, point, level, placement.host_id)
                    if placement.rotation:
                        radians = placement.rotation * math.pi / 180.0
                        axis = DB.Line.CreateBound(point, point.Add(DB.XYZ(0, 0, 1)))
                        DB.ElementTransformUtils.RotateElement(doc, instance.Id, axis, radians)
                    if properties:
                        for key, value in properties.items():
                            success, message = set_parameter_value(instance, key, str(value))
                            if not success:
                                failures.append(
                                    ToolErrorEntry.from_message(
                                        "Placement {}: {}".format(i, message),
                                        element_id_value(instance.Id),
                                    )
                                )
                    loc = self._instance_location(instance, point)
                    created.append(CreatedInstance(id=element_id_value(instance.Id), location=loc))
                except Exception as exc:
                    failures.append(ToolErrorEntry.from_message("Placement {}: {}".format(i, exc)))

        run_transaction(doc, "MCP: revit_place_family", _operation)
        return PlaceFamilyResult(created=created, failures=failures or None)

    def move_elements(self, element_ids: list[int], vector: list[float]) -> MoveElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        if len(vector) != 3:
            raise ToolError("Vector must have exactly 3 components [X, Y, Z]")
        doc = require_doc()
        translation = DB.XYZ(vector[0], vector[1], vector[2])
        failures = []
        moved = 0

        def _operation():
            nonlocal moved
            for eid in element_ids:
                try:
                    elem = doc.GetElement(DB.ElementId(eid))
                    if elem is None:
                        failures.append({"elementId": eid, "message": "Element {} not found".format(eid)})
                        continue
                    DB.ElementTransformUtils.MoveElement(doc, elem.Id, translation)
                    moved += 1
                except Exception as exc:
                    failures.append({"elementId": eid, "message": str(exc)})

        run_transaction(doc, "MCP: revit_move_elements", _operation)
        return MoveElementsResult(moved_count=moved, failures=failures or None)

    def rotate_elements(
        self,
        element_ids: list[int],
        axis_origin: list[float],
        axis_direction: list[float],
        degrees: float,
    ) -> RotateElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        if len(axis_origin) != 3 or len(axis_direction) != 3:
            raise ToolError("Axis origin and direction must have 3 components")
        doc = require_doc()
        radians = degrees * math.pi / 180.0
        origin = DB.XYZ(axis_origin[0], axis_origin[1], axis_origin[2])
        direction = DB.XYZ(axis_direction[0], axis_direction[1], axis_direction[2])
        failures = []
        rotated = 0

        def _operation():
            nonlocal rotated
            axis = DB.Line.CreateBound(origin, origin + direction)
            for eid in element_ids:
                try:
                    elem = doc.GetElement(DB.ElementId(eid))
                    if elem is None:
                        failures.append({"elementId": eid, "message": "Element {} not found".format(eid)})
                        continue
                    DB.ElementTransformUtils.RotateElement(doc, elem.Id, axis, radians)
                    rotated += 1
                except Exception as exc:
                    failures.append({"elementId": eid, "message": str(exc)})

        run_transaction(doc, "MCP: revit_rotate_elements", _operation)
        return RotateElementsResult(rotated_count=rotated, failures=failures or None)

    def delete_elements(self, element_ids: list[int], dry_run: bool = False) -> DeleteElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        if not dry_run and len(element_ids) > _DELETE_THRESHOLD:
            return DeleteElementsResult(
                deleted_count=0,
                warning=(
                    "Deleting {} elements exceeds the confirmation threshold of {}. "
                    "Set dryRun=true to preview, then retry with fewer elements."
                ).format(len(element_ids), _DELETE_THRESHOLD),
            )
        doc = require_doc()
        failures = []
        dry_results = []
        deleted_count = 0

        def _operation():
            nonlocal deleted_count
            for eid in element_ids:
                try:
                    elem = doc.GetElement(DB.ElementId(eid))
                    if elem is None:
                        failures.append({"elementId": eid, "message": "Element {} not found".format(eid)})
                        continue
                    deleted = doc.Delete(elem.Id)
                    if dry_run:
                        dry_results.append(
                            {"requestedId": eid, "wouldDelete": [element_id_value(d) for d in deleted]}
                        )
                    else:
                        deleted_count += len(deleted)
                except Exception as exc:
                    failures.append({"elementId": eid, "message": str(exc)})

        with DB.Transaction(doc, "MCP: revit_delete_elements") as tx:
            tx.Start()
            try:
                _operation()
                if dry_run:
                    tx.RollBack()
                else:
                    tx.Commit()
            except Exception:
                tx.RollBack()
                raise

        return DeleteElementsResult(
            deleted_count=deleted_count,
            failures=failures or None,
            dryRunResults=dry_results or None,
        )

    def clone_parameters(
        self,
        source_id: int,
        target_ids: list[int],
        param_names: list[str],
    ) -> CloneParametersResult:
        if not target_ids:
            raise ToolError("No target element IDs provided")
        if not param_names:
            raise ToolError("No parameter names provided")
        doc = require_doc()
        source = doc.GetElement(DB.ElementId(source_id))
        if source is None:
            raise ToolError("Source element {} not found".format(source_id))

        source_values: dict[str, str] = {}
        skipped = []
        for name in param_names:
            param = source.LookupParameter(name)
            if param is None:
                skipped.append({"paramName": name, "reason": "Parameter not found on source element"})
                continue
            source_values[name] = get_parameter_value(param, doc)

        success_count = 0

        def _operation():
            nonlocal success_count
            for eid in target_ids:
                target = doc.GetElement(DB.ElementId(eid))
                if target is None:
                    skipped.append({"elementId": eid, "reason": "Target element not found"})
                    continue
                for pname, value in source_values.items():
                    success, message = set_parameter_value(target, pname, value)
                    if success:
                        success_count += 1
                    else:
                        skipped.append({"elementId": eid, "paramName": pname, "reason": message})

        run_transaction(doc, "MCP: revit_clone_parameters", _operation)
        return CloneParametersResult(success_count=success_count, skipped=skipped)

    def swap_type(self, element_ids: list[int], new_type_id: int) -> SwapTypeResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        doc = require_doc()
        new_type = doc.GetElement(DB.ElementId(new_type_id))
        if new_type is None:
            raise ToolError("Type element {} not found".format(new_type_id))
        outcome = OperationOutcome()

        def _operation():
            for eid in element_ids:
                elem = doc.GetElement(DB.ElementId(eid))
                if elem is None:
                    outcome.record_failure(eid, "Element {} not found".format(eid))
                    continue
                success, message = change_element_type(elem, new_type_id)
                outcome.record(success, message, eid)

        run_transaction(doc, "MCP: revit_swap_type", _operation)
        result = outcome.summarize()
        return SwapTypeResult(**result)

    def highlight_elements(self, element_ids: list[int]) -> HighlightElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        doc = require_doc()
        ui_doc = RevitContext.ActiveUiDocument
        if ui_doc is None:
            raise ToolError("No active UI document")
        ids = List[DB.ElementId]()
        for eid in element_ids:
            if doc.GetElement(DB.ElementId(eid)) is not None:
                ids.Add(DB.ElementId(eid))
        ui_doc.Selection.SetElementIds(ids)
        return HighlightElementsResult(selected_count=ids.Count)

    @staticmethod
    def _resolve_level(doc: DB.Document, level_name: str | None) -> DB.Level | None:
        if not level_name:
            return None
        for level in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
            if normalize_string(level.Name) == normalize_string(level_name):
                return level
        raise ToolError("Level '{}' not found".format(level_name))

    @staticmethod
    def _create_instance(
        doc: DB.Document,
        symbol: DB.FamilySymbol,
        point: DB.XYZ,
        level: DB.Level | None,
        host_id: int | None,
    ) -> DB.FamilyInstance:
        if host_id:
            host = doc.GetElement(DB.ElementId(host_id))
            if host is None:
                raise ToolError("Host element {} not found".format(host_id))
            return doc.Create.NewFamilyInstance(point, symbol, host, DB.Structure.StructuralType.NonStructural)
        if level is not None:
            return doc.Create.NewFamilyInstance(
                point, symbol, level, DB.Structure.StructuralType.NonStructural
            )
        return doc.Create.NewFamilyInstance(point, symbol, DB.Structure.StructuralType.NonStructural)

    @staticmethod
    def _instance_location(instance: DB.FamilyInstance, fallback: DB.XYZ) -> dict[str, float]:
        try:
            pt = instance.Location.Point
            return {"x": pt.X, "y": pt.Y, "z": pt.Z}
        except Exception:
            return {"x": fallback.X, "y": fallback.Y, "z": fallback.Z}
