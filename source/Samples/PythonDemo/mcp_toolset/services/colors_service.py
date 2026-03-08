"""Service for color analysis and overrides."""

from __future__ import annotations

from collections import defaultdict

from shared.context import get_doc
from shared.element_helpers import normalize_string
from shared.responses import ToolError
from shared.transactions import run_transaction


class ColorsService:
    def _require_doc(self):
        doc = get_doc()
        if doc is None:
            raise ToolError("No active Revit document")
        return doc

    def _require_category_elements(self, doc, category_name: str):
        from Autodesk.Revit import DB

        category = self._find_category(doc, category_name)
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
    def _find_category(doc, category_name: str):
        target_name = normalize_string(category_name)
        for category in doc.Settings.Categories:
            if normalize_string(category.Name) == target_name:
                return category
        return None

    @staticmethod
    def _hex_to_color(hex_color: str):
        from Autodesk.Revit import DB

        value = (hex_color or "").strip().lstrip("#")
        if len(value) != 6:
            return DB.Color(255, 0, 0)
        try:
            return DB.Color(int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16))
        except Exception:
            return DB.Color(255, 0, 0)

    @staticmethod
    def _color_to_hex(color):
        try:
            return "#{:02X}{:02X}{:02X}".format(int(color.Red), int(color.Green), int(color.Blue))
        except Exception:
            return "#FF0000"

    @staticmethod
    def _generate_distinct_colors(count: int):
        from Autodesk.Revit import DB

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
    def _generate_gradient_colors(count: int):
        from Autodesk.Revit import DB

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
    def _solid_fill_pattern_id(doc):
        from Autodesk.Revit import DB

        try:
            for pattern in DB.FilteredElementCollector(doc).OfClass(DB.FillPatternElement):
                fill_pattern = pattern.GetFillPattern()
                if fill_pattern is not None and fill_pattern.IsSolidFill:
                    return pattern.Id
        except Exception:
            return None
        return None

    @staticmethod
    def _value_from_parameter(parameter, document, revit_db):
        if parameter is None or not parameter.HasValue:
            return "None"

        storage_type = parameter.StorageType
        if storage_type == revit_db.StorageType.String:
            return normalize_string(parameter.AsString() or "None")
        if storage_type == revit_db.StorageType.Integer:
            return normalize_string(parameter.AsValueString() or str(parameter.AsInteger()))
        if storage_type == revit_db.StorageType.Double:
            return normalize_string(parameter.AsValueString() or str(round(parameter.AsDouble(), 3)))
        if storage_type == revit_db.StorageType.ElementId:
            return ColorsService._element_id_display_value(parameter, document, revit_db)

        return normalize_string(parameter.AsValueString() or "None")

    @staticmethod
    def _element_id_display_value(parameter, document, revit_db):
        element_id = parameter.AsElementId()
        if not element_id or element_id == revit_db.ElementId.InvalidElementId:
            return "None"

        referenced = document.GetElement(element_id)
        if referenced is None:
            return "None"
        return normalize_string(getattr(referenced, "Name", None) or "None")

    @staticmethod
    def _try_get_element_type(element):
        try:
            return element.Document.GetElement(element.GetTypeId())
        except Exception:
            return None

    @staticmethod
    def _parameter_display_value(element, parameter_name: str):
        from Autodesk.Revit import DB

        parameter = element.LookupParameter(parameter_name)
        value = ColorsService._value_from_parameter(parameter, element.Document, DB)
        if value != "None":
            return value

        element_type = ColorsService._try_get_element_type(element)
        if element_type is None:
            return "None"
        return ColorsService._value_from_parameter(
            element_type.LookupParameter(parameter_name),
            element.Document,
            DB,
        )

    def _collect_parameter_metadata(self, sample_element):
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
                        {
                            "name": name,
                            "storage_type": str(parameter.StorageType),
                            "has_value": bool(parameter.HasValue),
                            "sample_value": self._parameter_display_value(sample_element, name),
                        }
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

        parameters.sort(key=lambda item: item["name"])
        return parameters

    def list_category_parameters(self, category_name: str) -> dict:
        doc = self._require_doc()
        _, elements = self._require_category_elements(doc, category_name)
        parameters = self._collect_parameter_metadata(elements[0])
        return {"category": normalize_string(category_name), "parameter_count": len(parameters), "parameters": parameters}

    def clear_colors(self, category_name: str) -> dict:
        from Autodesk.Revit import DB

        doc = self._require_doc()
        _, elements = self._require_category_elements(doc, category_name)
        active_view = doc.ActiveView
        if active_view is None:
            raise ToolError("No active view found")

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
        return {
            "category": normalize_string(category_name),
            "elements_processed": cleared_count,
        }

    def color_splash(self, category_name: str, parameter_name: str, use_gradient: bool = False, custom_colors=None) -> dict:
        from Autodesk.Revit import DB

        doc = self._require_doc()
        _, elements = self._require_category_elements(doc, category_name)
        active_view = doc.ActiveView
        if active_view is None:
            raise ToolError("No active view found")

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
        return {
            "category": normalize_string(category_name),
            "parameter": normalize_string(parameter_name),
            "color_assignments": color_assignments,
            "statistics": {
                "total_elements": len(elements),
                "elements_colored": colored_count,
                "unique_parameter_values": len(unique_values),
                "use_gradient": bool(use_gradient),
            },
        }

    def _select_colors(self, unique_values, use_gradient, custom_colors):
        if custom_colors:
            colors = [self._hex_to_color(value) for value in custom_colors]
            if len(colors) < len(unique_values):
                colors.extend(self._generate_distinct_colors(len(unique_values) - len(colors)))
            return colors
        if use_gradient:
            return self._generate_gradient_colors(len(unique_values))
        return self._generate_distinct_colors(len(unique_values))

    def _apply_color_overrides(self, active_view, grouped_elements, unique_values, colors, solid_fill_id):
        from Autodesk.Revit import DB
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
