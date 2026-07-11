"""MEP placement and system tools."""
from __future__ import annotations

from Autodesk.Revit import DB

from dto.mep import (
    ConduitSpec,
    DuctSpec,
    InsulateDuctResult,
    ListMepSystemsResult,
    MepSystemItem,
    PipeSpec,
    PlaceSegmentResult,
)
from shared.element_helpers import element_id_value, normalize_string, require_doc
from shared.responses import ToolError
from shared.transactions import run_transaction


class MepService:
    def place_duct(self, spec: DuctSpec) -> PlaceSegmentResult:
        doc = require_doc()
        start = DB.XYZ(spec.start[0], spec.start[1], spec.start[2])
        end = DB.XYZ(spec.end[0], spec.end[1], spec.end[2])
        length = start.DistanceTo(end)

        def _operation():
            duct = DB.Mechanical.Duct.Create(
                doc,
                DB.ElementId(spec.system_type_id),
                DB.ElementId(spec.duct_type_id),
                DB.ElementId(spec.level_id),
                start,
                end,
            )
            if spec.width and spec.width > 0:
                duct.LookupParameter("Width").Set(spec.width)
            if spec.height and spec.height > 0:
                duct.LookupParameter("Height").Set(spec.height)
            if spec.diameter and spec.diameter > 0:
                duct.LookupParameter("Diameter").Set(spec.diameter)
            return duct

        duct = run_transaction(doc, "MCP: revit_place_duct", _operation)
        return PlaceSegmentResult(elementId=element_id_value(duct.Id), length=length)

    def place_pipe(self, spec: PipeSpec) -> PlaceSegmentResult:
        doc = require_doc()
        start = DB.XYZ(spec.start[0], spec.start[1], spec.start[2])
        end = DB.XYZ(spec.end[0], spec.end[1], spec.end[2])
        length = start.DistanceTo(end)

        def _operation():
            pipe = DB.Plumbing.Pipe.Create(
                doc,
                DB.ElementId(spec.system_type_id),
                DB.ElementId(spec.pipe_type_id),
                DB.ElementId(spec.level_id),
                start,
                end,
            )
            if spec.diameter > 0:
                pipe.LookupParameter("Diameter").Set(spec.diameter)
            return pipe

        pipe = run_transaction(doc, "MCP: revit_place_pipe", _operation)
        return PlaceSegmentResult(elementId=element_id_value(pipe.Id), length=length)

    def place_conduit(self, spec: ConduitSpec) -> PlaceSegmentResult:
        doc = require_doc()
        start = DB.XYZ(spec.start[0], spec.start[1], spec.start[2])
        end = DB.XYZ(spec.end[0], spec.end[1], spec.end[2])
        length = start.DistanceTo(end)

        def _operation():
            conduit = DB.Electrical.Conduit.Create(
                doc,
                DB.ElementId(spec.conduit_type_id),
                start,
                end,
                DB.ElementId(spec.level_id),
            )
            if spec.diameter > 0:
                conduit.LookupParameter("Diameter").Set(spec.diameter)
            return conduit

        conduit = run_transaction(doc, "MCP: revit_place_conduit", _operation)
        return PlaceSegmentResult(elementId=element_id_value(conduit.Id), length=length)

    def list_mep_systems(self, kind: str = "all") -> ListMepSystemsResult:
        doc = require_doc()
        normalized = kind.strip().lower()
        systems: list[MepSystemItem] = []
        if normalized in ("duct", "all"):
            systems.extend(self._list_duct_systems(doc))
        if normalized in ("pipe", "all"):
            systems.extend(self._list_pipe_systems(doc))
        if normalized in ("electrical", "all"):
            systems.extend(self._list_electrical_systems(doc))
        if not systems and normalized not in ("duct", "pipe", "electrical", "all"):
            raise ToolError("Invalid kind '{}'. Expected duct, pipe, electrical, or all".format(kind))
        return ListMepSystemsResult(systems=systems)

    def insulate_duct_system(self, system_id: int, thickness_mm: float) -> InsulateDuctResult:
        doc = require_doc()
        system = doc.GetElement(DB.ElementId(system_id))
        if system is None or not isinstance(system, DB.Mechanical.MechanicalSystem):
            raise ToolError("Mechanical system {} not found".format(system_id))

        insulation_type = (
            DB.FilteredElementCollector(doc).OfClass(DB.Mechanical.DuctInsulationType).FirstElementId()
        )
        if insulation_type is None or insulation_type == DB.ElementId.InvalidElementId:
            raise ToolError("No duct insulation type found in the document")

        thickness_ft = thickness_mm / 304.8
        insulated = 0

        def _operation():
            nonlocal insulated
            for element in system.DuctNetwork:
                try:
                    if isinstance(element, DB.Mechanical.Duct):
                        DB.Mechanical.DuctInsulation.Create(doc, element.Id, insulation_type, thickness_ft)
                        insulated += 1
                except Exception:
                    continue

        run_transaction(doc, "MCP: revit_insulate_duct_system", _operation)
        return InsulateDuctResult(insulated_count=insulated)

    def _list_duct_systems(self, doc: DB.Document) -> list[MepSystemItem]:
        items = []
        for system in DB.FilteredElementCollector(doc).OfClass(DB.Mechanical.MechanicalSystem).ToElements():
            count = system.DuctNetwork.Size if system.DuctNetwork else 0
            type_label = str(system.SystemType).replace("SystemType", "")
            items.append(
                MepSystemItem(
                    id=element_id_value(system.Id),
                    name=normalize_string(system.Name),
                    type=type_label,
                    element_count=count,
                    classification=type_label.split()[0] if type_label else "Supply",
                )
            )
        return items

    def _list_pipe_systems(self, doc: DB.Document) -> list[MepSystemItem]:
        items = []
        for system in DB.FilteredElementCollector(doc).OfClass(DB.Plumbing.PipingSystem).ToElements():
            count = system.PipingNetwork.Size if system.PipingNetwork else 0
            type_label = str(system.SystemType).replace("SystemType", "")
            items.append(
                MepSystemItem(
                    id=element_id_value(system.Id),
                    name=normalize_string(system.Name),
                    type=type_label,
                    element_count=count,
                    classification=type_label.split()[0] if type_label else "Domestic",
                )
            )
        return items

    def _list_electrical_systems(self, doc: DB.Document) -> list[MepSystemItem]:
        items = []
        for system in DB.FilteredElementCollector(doc).OfClass(DB.Electrical.ElectricalSystem).ToElements():
            count = system.Elements.Size if system.Elements else 0
            type_label = str(system.SystemType)
            items.append(
                MepSystemItem(
                    id=element_id_value(system.Id),
                    name=normalize_string(system.Name),
                    type=type_label,
                    element_count=count,
                    classification=str(system.CircuitType),
                )
            )
        return items
