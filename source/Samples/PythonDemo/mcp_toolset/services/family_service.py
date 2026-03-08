"""Service for family discovery and placement."""

from __future__ import annotations

from shared.context import get_doc
from shared.element_helpers import (
    element_id_value,
    find_family_symbol_safely,
    normalize_string,
)
from shared.responses import ToolError
from shared.transactions import run_transaction


class FamilyService:
    def _require_doc(self):
        doc = get_doc()
        if doc is None:
            raise ToolError("No active Revit document")
        return doc

    def place_family(
        self,
        family_name: str,
        type_name: str = None,
        x: float = 0.0,
        y: float = 0.0,
        z: float = 0.0,
        rotation: float = 0.0,
        level_name: str = None,
        properties: dict | None = None,
    ) -> dict:
        from Autodesk.Revit import DB

        doc = self._require_doc()

        target_symbol = find_family_symbol_safely(doc, family_name, type_name)
        if target_symbol is None:
            self._raise_family_not_found(doc, family_name, type_name)

        target_level = self._resolve_level(doc, level_name)
        point = DB.XYZ(float(x), float(y), float(z))

        def _operation():
            if not target_symbol.IsActive:
                target_symbol.Activate()
                doc.Regenerate()

            instance = self._create_instance(doc, point, target_symbol, target_level)
            self._apply_rotation(instance, point, rotation)
            properties_set, properties_failed = self._set_properties(instance, properties or {})
            return instance, properties_set, properties_failed

        instance, properties_set, properties_failed = run_transaction(doc, "Place Family Instance via MCP", _operation)
        return {
            "element_id": element_id_value(instance.Id),
            "family_name": family_name,
            "type_name": type_name,
            "requested_location": {"x": point.X, "y": point.Y, "z": point.Z},
            "actual_location": self._instance_point(instance, point),
            "rotation_degrees": rotation,
            "level": level_name if target_level else None,
            "properties_set": properties_set,
            "properties_failed": properties_failed,
        }

    @staticmethod
    def _create_instance(doc, point, target_symbol, target_level):
        from Autodesk.Revit import DB
        if target_level is not None:
            return doc.Create.NewFamilyInstance(point, target_symbol, target_level, DB.Structure.StructuralType.NonStructural)
        return doc.Create.NewFamilyInstance(point, target_symbol, DB.Structure.StructuralType.NonStructural)

    @staticmethod
    def _instance_point(instance, fallback_point):
        try:
            placed_point = instance.Location.Point
            return {"x": placed_point.X, "y": placed_point.Y, "z": placed_point.Z}
        except Exception:
            return {"x": fallback_point.X, "y": fallback_point.Y, "z": fallback_point.Z}

    @staticmethod
    def _apply_rotation(instance, point, rotation):
        from Autodesk.Revit import DB
        if not rotation:
            return
        try:
            rotation_radians = float(rotation) * (3.14159265359 / 180.0)
            axis = DB.Line.CreateBound(point, point.Add(DB.XYZ(0, 0, 1)))
            if hasattr(instance.Location, "Rotate"):
                instance.Location.Rotate(axis, rotation_radians)
        except Exception:
            pass

    @staticmethod
    def _set_properties(instance, properties: dict):
        from Autodesk.Revit import DB
        set_ok = []
        set_fail = []
        for param_name, param_value in properties.items():
            try:
                param = instance.LookupParameter(param_name)
                if param is None:
                    set_fail.append("{} (not found)".format(param_name))
                    continue
                if param.IsReadOnly:
                    set_fail.append("{} (read-only)".format(param_name))
                    continue
                if param.StorageType == DB.StorageType.String:
                    param.Set(str(param_value))
                elif param.StorageType == DB.StorageType.Integer:
                    param.Set(int(param_value))
                elif param.StorageType == DB.StorageType.Double:
                    param.Set(float(param_value))
                else:
                    set_fail.append("{} (unsupported type)".format(param_name))
                    continue
                set_ok.append(param_name)
            except Exception as exc:
                set_fail.append("{} ({})".format(param_name, exc))
        return set_ok, set_fail

    @staticmethod
    def _resolve_level(doc, level_name):
        from Autodesk.Revit import DB
        if not level_name:
            return None
        levels = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        for level in levels:
            try:
                if normalize_string(level.Name) == normalize_string(level_name):
                    return level
            except Exception:
                continue
        raise ToolError("Level not found: {}".format(level_name), code="revit.level_not_found")

    @staticmethod
    def _raise_family_not_found(doc, family_name, type_name):
        from Autodesk.Revit import DB
        available = []
        try:
            symbols = DB.FilteredElementCollector(doc).OfClass(DB.FamilySymbol).ToElements()
            available = sorted({normalize_string(symbol.Family.Name) for symbol in symbols[:200]})
        except Exception:
            pass
        msg = "Family type not found: {} - {}".format(family_name, type_name or "Any")
        if available:
            msg += ". Available (first 20): {}".format(", ".join(available[:20]))
        raise ToolError(msg, code="revit.family_type_not_found")

    def list_families(self, contains: str = None, limit: int = 50) -> dict:
        from Autodesk.Revit import DB

        doc = self._require_doc()

        needle = normalize_string(contains).lower() if contains else ""
        items = []
        symbols = DB.FilteredElementCollector(doc).OfClass(DB.FamilySymbol).ToElements()
        for symbol in symbols:
            try:
                family_name = normalize_string(symbol.Family.Name)
                type_name = normalize_string(symbol.Name)
                combined = "{} {}".format(family_name, type_name).lower()
                if needle and needle not in combined:
                    continue
                items.append(
                    {
                        "family_name": family_name,
                        "type_name": type_name,
                        "category": normalize_string(symbol.Category.Name) if symbol.Category else "Unknown",
                        "is_active": bool(symbol.IsActive),
                    }
                )
            except Exception:
                continue
        items.sort(key=lambda item: (item["family_name"], item["type_name"]))
        if limit > 0:
            items = items[:limit]
        return {"families": items, "count": len(items), "filtered_by": contains}

    def list_family_categories(self) -> dict:
        from Autodesk.Revit import DB

        doc = self._require_doc()

        categories = {}
        symbols = DB.FilteredElementCollector(doc).OfClass(DB.FamilySymbol).ToElements()
        for symbol in symbols:
            try:
                category_name = normalize_string(symbol.Category.Name) if symbol.Category else "Unknown"
                categories[category_name] = categories.get(category_name, 0) + 1
            except Exception:
                continue

        category_list = [{"category": name, "family_count": count} for name, count in sorted(categories.items())]
        return {"categories": category_list, "count": len(category_list)}
