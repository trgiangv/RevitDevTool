"""Element helpers shared by services."""

from __future__ import annotations

from shared.constants import DEFAULT_UNNAMED


def normalize_string(text):
    if text is None:
        return DEFAULT_UNNAMED
    if isinstance(text, bytes):
        try:
            return text.decode("utf-8").strip()
        except UnicodeDecodeError:
            return text.decode("latin-1", errors="replace").strip()
    try:
        return str(text).strip()
    except Exception:
        return DEFAULT_UNNAMED


def element_id_value(element_id):
    try:
        return int(element_id.Value)
    except AttributeError:
        return int(element_id.IntegerValue)


def find_family_symbol_safely(doc, target_family_name, target_type_name=None):
    try:
        from Autodesk.Revit import DB

        collector = DB.FilteredElementCollector(doc).OfClass(DB.FamilySymbol)
        for symbol in collector:
            if symbol.Family.Name != target_family_name:
                continue
            if not target_type_name or symbol.Name == target_type_name:
                return symbol
        return None
    except Exception:
        return None

