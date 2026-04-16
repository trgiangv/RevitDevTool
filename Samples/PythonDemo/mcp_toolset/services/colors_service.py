"""Service for color analysis and overrides."""

from __future__ import annotations

from collections import defaultdict

from Autodesk.Revit import DB

from dto.colors import (
    CategoryParametersResult,
    ClearColorsResult,
    ColorAssignment,
    ColorSplashResult,
    ColorSplashStatistics,
    ParameterInfo,
)
from shared.element_helpers import (
    find_category_by_name,
    normalize_string,
    param_value_as_string,
    require_active_view,
    require_doc,
)
from shared.responses import ToolError
from shared.transactions import run_transaction


class ColorsService:
    def _require_category_elements(self, doc: DB.Document, category_name: str) -> tuple[DB.Category, list]:
        category = find_category_by_name(doc, category_name)
        if category is None:
            raise ToolError("Category '{}' not found".format(category_name))

        elements = (
            DB.FilteredElementCollector(doc)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        if not elements:
            raise ToolError("No elements found in category '{}'".format(category_name))
        return category, elements

    @staticmethod
    def _hex_to_color(hex_color: str) -> DB.Color:
        value = (hex_color or "").strip().lstrip("#")
        if len(value) != 6:
            return DB.Color(255, 0, 0)
        try:
            return DB.Color(int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16))
        except Exception:
            return DB.Color(255, 0, 0)

    @staticmethod
    def _color_to_hex(color: DB.Color) -> str:
        try:
            return "#{:02X}{:02X}{:02X}".format(int(color.Red), int(color.Green), int(color.Blue))
        except Exception:
            return "#FF0000"

    @staticmethod
    def _generate_distinct_colors(count: int) -> list[DB.Color]:
        base_colors = [
            (255, 0, 0),
            (0, 128, 255),
            (0, 180, 0),
            (255, 180, 0),
            (180, 0, 255),
            (0, 200, 200),
            (255, 105, 180),
            (128, 128, 0),
            (128, 64, 0),
            (90, 90, 255),
        ]
        colors = []
        for index in range(count):
            red, green, blue = base_colors[index % len(base_colors)]
            cycle = index // len(base_colors)
            if cycle > 0:
                factor = max(0.45, 1.0 - (cycle * 0.15))
                red, green, blue = int(red * factor), int(green * factor), int(blue * factor)
            colors.append(DB.Color(red, green, blue))
        return colors

    @staticmethod
    def _generate_gradient_colors(count: int) -> list[DB.Color]:
        if count <= 1:
            return [DB.Color(255, 0, 0)]
        colors = []
        for index in range(count):
            ratio = float(index) / float(count - 1)
            red = int(255 * ratio)
            green = int(255 * (1 - abs((2 * ratio) - 1)))
            blue = int(255 * (1 - ratio))
            colors.append(DB.Color(red, green, blue))
        return colors

    @staticmethod
    def _solid_fill_pattern_id(doc: DB.Document) -> DB.ElementId | None:
        try:
            for pattern in DB.FilteredElementCollector(doc).OfClass(DB.FillPatternElement):
                fill_pattern = pattern.GetFillPattern()
                if fill_pattern is not None and fill_pattern.IsSolidFill:
                    return pattern.Id
        except Exception:
            return None
        return None

    @staticmethod
    def _value_from_parameter(parameter: DB.Parameter | None, document: DB.Document) -> str:
        if parameter is None or not parameter.HasValue:
            return "None"
        return param_value_as_string(parameter, document, default="None")

    @staticmethod
    def _try_get_element_type(element: DB.Element) -> DB.Element | None:
        try:
            return element.Document.GetElement(element.GetTypeId())
        except Exception:
            return None

    @staticmethod
    def _parameter_display_value(element: DB.Element, parameter_name: str) -> str:
        parameter = element.LookupParameter(parameter_name)
        value = ColorsService._value_from_parameter(parameter, element.Document)
        if value != "None":
            return value

        element_type = ColorsService._try_get_element_type(element)
        if element_type is None:
            return "None"
        return ColorsService._value_from_parameter(
            element_type.LookupParameter(parameter_name),
            element.Document,
        )

    def _collect_parameter_metadata(self, sample_element: DB.Element) -> list[ParameterInfo]:
        parameters = []
        seen_names = set()

        def _append_parameters(parameter_source):
            for parameter in parameter_source:
                try:
                    name = normalize_string(parameter.Definition.Name)
                    if not name or name in seen_names:
                        continue
                    seen_names.add(name)
                    parameters.append(
                        ParameterInfo(
                            name=name,
                            storage_type=str(parameter.StorageType),
                            has_value=bool(parameter.HasValue),
                            sample_value=self._parameter_display_value(sample_element, name),
                        )
                    )
                except Exception:
                    continue

        _append_parameters(sample_element.Parameters)
        try:
            element_type = sample_element.Document.GetElement(sample_element.GetTypeId())
            if element_type is not None:
                _append_parameters(element_type.Parameters)
        except Exception:
            pass

        parameters.sort(key=lambda item: item.name)
        return parameters

    def list_category_parameters(self, category_name: str) -> CategoryParametersResult:
        doc = require_doc()
        _, elements = self._require_category_elements(doc, category_name)
        parameters = self._collect_parameter_metadata(elements[0])
        return CategoryParametersResult(
            category=normalize_string(category_name),
            parameter_count=len(parameters),
            parameters=parameters,
        )

    def clear_colors(self, category_name: str) -> ClearColorsResult:
        doc = require_doc()
        _, elements = self._require_category_elements(doc, category_name)
        active_view = require_active_view(doc)

        def _operation():
            empty_override = DB.OverrideGraphicSettings()
            cleared_count = 0
            for element in elements:
                try:
                    active_view.SetElementOverrides(element.Id, empty_override)
                    cleared_count += 1
                except Exception:
                    continue
            return cleared_count

        cleared_count = run_transaction(doc, "Clear Element Colors", _operation)
        return ClearColorsResult(
            category=normalize_string(category_name),
            elements_processed=cleared_count,
        )

    def color_splash(
        self,
        category_name: str,
        parameter_name: str,
        use_gradient: bool = False,
        custom_colors: list[str] | None = None,
    ) -> ColorSplashResult:
        doc = require_doc()
        _, elements = self._require_category_elements(doc, category_name)
        active_view = require_active_view(doc)

        grouped_elements = defaultdict(list)
        for element in elements:
            grouped_elements[self._parameter_display_value(element, parameter_name)].append(element)

        unique_values = sorted(grouped_elements.keys(), key=lambda value: (value == "None", value.lower()))
        if not unique_values:
            raise ToolError("No parameter values found for '{}'".format(parameter_name))

        colors = self._select_colors(unique_values, use_gradient, custom_colors)
        solid_fill_id = self._solid_fill_pattern_id(doc)

        def _operation():
            return self._apply_color_overrides(active_view, grouped_elements, unique_values, colors, solid_fill_id)

        color_assignments, colored_count = run_transaction(doc, "Color Elements by Parameter", _operation)
        assignments_dto = {
            k: ColorAssignment(color=v["color"], element_count=v["element_count"])
            for k, v in color_assignments.items()
        }
        return ColorSplashResult(
            category=normalize_string(category_name),
            parameter=normalize_string(parameter_name),
            color_assignments=assignments_dto,
            statistics=ColorSplashStatistics(
                total_elements=len(elements),
                elements_colored=colored_count,
                unique_parameter_values=len(unique_values),
                use_gradient=bool(use_gradient),
            ),
        )

    def _select_colors(
        self, unique_values: list[str], use_gradient: bool, custom_colors: list[str] | None,
    ) -> list[DB.Color]:
        if custom_colors:
            colors = [self._hex_to_color(value) for value in custom_colors]
            if len(colors) < len(unique_values):
                colors.extend(self._generate_distinct_colors(len(unique_values) - len(colors)))
            return colors
        if use_gradient:
            return self._generate_gradient_colors(len(unique_values))
        return self._generate_distinct_colors(len(unique_values))

    def _apply_color_overrides(
        self,
        active_view: DB.View,
        grouped_elements: dict[str, list],
        unique_values: list[str],
        colors: list[DB.Color],
        solid_fill_id: DB.ElementId | None,
    ) -> tuple[dict, int]:
        assignments = {}
        colored_count = 0
        for index, value in enumerate(unique_values):
            color = colors[index]
            override = DB.OverrideGraphicSettings()
            override.SetProjectionLineColor(color)
            override.SetSurfaceForegroundPatternColor(color)
            override.SetCutForegroundPatternColor(color)
            override.SetCutLineColor(color)
            override.SetProjectionLineWeight(3)
            if solid_fill_id is not None:
                override.SetSurfaceForegroundPatternId(solid_fill_id)
                override.SetCutForegroundPatternId(solid_fill_id)

            group = grouped_elements[value]
            assignments[value] = {"color": self._color_to_hex(color), "element_count": len(group)}
            for element in group:
                try:
                    active_view.SetElementOverrides(element.Id, override)
                    colored_count += 1
                except Exception:
                    continue
        return assignments, colored_count
