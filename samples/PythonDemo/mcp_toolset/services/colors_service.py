"""Internal helpers for color analysis and graphic overrides."""

from typing import Callable

from Autodesk.Revit import DB

from shared.element_helpers import param_value_as_string


class ColorsService:
    @staticmethod
    def _hex_to_color(hex_color: str) -> DB.Color:
        value = (hex_color or "").strip().lstrip("#")
        if len(value) != 6:
            return DB.Color(255, 0, 0)
        try:
            return DB.Color(
                int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)
            )
        except Exception:
            return DB.Color(255, 0, 0)

    @staticmethod
    def _color_to_hex(color: DB.Color) -> str:
        try:
            return f"#{int(color.Red):02X}{int(color.Green):02X}{int(color.Blue):02X}"
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
                red, green, blue = (
                    int(red * factor),
                    int(green * factor),
                    int(blue * factor),
                )
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
    def solid_fill_pattern_id(doc: DB.Document) -> DB.ElementId | None:
        try:
            patterns = (
                DB.FilteredElementCollector(doc)
                .OfClass(DB.FillPatternElement)
                .ToElements()
            )
            for pattern in patterns:
                fill_pattern = pattern.GetFillPattern()
                if fill_pattern is not None and fill_pattern.IsSolidFill:
                    return pattern.Id
        except Exception:
            return None
        return None

    @staticmethod
    def _value_from_parameter(
        parameter: DB.Parameter | None, document: DB.Document
    ) -> str:
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
    def parameter_display_value(element: DB.Element, parameter_name: str) -> str:
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

    def select_colors(
        self,
        unique_values: list[str],
        use_gradient: bool,
        custom_colors: list[str] | None,
    ) -> list[DB.Color]:
        if custom_colors is not None:
            colors = _hex_colors_from_list(custom_colors, self._hex_to_color)
            if len(colors) < len(unique_values):
                colors.extend(
                    self._generate_distinct_colors(len(unique_values) - len(colors))
                )
            return colors
        if use_gradient:
            return self._generate_gradient_colors(len(unique_values))
        return self._generate_distinct_colors(len(unique_values))

    def apply_color_overrides(
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
            assignments[value] = {
                "color": self._color_to_hex(color),
                "element_count": len(group),
            }
            for element in group:
                try:
                    active_view.SetElementOverrides(element.Id, override)
                    colored_count += 1
                except Exception:
                    pass
        return assignments, colored_count


def _hex_colors_from_list(
    values: list[str],
    converter: Callable[[str], DB.Color],
) -> list[DB.Color]:
    colors: list[DB.Color] = []
    for value in values:
        colors.append(converter(value))
    return colors
