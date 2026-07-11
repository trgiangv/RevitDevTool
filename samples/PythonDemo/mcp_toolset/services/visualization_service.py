"""Visualization and annotation services."""
from __future__ import annotations

from collections import defaultdict

from Autodesk.Revit import DB

from dto.visualization import (
    ClearOverridesResult,
    ColorByParameterResult,
    OverrideColorsResult,
    PlaceTagsResult,
    TagPlacement,
)
from services.colors_service import ColorsService
from shared.element_helpers import find_category_by_name, normalize_string, require_active_view, require_doc
from shared.responses import ToolError
from shared.transactions import run_transaction

_LARGE_ELEMENT_WARNING = 10_000


class VisualizationService:
    def __init__(self) -> None:
        self._colors = ColorsService()

    def color_by_parameter(
        self,
        category_name: str,
        parameter_name: str,
        view_id: int | None = None,
        use_gradient: bool = False,
        colors: list[str] | None = None,
    ) -> ColorByParameterResult:
        doc = require_doc()
        view = self._resolve_view(doc, view_id)
        category = find_category_by_name(doc, category_name)
        if category is None:
            raise ToolError("Category '{}' not found".format(category_name))

        elements = (
            DB.FilteredElementCollector(doc, view.Id)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        if not elements:
            raise ToolError("No elements found in category '{}' for the target view".format(category_name))

        grouped: dict[str, list] = defaultdict(list)
        for element in elements:
            grouped[self._colors._parameter_display_value(element, parameter_name)].append(element)

        unique_values = sorted(grouped.keys(), key=lambda v: (v == "None", v.lower()))
        palette = self._colors._select_colors(unique_values, use_gradient, colors)
        solid_fill = self._colors._solid_fill_pattern_id(doc)

        def _operation():
            assignments, colored = self._colors._apply_color_overrides(
                view, grouped, unique_values, palette, solid_fill
            )
            return colored

        colored = run_transaction(doc, "MCP: revit_color_by_parameter", _operation)
        result = ColorByParameterResult(
            groups_colored=len(unique_values),
            element_count=colored,
        )
        if colored > _LARGE_ELEMENT_WARNING:
            result.warning = "Colored {} elements; operations on more than {} may be slow.".format(
                colored, _LARGE_ELEMENT_WARNING
            )
        return result

    def clear_overrides(self, category_name: str, view_id: int | None = None) -> ClearOverridesResult:
        doc = require_doc()
        view = self._resolve_view(doc, view_id)
        category = find_category_by_name(doc, category_name)
        if category is None:
            raise ToolError("Category '{}' not found".format(category_name))
        elements = (
            DB.FilteredElementCollector(doc, view.Id)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToElements()
        )

        def _operation():
            empty = DB.OverrideGraphicSettings()
            cleared = 0
            for element in elements:
                try:
                    view.SetElementOverrides(element.Id, empty)
                    cleared += 1
                except Exception:
                    continue
            return cleared

        cleared = run_transaction(doc, "MCP: revit_clear_overrides", _operation)
        return ClearOverridesResult(cleared=cleared)

    def place_tags(self, tagging_data: list[TagPlacement]) -> PlaceTagsResult:
        if not tagging_data:
            raise ToolError("No tagging data provided")
        doc = require_doc()
        tags_placed = 0

        with DB.TransactionGroup(doc, "MCP: revit_place_tags") as group:
            group.Start()
            try:
                for td in tagging_data:
                    view = doc.GetElement(DB.ElementId(td.view_id))
                    if not isinstance(view, DB.View):
                        raise ToolError("View {} not found".format(td.view_id))

                    def _operation():
                        nonlocal tags_placed
                        for eid in td.element_ids:
                            element = doc.GetElement(DB.ElementId(eid))
                            if element is None:
                                continue
                            loc = element.Location
                            tag_point = loc.Point if isinstance(loc, DB.LocationPoint) else DB.XYZ.Zero
                            DB.IndependentTag.Create(
                                doc,
                                view.Id,
                                DB.Reference(element),
                                False,
                                DB.TagMode.TM_ADDBY_CATEGORY,
                                DB.TagOrientation.Horizontal,
                                tag_point,
                            )
                            tags_placed += 1

                    run_transaction(doc, "MCP: revit_place_tags", _operation)
                group.Assimilate()
            except Exception:
                group.RollBack()
                raise

        return PlaceTagsResult(tags_placed=tags_placed)

    def override_colors(self, element_ids: list[int], color: list[int]) -> OverrideColorsResult:
        if not element_ids:
            raise ToolError("No element IDs provided")
        if len(color) < 3:
            raise ToolError("Color must have 3 components [R, G, B]")
        doc = require_doc()
        view = require_active_view(doc)
        revit_color = DB.Color(
            max(0, min(255, color[0])),
            max(0, min(255, color[1])),
            max(0, min(255, color[2])),
        )
        solid_fill = self._colors._solid_fill_pattern_id(doc)
        override = DB.OverrideGraphicSettings()
        override.SetProjectionLineColor(revit_color)
        override.SetSurfaceForegroundPatternColor(revit_color)
        if solid_fill:
            override.SetSurfaceForegroundPatternId(solid_fill)

        def _operation():
            count = 0
            for eid in element_ids:
                eid_obj = DB.ElementId(eid)
                if doc.GetElement(eid_obj) is None:
                    continue
                view.SetElementOverrides(eid_obj, override)
                count += 1
            return count

        count = run_transaction(doc, "MCP: revit_override_colors", _operation)
        return OverrideColorsResult(overridden_count=count)

    @staticmethod
    def _resolve_view(doc: DB.Document, view_id: int | None) -> DB.View:
        if view_id is None:
            view = doc.ActiveView
            if view is None:
                raise ToolError("No active view")
            return view
        view = doc.GetElement(DB.ElementId(view_id))
        if not isinstance(view, DB.View):
            raise ToolError("View {} not found".format(view_id))
        return view
