"""MEP placement and system tools."""

from typing import Callable

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
from shared.element_helpers import require_doc
from shared.responses import ToolError
from shared.transactions import run_transaction


class MepService:
    @staticmethod
    def place_duct(spec: DuctSpec) -> PlaceSegmentResult:
        return _place_linear_segment(
            spec.start,
            spec.end,
            "MCP: revit_place_duct",
            lambda doc, start, end: _create_duct(doc, spec, start, end),
        )

    @staticmethod
    def place_pipe(spec: PipeSpec) -> PlaceSegmentResult:
        return _place_linear_segment(
            spec.start,
            spec.end,
            "MCP: revit_place_pipe",
            lambda doc, start, end: _create_pipe(doc, spec, start, end),
        )

    @staticmethod
    def place_conduit(spec: ConduitSpec) -> PlaceSegmentResult:
        return _place_linear_segment(
            spec.start,
            spec.end,
            "MCP: revit_place_conduit",
            lambda doc, start, end: _create_conduit(doc, spec, start, end),
        )

    @staticmethod
    def list_mep_systems(kind: str = "all") -> ListMepSystemsResult:
        doc = require_doc()
        normalized = kind.strip().lower()
        systems: list[MepSystemItem] = []
        if normalized in ("duct", "all"):
            systems.extend(MepService._list_duct_systems(doc))
        if normalized in ("pipe", "all"):
            systems.extend(MepService._list_pipe_systems(doc))
        if normalized in ("electrical", "all"):
            systems.extend(MepService._list_electrical_systems(doc))
        if not systems and normalized not in ("duct", "pipe", "electrical", "all"):
            raise ToolError(
                f"Invalid kind '{kind}'. Expected duct, pipe, electrical, or all"
            )
        return ListMepSystemsResult(systems=systems)

    @staticmethod
    def insulate_duct_system(
        system_id: int, thickness_mm: float
    ) -> InsulateDuctResult:
        doc = require_doc()
        system: DB.Mechanical.MechanicalSystem = doc.GetElement(DB.ElementId(system_id)) # noqa
        if system is None or not isinstance(system, DB.Mechanical.MechanicalSystem):
            raise ToolError(f"Mechanical system {system_id} not found")

        insulation_type = (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.Mechanical.DuctInsulationType)
            .FirstElementId()
        )
        if insulation_type is None or insulation_type == DB.ElementId.InvalidElementId:
            raise ToolError("No duct insulation type found in the document")

        thickness_ft = thickness_mm / 304.8
        insulated = 0

        def _operation() -> None:
            nonlocal insulated

            for element in system.DuctNetwork:
                try:
                    if isinstance(element, DB.Mechanical.Duct):
                        DB.Mechanical.DuctInsulation.Create(
                            doc, element.Id, insulation_type, thickness_ft
                        )
                        insulated += 1
                except Exception:
                    pass

        run_transaction(doc, "MCP: revit_insulate_duct_system", _operation)
        return InsulateDuctResult(insulated_count=insulated)

    @staticmethod
    def _list_duct_systems(doc: DB.Document) -> list[MepSystemItem]:
        items = []
        for system in (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.Mechanical.MechanicalSystem)
            .ToElements()
        ):
            count = system.DuctNetwork.Size if system.DuctNetwork else 0
            type_label = str(system.SystemType).replace("SystemType", "")
            items.append(
                MepSystemItem(
                    id=int(system.Id.Value),
                    name=(system.Name or ""),
                    type=type_label,
                    element_count=count,
                    classification=type_label.split()[0] if type_label else "Supply",
                )
            )
        return items

    @staticmethod
    def _list_pipe_systems(doc: DB.Document) -> list[MepSystemItem]:
        items = []
        for system in (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.Plumbing.PipingSystem)
            .ToElements()
        ):
            count = system.PipingNetwork.Size if system.PipingNetwork else 0
            type_label = str(system.SystemType).replace("SystemType", "")
            items.append(
                MepSystemItem(
                    id=int(system.Id.Value),
                    name=(system.Name or ""),
                    type=type_label,
                    element_count=count,
                    classification=type_label.split()[0] if type_label else "Domestic",
                )
            )
        return items

    @staticmethod
    def _list_electrical_systems(doc: DB.Document) -> list[MepSystemItem]:
        items = []
        for system in (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.Electrical.ElectricalSystem)
            .ToElements()
        ):
            count = system.Elements.Size if system.Elements else 0
            type_label = str(system.SystemType)
            items.append(
                MepSystemItem(
                    id=int(system.Id.Value),
                    name=(system.Name or ""),
                    type=type_label,
                    element_count=count,
                    classification=str(system.CircuitType),
                )
            )
        return items


def _segment_geometry(
    start_coords: list[float], end_coords: list[float]
) -> tuple[DB.XYZ, DB.XYZ, float]:
    start = DB.XYZ(start_coords[0], start_coords[1], start_coords[2])
    end = DB.XYZ(end_coords[0], end_coords[1], end_coords[2])
    return start, end, start.DistanceTo(end)


def _place_linear_segment(
    start_coords: list[float],
    end_coords: list[float],
    transaction_name: str,
    creator: Callable[[DB.Document, DB.XYZ, DB.XYZ], DB.Element],
) -> PlaceSegmentResult:
    doc = require_doc()
    start, end, length = _segment_geometry(start_coords, end_coords)

    def _operation() -> DB.Element:
        return creator(doc, start, end)

    element = run_transaction(doc, transaction_name, _operation)
    return PlaceSegmentResult(elementId=element.Id.ToString(), length=length)


def _create_duct(
    doc: DB.Document, spec: DuctSpec, start: DB.XYZ, end: DB.XYZ
) -> DB.Mechanical.Duct:
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


def _create_pipe(
    doc: DB.Document, spec: PipeSpec, start: DB.XYZ, end: DB.XYZ
) -> DB.Plumbing.Pipe:
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


def _create_conduit(
    doc: DB.Document, spec: ConduitSpec, start: DB.XYZ, end: DB.XYZ
) -> DB.Electrical.Conduit:
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
