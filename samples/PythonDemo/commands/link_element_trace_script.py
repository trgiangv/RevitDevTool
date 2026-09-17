# /// script
# dependencies = []
# ///

"""Pick linked elements and print scoped tokens for the Revit monitor linkifier.

Token shape: {linkInstanceId}@{ElementId|UniqueId|IfcGuid}
Click a token in the monitor to select the linked element and zoom.
"""

from Autodesk.Revit.DB import BuiltInParameter, RevitLinkInstance
from Autodesk.Revit.Exceptions import OperationCanceledException
from Autodesk.Revit.UI.Selection import ObjectType


def _id_text(element_id):
    if hasattr(element_id, "Value"):
        return str(element_id.Value)
    return str(element_id.IntegerValue)


def main():
    uiapp = __revit__  # noqa: F821
    uidoc = uiapp.ActiveUIDocument
    if uidoc is None:
        print("No active document. Open a model, then run again.")
        return

    try:
        picks = uidoc.Selection.PickObjects(ObjectType.LinkedElement, "Select linked element(s)")
    except OperationCanceledException:
        return

    host_doc = uidoc.Document
    for pick in picks:
        instance = host_doc.GetElement(pick.ElementId)
        if not isinstance(instance, RevitLinkInstance):
            continue

        link_doc = instance.GetLinkDocument()
        linked = None if link_doc is None else link_doc.GetElement(pick.LinkedElementId)
        if linked is None:
            print("Unloaded or missing link instance {0}".format(_id_text(instance.Id)))
            continue

        instance_id = _id_text(instance.Id)
        # Scoped tokens: {linkInstanceId}@{inner}. Click in the monitor to select + zoom.
        print("Linked ElementId {0}@{1}".format(instance_id, _id_text(linked.Id)))
        print("Linked UniqueId {0}@{1}".format(instance_id, linked.UniqueId))

        ifc_param = linked.get_Parameter(BuiltInParameter.IFC_GUID)
        ifc = None if ifc_param is None else ifc_param.AsString()
        if ifc:
            print("Linked IfcGuid {0}@{1}".format(instance_id, ifc))


if __name__ == "__main__":
    main()
