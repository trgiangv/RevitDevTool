from Autodesk.Revit import DB, UI
from System import Environment

def main():
    uiapp : UI.UIApplication = __revit__ # type: ignore
    doc : DB.Document = __doc__ # type: ignore

    # create a file with name == doc.Title and write the doc path in it
    special_foler_user = Environment.SpecialFolder.CommonDesktopDirectory
    txt_file_path = Environment.GetFolderPath(special_foler_user) + "\\" + doc.Title + ".txt"
    with open(txt_file_path, "w") as f:
        f.write(doc.PathName)

if __name__ == "__main__":
    main()