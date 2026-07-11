"""Infrastructure: status, grids, levels."""
from __future__ import annotations

from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

from dto.infrastructure import (
    GenerateGridsResult,
    GenerateLevelsResult,
    GridAxisSpec,
    LevelSpec,
    StatusResult,
)
from shared.element_helpers import element_id_value, normalize_string, require_doc
from shared.responses import ToolError
from shared.transactions import run_transaction


class InfrastructureService:
    def get_status(self) -> StatusResult:
        try:
            doc = RevitContext.ActiveDocument
            if doc is None:
                return StatusResult(healthy=False)

            ui_doc = RevitContext.ActiveUiDocument
            app = RevitContext.UiApplication.Application if RevitContext.UiApplication else None

            central_path = None
            active_workset = None
            if doc.IsWorkshared:
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
                        active_workset = normalize_string(table.GetWorkset(active_id).Name)
                except Exception:
                    pass

            selection_count = None
            if ui_doc is not None:
                selection_count = ui_doc.Selection.GetElementIds().Count

            file_path = normalize_string(doc.PathName) if doc.PathName else None
            version = normalize_string(app.VersionNumber) if app else None

            return StatusResult(
                healthy=True,
                documentTitle=normalize_string(doc.Title),
                filePath=file_path,
                worksharingEnabled=bool(doc.IsWorkshared),
                centralPath=central_path,
                activeWorkset=active_workset,
                selectionCount=selection_count,
                version=version,
            )
        except Exception:
            return StatusResult(healthy=False)

    def generate_grids(
        self,
        vertical: GridAxisSpec,
        horizontal: GridAxisSpec,
        origin: list[float] | None = None,
    ) -> GenerateGridsResult:
        if vertical.count <= 0 or horizontal.count <= 0:
            raise ToolError("Grid counts must be positive")
        if vertical.spacing <= 0 or horizontal.spacing <= 0:
            raise ToolError("Grid spacing must be positive")

        doc = require_doc()
        coords = origin if origin and len(origin) >= 3 else [0.0, 0.0, 0.0]
        ox, oy, oz = coords[0], coords[1], coords[2]
        h_extent = horizontal.spacing * (horizontal.count - 1) if horizontal.count > 1 else max(vertical.spacing, 100.0)
        v_extent = vertical.spacing * (vertical.count - 1) if vertical.count > 1 else max(horizontal.spacing, 100.0)

        def _operation():
            grid_ids = []
            for i in range(vertical.count):
                x = ox + i * vertical.spacing
                line = DB.Line.CreateBound(DB.XYZ(x, oy, oz), DB.XYZ(x, oy + h_extent, oz))
                grid = DB.Grid.Create(doc, line)
                grid.Name = str(i + 1)
                grid_ids.append(element_id_value(grid.Id))
            for j in range(horizontal.count):
                y = oy + j * horizontal.spacing
                line = DB.Line.CreateBound(DB.XYZ(ox, y, oz), DB.XYZ(ox + v_extent, y, oz))
                grid = DB.Grid.Create(doc, line)
                grid.Name = chr(ord("A") + j)
                grid_ids.append(element_id_value(grid.Id))
            return grid_ids

        grid_ids = run_transaction(doc, "MCP: revit_generate_grids", _operation)
        return GenerateGridsResult(grid_ids=grid_ids)

    def generate_levels(self, levels: list[LevelSpec]) -> GenerateLevelsResult:
        if not levels:
            raise ToolError("No level configurations provided")
        doc = require_doc()
        vft = (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.ViewFamilyType)
            .ToElements()
        )
        floor_plan_type = None
        for item in vft:
            if item.ViewFamily == DB.ViewFamily.FloorPlan:
                floor_plan_type = item
                break

        def _operation():
            level_ids = []
            for spec in levels:
                if not spec.name.strip():
                    raise ToolError("Each level specification must include a name")
                existing = None
                for lvl in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
                    if normalize_string(lvl.Name).lower() == normalize_string(spec.name).lower():
                        existing = lvl
                        break
                if existing is not None:
                    level_ids.append(element_id_value(existing.Id))
                    continue
                level = DB.Level.Create(doc, spec.elevation)
                level.Name = spec.name
                if spec.create_view and floor_plan_type is not None:
                    DB.ViewPlan.Create(doc, floor_plan_type.Id, level.Id)
                level_ids.append(element_id_value(level.Id))
            return level_ids

        level_ids = run_transaction(doc, "MCP: revit_generate_levels", _operation)
        return GenerateLevelsResult(level_ids=level_ids)
