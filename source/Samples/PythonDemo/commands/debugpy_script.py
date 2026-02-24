# /// script
# dependencies = [
#     "numpy",
# ]
# ///

from Autodesk.Revit import UI

uidoc : UI.UIDocument = __revit__.ActiveUIDocument  # pyright: ignore[reportUndefinedVariable] # noqa: F821
sel_ids = uidoc.Selection.GetElementIds()

for eid in sel_ids:
    el = uidoc.Document.GetElement(eid)
    print(el.Id, el.Name)
