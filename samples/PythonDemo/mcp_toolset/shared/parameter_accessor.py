"""Parameter read/write helpers aligned with C# ParameterAccessor."""
from __future__ import annotations

from Autodesk.Revit import DB

from shared.element_helpers import element_id_value, normalize_string, param_value_as_string


def get_parameter_value(param: DB.Parameter, doc: DB.Document) -> str:
    if param is None or not param.HasValue:
        return ""
    return param_value_as_string(param, doc)


def set_parameter_value(element: DB.Element, param_name: str, value: str) -> tuple[bool, str]:
    param = element.LookupParameter(param_name)
    if param is None:
        return False, "Parameter '{}' not found".format(param_name)
    if param.IsReadOnly:
        return False, "Parameter '{}' is read-only".format(param_name)
    try:
        storage = param.StorageType
        if storage == DB.StorageType.String:
            param.Set(str(value))
        elif storage == DB.StorageType.Integer:
            param.Set(int(float(value)))
        elif storage == DB.StorageType.Double:
            param.Set(float(value))
        elif storage == DB.StorageType.ElementId:
            param.Set(DB.ElementId(int(value)))
        else:
            return False, "Unsupported storage type for '{}'".format(param_name)
        return True, ""
    except Exception as exc:
        return False, str(exc)


def change_element_type(element: DB.Element, new_type_id: int) -> tuple[bool, str]:
    try:
        element.ChangeTypeId(DB.ElementId(new_type_id))
        return True, ""
    except Exception as exc:
        return False, str(exc)


def parameter_entry(param: DB.Parameter, doc: DB.Document) -> dict:
    builtin = False
    try:
        definition = param.Definition
        if isinstance(definition, DB.InternalDefinition):
            builtin = definition.BuiltInParameter != DB.BuiltInParameter.INVALID
    except Exception:
        pass
    return {
        "name": normalize_string(param.Definition.Name),
        "value": get_parameter_value(param, doc),
        "storage": str(param.StorageType),
        "writable": not param.IsReadOnly,
        "builtin": builtin,
        "isShared": bool(param.IsShared),
        "hasValue": bool(param.HasValue),
    }
