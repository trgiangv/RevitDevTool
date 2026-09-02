"""Element CRUD operations: parameters, transforms, placement, highlight."""

import math

from Autodesk.Revit import DB, UI
from RevitDevTool.Core import RevitContext
from System.Collections.Generic import List

from dto.common import ToolErrorEntry
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
from shared.element_helpers import (
    find_family_symbol_safely,
    require_doc,
)
from shared.operation_outcome import OperationOutcome
from shared.parameter_accessor import (
    change_element_type,
    get_parameter_value,
    set_parameter_value,
)
from shared.responses import ToolError
from shared.transactions import run_transaction

_DELETE_THRESHOLD = 50


def _delete_threshold_blocked(
    element_ids: list[int], dry_run: bool
) -> DeleteElementsResult | None:
    if dry_run or len(element_ids) <= _DELETE_THRESHOLD:
        return None
    return DeleteElementsResult(
        deleted_count=0,
        warning=(
            f"Deleting {len(element_ids)} elements exceeds the confirmation threshold of {_DELETE_THRESHOLD}. "
            "Set dryRun=true to preview, then retry with fewer elements."
        ),
    )


def _try_delete_element(
    doc: DB.Document,
    eid: int,
    dry_run: bool,
    failures: list[dict],
    dry_results: list[dict],
) -> int:
    try:
        elem = doc.GetElement(DB.ElementId(eid))
        if elem is None:
            failures.append(
                {"elementId": eid, "message": f"Element {eid} not found"}
            )
            return 0
        deleted = doc.Delete(elem.Id)
        if dry_run:
            dry_results.append(
                {
                    "requestedId": eid,
                    "wouldDelete": [int(item_id.Value) for item_id in deleted],
                }
            )
            return 0
        return len(deleted)
    except Exception as exc:
        failures.append({"elementId": eid, "message": str(exc)})
        return 0


def _finalize_delete_transaction(tx: DB.Transaction, dry_run: bool) -> None:
    if dry_run:
        tx.RollBack()
    else:
        tx.Commit()


def _clone_parameters_to_target(
    target: DB.Element,
    element_id: int,
    source_values: dict[str, str],
    skipped: list[dict],
) -> int:
    copied = 0
    for pname, value in source_values.items():
        success, message = set_parameter_value(target, pname, value)
        if success:
            copied += 1
        else:
            skipped.append(
                {
                    "elementId": element_id,
                    "paramName": pname,
                    "reason": message,
                }
            )
    return copied


