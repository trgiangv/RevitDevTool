"""Infrastructure: status, grids, levels."""

from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

from dto.infrastructure import (
    GenerateGridsResult,
    GenerateLevelsResult,
    GridAxisSpec,
    LevelSpec,
    StatusResult,
)
from shared.document_stats import count_warnings
from shared.element_helpers import require_doc
from shared.responses import ToolError
from shared.transactions import run_transaction


class InfrastructureService:
    @staticmethod
    def get_status() -> StatusResult:
        doc : DB.Document = RevitContext.ActiveDocument # noqa
        if doc is None:
            return StatusResult(healthy=False)

        try:
            central_path, active_workset = _read_worksharing_status(doc)
            ui_doc = RevitContext.ActiveUiDocument
            selection_count = (
                ui_doc.Selection.GetElementIds().Count  # noqa
                if ui_doc is not None
                else None
            )
            ui_app = RevitContext.UiApplication
            version = ui_app.Application.VersionNumber if ui_app is not None else None  # noqa
            warning_count = count_warnings(doc)

            return StatusResult(
                healthy=True,
                documentTitle=doc.Title or "",
                filePath=doc.PathName or None,
                worksharingEnabled=bool(doc.IsWorkshared),
                centralPath=central_path,
                activeWorkset=active_workset,
                selectionCount=selection_count,
                warningCount=warning_count,
                version=version or None,
            )
        except Exception:
            return StatusResult(healthy=False)

    @staticmethod
    def generate_grids(
        vertical: GridAxisSpec,
        horizontal: GridAxisSpec,
        origin: list[float] | None = None,
    ) -> GenerateGridsResult:
        if vertical.count <= 0 or horizontal.count <= 0:
            raise ToolError("Grid counts must be positive")
        if vertical.spacing <= 0 or horizontal.spacing <= 0:
            raise ToolError("Grid spacing must be positive")

        doc = require_doc()
        ox, oy, oz = _resolve_origin(origin)
        h_extent = (
            horizontal.spacing * (horizontal.count - 1)
            if horizontal.count > 1
            else max(vertical.spacing, 100.0)
        )
        v_extent = (
            vertical.spacing * (vertical.count - 1)
            if vertical.count > 1
            else max(horizontal.spacing, 100.0)
        )

        def _operation() -> list[int]:
            created_ids: list[int] = []
            for i in range(vertical.count):
                x = ox + i * vertical.spacing
                line = DB.Line.CreateBound(
                    DB.XYZ(x, oy, oz), DB.XYZ(x, oy + h_extent, oz)
                )
                grid = DB.Grid.Create(doc, line)
                grid.Name = str(i + 1)
                created_ids.append(int(grid.Id.Value))
            for j in range(horizontal.count):
                y = oy + j * horizontal.spacing
                line = DB.Line.CreateBound(
                    DB.XYZ(ox, y, oz), DB.XYZ(ox + v_extent, y, oz)
                )
                grid = DB.Grid.Create(doc, line)
                grid.Name = chr(ord("A") + j)
                created_ids.append(int(grid.Id.Value))
            return created_ids

        created_grid_ids = run_transaction(doc, "MCP: revit_generate_grids", _operation)
        return GenerateGridsResult(grid_ids=created_grid_ids)

    @staticmethod
    def generate_levels(levels: list[LevelSpec]) -> GenerateLevelsResult:
        if not levels:
            raise ToolError("No level configurations provided")
        doc = require_doc()
        floor_plan_type = _find_floor_plan_type(doc)

        def _operation() -> list[int]:
            created_ids: list[int] = []
            for spec in levels:
                created_ids.append(_resolve_or_create_level(doc, spec, floor_plan_type))
            return created_ids

        created_level_ids = run_transaction(
            doc, "MCP: revit_generate_levels", _operation
        )
        return GenerateLevelsResult(level_ids=created_level_ids)


def _read_worksharing_status(
    doc: DB.Document,
) -> tuple[str | None, str | None]:
    if not doc.IsWorkshared:
        return None, None

    central_path = None
    active_workset = None
    try:
        central = doc.GetWorksharingCentralModelPath()
        if central:
            central_path = DB.ModelPathUtils.ConvertModelPathToUserVisiblePath(central)
    except Exception:
        pass
    try:
        table = doc.GetWorksetTable()
        active_id = table.GetActiveWorksetId()
        if active_id != DB.WorksetId.InvalidWorksetId:
            active_workset = (table.GetWorkset(active_id).Name or "")
    except Exception:
        pass
    return central_path, active_workset


def _resolve_origin(origin: list[float] | None) -> tuple[float, float, float]:
    if origin is not None and len(origin) >= 3:
        return float(origin[0]), float(origin[1]), float(origin[2])
    return 0.0, 0.0, 0.0


def _find_floor_plan_type(doc: DB.Document) -> DB.ViewFamilyType | None:
    for item in (
        DB.FilteredElementCollector(doc).OfClass(DB.ViewFamilyType).ToElements()
    ):
        if item.ViewFamily == DB.ViewFamily.FloorPlan:
            return item
    return None


def _resolve_or_create_level(
    doc: DB.Document,
    spec: LevelSpec,
    floor_plan_type: DB.ViewFamilyType | None,
) -> int:
    if not spec.name.strip():
        raise ToolError("Each level specification must include a name")
    existing = _find_level_by_name(doc, spec.name)
    if existing is not None:
        return int(existing.Id.Value)
    level = DB.Level.Create(doc, spec.elevation)
    level.Name = spec.name
    if spec.create_view and floor_plan_type is not None:
        DB.ViewPlan.Create(doc, floor_plan_type.Id, level.Id)
    return int(level.Id.Value)


def _find_level_by_name(doc: DB.Document, name: str) -> DB.Level | None:
    target = (name or "").lower()
    for lvl in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
        if (lvl.Name or "").lower() == target:
            return lvl
    return None
