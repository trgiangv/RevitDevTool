"""Read-only document counters shared across content and infrastructure services."""

from Autodesk.Revit import DB


def count_warnings(doc: DB.Document) -> int:
    try:
        warnings = doc.GetWarnings()
        return len(warnings) if warnings else 0
    except Exception:
        return 0


def count_levels(doc: DB.Document) -> int:
    return DB.FilteredElementCollector(doc).OfClass(DB.Level).GetElementCount()


def count_user_views(doc: DB.Document) -> int:
    views = (
        DB.FilteredElementCollector(doc)
        .OfClass(DB.View)
        .WhereElementIsNotElementType()
        .ToElements()
    )
    return sum(
        1
        for view in views
        if not view.IsTemplate
        and view.ViewType not in (DB.ViewType.Undefined, DB.ViewType.Internal)
        and not isinstance(view, DB.ViewSchedule)
    )