class ElementService:
    @staticmethod
    def write_parameters(
        element_ids: list[int], updates: list[ParameterUpdate]
    ) -> WriteParametersResult:
        if not element_ids:
            raise ToolError("No element IDs provided.")
        if not updates:
            raise ToolError("No parameter updates provided")
        doc = require_doc()
        outcome = OperationOutcome()

        def _operation() -> None:
            for eid in element_ids:
                elem = doc.GetElement(DB.ElementId(eid))
                if elem is None:
                    outcome.record_failure(eid, f"Element {eid} not found")
                    continue
                for update in updates:
                    success, message = set_parameter_value(
                        elem, update.param_name, str(update.value)
                    )
                    outcome.record(success, message, eid)

        run_transaction(doc, "MCP: revit_write_parameters", _operation)
        result = outcome.summarize()
        return WriteParametersResult.model_validate(result)

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
            raise ToolError(
                "Family '{}' type '{}' not found".format(
                    family_name, type_name or "Any"
                )
            )
        family_symbol = symbol

        created: list[CreatedInstance] = []
        failures = []

        def _operation() -> None:
            if not family_symbol.IsActive:
                family_symbol.Activate()
                doc.Regenerate()
            for i, placement in enumerate(placements):
                ElementService._place_one_instance(
                    doc,
                    family_symbol,
                    i,
                    placement,
                    properties,
                    created,
                    failures,
                )

        run_transaction(doc, "MCP: revit_place_family", _operation)
        return PlaceFamilyResult(created=created, failures=failures or None)

    @staticmethod
    def move_elements(
        element_ids: list[int], vector: list[float]
    ) -> MoveElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided.")
        if len(vector) != 3:
            raise ToolError("Vector must have exactly 3 components [X, Y, Z]")
        doc = require_doc()
        translation = DB.XYZ(vector[0], vector[1], vector[2])
        failures = []
        moved = 0

        def _operation() -> None:
            nonlocal moved
            for eid in element_ids:
                try:
                    elem = doc.GetElement(DB.ElementId(eid))
                    if elem is None:
                        failures.append(
                            ToolErrorEntry.from_message(f"Element {eid} not found", eid)
                        )
                        continue
                    DB.ElementTransformUtils.MoveElement(doc, elem.Id, translation)
                    moved += 1
                except Exception as exc:
                    failures.append(ToolErrorEntry.from_exception(exc, eid))

        run_transaction(doc, "MCP: revit_move_elements", _operation)
        return MoveElementsResult(moved_count=moved, failures=failures or None)

    @staticmethod
    def rotate_elements(
        element_ids: list[int],
        axis_origin: list[float],
        axis_direction: list[float],
        degrees: float,
    ) -> RotateElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided.")
        if len(axis_origin) != 3 or len(axis_direction) != 3:
            raise ToolError("Axis origin and direction must have 3 components")
        doc = require_doc()
        radians = degrees * math.pi / 180.0
        origin = DB.XYZ(axis_origin[0], axis_origin[1], axis_origin[2])
        direction = DB.XYZ(axis_direction[0], axis_direction[1], axis_direction[2])
        failures = []
        rotated = 0

        def _operation() -> None:
            nonlocal rotated
            axis = DB.Line.CreateBound(origin, origin + direction)
            for eid in element_ids:
                try:
                    elem = doc.GetElement(DB.ElementId(eid))
                    if elem is None:
                        failures.append(
                            ToolErrorEntry.from_message(f"Element {eid} not found", eid)
                        )
                        continue
                    DB.ElementTransformUtils.RotateElement(doc, elem.Id, axis, radians)
                    rotated += 1
                except Exception as exc:
                    failures.append(ToolErrorEntry.from_exception(exc, eid))

        run_transaction(doc, "MCP: revit_rotate_elements", _operation)
        return RotateElementsResult(rotated_count=rotated, failures=failures or None)

    @staticmethod
    def delete_elements(
        element_ids: list[int], dry_run: bool = False
    ) -> DeleteElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided.")
        blocked = _delete_threshold_blocked(element_ids, dry_run)
        if blocked is not None:
            return blocked

        doc = require_doc()
        failures: list[dict] = []
        dry_results: list[dict] = []
        deleted_count = 0

        def _operation() -> None:
            nonlocal deleted_count
            for eid in element_ids:
                deleted_count += _try_delete_element(
                    doc, eid, dry_run, failures, dry_results
                )

        with DB.Transaction(doc, "MCP: revit_delete_elements") as tx:  # noqa
            tx.Start()
            try:
                _operation()
                _finalize_delete_transaction(tx, dry_run)
            except Exception:
                tx.RollBack()
                raise

        return DeleteElementsResult(
            deleted_count=deleted_count,
            failures=failures or None,
            dryRunResults=dry_results or None,
        )

    @staticmethod
    def clone_parameters(
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
            raise ToolError(f"Source element {source_id} not found")

        source_values, skipped = ElementService._read_source_parameter_values(
            source, doc, param_names
        )
        success_count = 0

        def _operation() -> None:
            nonlocal success_count
            for eid in target_ids:
                target = doc.GetElement(DB.ElementId(eid))
                if target is None:
                    skipped.append(
                        {"elementId": eid, "reason": "Target element not found"}
                    )
                    continue
                success_count += _clone_parameters_to_target(
                    target, eid, source_values, skipped
                )

        run_transaction(doc, "MCP: revit_clone_parameters", _operation)
        return CloneParametersResult(success_count=success_count, skipped=skipped)

    @staticmethod
    def swap_type(element_ids: list[int], new_type_id: int) -> SwapTypeResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        doc = require_doc()
        new_type = doc.GetElement(DB.ElementId(new_type_id))
        if new_type is None:
            raise ToolError(f"Type element {new_type_id} not found")
        outcome = OperationOutcome()

        def _operation() -> None:
            for eid in element_ids:
                elem = doc.GetElement(DB.ElementId(eid))
                if elem is None:
                    outcome.record_failure(eid, f"Element {eid} not found")
                    continue
                success, message = change_element_type(elem, new_type_id)
                outcome.record(success, message, eid)

        run_transaction(doc, "MCP: revit_swap_type", _operation)
        result = outcome.summarize()
        return SwapTypeResult.model_validate(result)

    @staticmethod
    def highlight_elements(element_ids: list[int]) -> HighlightElementsResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        doc = require_doc()
        ui_doc : UI.UIDocument = RevitContext.ActiveUiDocument # noqa
        if ui_doc is None:
            raise ToolError("No active UI document")
        ids = List[DB.ElementId]()
        for eid in element_ids:
            if doc.GetElement(DB.ElementId(eid)) is not None:
                ids.Add(DB.ElementId(eid))
        ui_doc.Selection.SetElementIds(ids)
        return HighlightElementsResult(selected_count=ids.Count)

    @staticmethod
    def _read_source_parameter_values(
        source: DB.Element,
        doc: DB.Document,
        param_names: list[str],
    ) -> tuple[dict[str, str], list[dict]]:
        source_values: dict[str, str] = {}
        skipped: list[dict] = []
        for name in param_names:
            param = source.LookupParameter(name)
            if param is None:
                skipped.append(
                    {
                        "paramName": name,
                        "reason": "Parameter not found on source element",
                    }
                )
                continue
            source_values[name] = get_parameter_value(param, doc)
        return source_values, skipped

    @staticmethod
    def _place_one_instance(
        doc: DB.Document,
        symbol: DB.FamilySymbol,
        index: int,
        placement: PlacementSpec,
        properties: dict | None,
        created: list[CreatedInstance],
        failures: list[ToolErrorEntry],
    ) -> None:
        try:
            point = DB.XYZ(placement.x, placement.y, placement.z)
            level = ElementService._resolve_level(doc, placement.level_name)
            instance = ElementService._create_instance(
                doc, symbol, point, level, placement.host_id
            )
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
                                f"Placement {index}: {message}",
                                int(instance.Id.Value),
                            )
                        )
            loc = ElementService._instance_location(instance, point)
            created.append(
                CreatedInstance(id=int(instance.Id.Value), location=loc)
            )
        except Exception as exc:
            failures.append(ToolErrorEntry.from_message(f"Placement {index}: {exc}"))

    @staticmethod
    def _resolve_level(doc: DB.Document, level_name: str | None) -> DB.Level | None:
        if not level_name:
            return None
        for level in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
            if (level.Name or "") == (level_name or ""):
                return level
        raise ToolError(f"Level '{level_name}' not found")

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
                raise ToolError(f"Host element {host_id} not found")
            return doc.Create.NewFamilyInstance(
                point, symbol, host, DB.Structure.StructuralType.NonStructural
            )
        if level is not None:
            return doc.Create.NewFamilyInstance(
                point, symbol, level, DB.Structure.StructuralType.NonStructural
            )
        return doc.Create.NewFamilyInstance(
            point, symbol, DB.Structure.StructuralType.NonStructural
        )

    @staticmethod
    def _instance_location(
        instance: DB.FamilyInstance, fallback: DB.XYZ
    ) -> dict[str, float]:
        try:
            location = instance.Location
            if isinstance(location, DB.LocationPoint):
                pt = location.Point
            else:
                pt = fallback
            return {"x": pt.X, "y": pt.Y, "z": pt.Z}
        except Exception:
            return {"x": fallback.X, "y": fallback.Y, "z": fallback.Z}
