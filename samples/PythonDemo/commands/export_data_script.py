# /// script
# dependencies = [
#     "polars",
#     "openpyxl",
# ]
# ///

from typing import Any
import polars as pl
from RevitDevTool.Core import RevitContext
from System.Windows.Forms import SaveFileDialog, DialogResult
from Autodesk.Revit import DB


def collect_elements(doc: DB.Document) -> list[DB.Element]:
    return list(DB.FilteredElementCollector(doc, doc.ActiveView.Id).ToElements())


def get_element_data(element: DB.Element) -> dict[str, Any]:
    data: dict[str, Any] = {
        "id": element.Id.Value,
        "typeId": element.GetTypeId().Value,
        "category": element.Category.Name if element.Category else "Unknown",
    }

    params_map = element.ParametersMap
    it = params_map.ForwardIterator()
    while it.MoveNext():
        param : DB.Parameter = it.Current
        data[param.Definition.Name] = param.AsValueString()
        it.MoveNext()
    it.Dispose()

    return data


def export_data(data: list[dict[str, Any]], output_path: str) -> None:
    df = pl.DataFrame(data)
    df.write_excel(output_path)


def _ask_save_path(default_name: str) -> str | None:
    """Open a Windows Save File dialog and return the chosen path, or None if cancelled."""
    dialog = SaveFileDialog()
    dialog.Title = "Export Dashboard Data"
    dialog.Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*"
    dialog.FileName = default_name
    dialog.DefaultExt = ".xlsx"
    dialog.AddExtension = True

    if dialog.ShowDialog() == DialogResult.OK:
        return dialog.FileName
    return None


def main():
    doc = RevitContext.ActiveDocument
    elements = collect_elements(doc)
    data = [get_element_data(element) for element in elements]
    save_path = _ask_save_path("export.xlsx")
    if save_path:
        export_data(data, save_path)
    else:
        print("No save path selected")

if __name__ == "__main__":
    main()
