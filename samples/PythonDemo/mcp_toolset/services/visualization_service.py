"""Visualization and annotation services."""

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
from shared.element_helpers import (
    require_active_view,
    require_category,
    require_doc,
)
from shared.responses import ToolError
from shared.transactions import run_transaction

_LARGE_ELEMENT_WARNING = 10_000


def _view_for_tagging(doc: DB.Document, view_id: int) -> DB.View:
    view = doc.GetElement(DB.ElementId(view_id))
    if not isinstance(view, DB.View):
        raise ToolError(f"View {view_id} not found")
    return view


def _tag_point(element: DB.Element) -> DB.XYZ:
    loc = element.Location
    if isinstance(loc, DB.LocationPoint):
        return loc.Point
    return DB.XYZ.Zero # noqa


def _place_tags_for_view(
    doc: DB.Document, view: DB.View, element_ids: list[int]
) -> int:
    placed = 0
    for eid in element_ids:
        element = doc.GetElement(DB.ElementId(eid))
        if element is None:
            continue
        DB.IndependentTag.Create(  # noqa
            doc,
            view.Id,
            DB.Reference(element),
            False,
            DB.TagMode.TM_ADDBY_CATEGORY,
            DB.TagOrientation.Horizontal,
            _tag_point(element),
        )
        placed += 1
    return placed


def _place_tag_batch(doc: DB.Document, tagging_data: list[TagPlacement]) -> int:
    tags_placed = 0
    with DB.TransactionGroup(doc, "MCP: revit_place_tags") as group:  # noqa
        group.Start()
        try:
            for placement in tagging_data:
                view = _view_for_tagging(doc, placement.view_id)
                tags_placed += run_transaction(
                    doc,
                    "MCP: revit_place_tags",
                    lambda view=view, element_ids=placement.element_ids: _place_tags_for_view(
                        doc, view, element_ids
                    ),
                )
            group.Assimilate()
        except Exception:
            group.RollBack()
            raise
    return tags_placed


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
        category = require_category(doc, category_name)

        elements = (
            DB.FilteredElementCollector(doc, view.Id)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        if not elements:
            raise ToolError(
                f"No elements found in category '{category_name}' for the target view"
            )

        grouped: dict[str, list] = defaultdict(list)
        for element in elements:
            grouped[
                self._colors.parameter_display_value(element, parameter_name)
            ].append(element)

        unique_values = sorted(grouped.keys(), key=lambda v: (v == "None", v.lower()))
        palette = self._colors.select_colors(unique_values, use_gradient, colors)
        solid_fill = self._colors.solid_fill_pattern_id(doc)

        def _operation() -> int:
            _assignments, colored = self._colors.apply_color_overrides(
                view, grouped, unique_values, palette, solid_fill
            )
            return colored

        colored = run_transaction(doc, "MCP: revit_color_by_parameter", _operation)
        result = ColorByParameterResult(
            groups_colored=len(unique_values),
            element_count=colored,
        )
        if colored > _LARGE_ELEMENT_WARNING:
            result.warning = f"Colored {colored} elements; operations on more than {_LARGE_ELEMENT_WARNING} may be slow."
        return result

    def clear_overrides(
        self, category_name: str, view_id: int | None = None
    ) -> ClearOverridesResult:
        doc = require_doc()
        view = self._resolve_view(doc, view_id)
        category = require_category(doc, category_name)
        elements = (
            DB.FilteredElementCollector(doc, view.Id)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToElements()
        )

        def _operation() -> int:
            empty = DB.OverrideGraphicSettings()
            cleared = 0
            for element in elements:
                try:
                    view.SetElementOverrides(element.Id, empty)
                    cleared += 1
                except Exception:
                    pass
            return cleared

        cleared = run_transaction(doc, "MCP: revit_clear_overrides", _operation)
        return ClearOverridesResult(cleared=cleared)

    @staticmethod
    def place_tags(tagging_data: list[TagPlacement]) -> PlaceTagsResult:
        if not tagging_data:
            raise ToolError("No tagging data provided")
        tags_placed = _place_tag_batch(require_doc(), tagging_data)
        return PlaceTagsResult(tags_placed=tags_placed)

    def override_colors(
        self, element_ids: list[int], color: list[int]
    ) -> OverrideColorsResult:
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
        solid_fill = self._colors.solid_fill_pattern_id(doc)
        override = DB.OverrideGraphicSettings()
        override.SetProjectionLineColor(revit_color)
        override.SetSurfaceForegroundPatternColor(revit_color)
        if solid_fill:
            override.SetSurfaceForegroundPatternId(solid_fill)

        def _operation() -> int:
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
            raise ToolError(f"View {view_id} not found")
        return view
