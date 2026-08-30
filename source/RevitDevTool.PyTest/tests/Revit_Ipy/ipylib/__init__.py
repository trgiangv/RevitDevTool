# coding: utf-8

from .geom import MM_PER_FOOT, midpoint, mm_to_feet

__all__ = ["MM_PER_FOOT", "active_document", "midpoint", "mm_to_feet"]


def active_document():
    uidoc = getattr(__revit__, "ActiveUIDocument", None)
    if uidoc is not None:
        return uidoc.Document
    try:
        from pyrevit import HOST_APP
        return HOST_APP.doc
    except ImportError:
        return None
