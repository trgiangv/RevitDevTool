from Autodesk.Revit import DB, UI
from Autodesk.Revit.UI.Selection import ISelectionFilter, ObjectType
from System import Guid

class MySelectionFilter(ISelectionFilter):
    __namespace__ = str(Guid.NewGuid())  # must be unique for each execution (pythonnet3 limitation)

    def AllowElement(self, element):
        # only allow walls to be selected
        return isinstance(element, DB.Wall)

    def AllowReference(self, reference, position):
        return False


def main():
    uiapp: UI.UIApplication = __revit__.ActiveUIDocument  # type: ignore  # noqa: F821
    seleted_refs = uiapp.ActiveUIDocument.Selection.PickObjects(
        ObjectType.Element, MySelectionFilter(), "Select walls"
    )
    for ref in seleted_refs:
        element = uiapp.ActiveUIDocument.Document.GetElement(ref)
        print("Selected wall: {}".format(element.Id))

if __name__ == "__main__":
    main()