"""IronPython debug sample (Revit). Host listens on 127.0.0.1:5680.

Must stay named *_ipy_script.py so ScriptExecutionProvider uses IronPython.
Do not import pydevd here -- RevitDevTool already called _enable_attach.

See launch.json: Attach Host: IronPython (type pydevd, not debugpy).
"""


def main():
    uiapp = __revit__  # noqa: F821
    uidoc = uiapp.ActiveUIDocument
    if uidoc is None:
        print("No active document. Open a model, then run again.")
        return

    doc = uidoc.Document
    sel = list(uidoc.Selection.GetElementIds())

    # Breakpoint here after VS Code is attached to 5680.
    print("document:", doc.Title)
    print("selection count:", len(sel))

    for eid in sel:
        el = doc.GetElement(eid)
        name = el.Name if hasattr(el, "Name") else "?"
        print(el.Id, name)


if __name__ == "__main__":
    main()
