# /// script
# dependencies = [
#     "polars==1.38.1",
#     "numpy==2.4.2",
#     "openpyxl==3.1.5",
#     "pydantic==2.12.5",
# ]
# ///

from Autodesk.Revit import DB, UI

uiapp : UI.UIApplication = __revit__ # type: ignore
    
def get_revit_data():
    elements = (DB.FilteredElementCollector(uiapp.ActiveUIDocument.Document, uiapp.ActiveUIDocument.ActiveView.Id)
                .WhereElementIsNotElementType().ToElements())
    data = []
    for elem in elements:
        data.append({
            "ElementId": elem.Id.IntegerValue,
            "Category": elem.Category.Name if elem.Category else "Uncategorized",
            "Family": elem.Symbol.Family.Name if hasattr(elem, 'Symbol') and elem.Symbol and elem.Symbol.Family else "N/A",
            "Type": elem.Symbol.Name if hasattr(elem, 'Symbol') and elem.Symbol else "N/A",
            "Level": elem.LookupParameter("Level").AsString() if elem.LookupParameter("Level") else "N/A",
        })
    return data

def analysis(data):
    import polars as pl
    df = pl.DataFrame(data)
    summary = df.group_by("Category").agg([
        pl.len().alias("ElementCount"),
        pl.col("Family").n_unique().alias("UniqueFamilies"),
        pl.col("Type").n_unique().alias("UniqueTypes"),
    ])
    return summary

def main():
    data = get_revit_data()
    analysis_result = analysis(data)
    print(str(analysis_result))

if __name__ == "__main__":
    main()