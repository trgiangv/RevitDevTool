"""IronPython debug sample (Revit). Host listens on 127.0.0.1:4567.

Must stay named *_ipy_script.py so ScriptExecutionProvider uses IronPython.
Do not import pydevd here -- RevitDevTool already called _enable_attach.

Attach with launch.json "Attach Host: IronPython" (debugpy, port 4567).
CPython debugpy stays on 5678.
"""


def main():
    uiapp = __revit__  # noqa: F821
    uidoc = uiapp.ActiveUIDocument
    if uidoc is None:
        print("No active document. Open a model, then run again.")
        return

    doc = uidoc.Document
    sel = list(uidoc.Selection.GetElementIds())

    # Breakpoint here after VS Code is attached to 4567.
    print("document:", doc.Title)
    print("selection count:", len(sel))

    for eid in sel:
        el = doc.GetElement(eid)
        name = el.Name if hasattr(el, "Name") else "?"
        print(el.Id, name)


if __name__ == "__main__":
    main()
