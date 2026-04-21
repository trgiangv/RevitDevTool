---
name: pyrevit-ironpython
description: Write pyRevit IronPython tools for Revit with correct project structure, coding conventions, and WPF/MVVM patterns. Use when creating or editing pyRevit pushbutton scripts, shared libs, bundle.yaml, XAML views, or ViewModels.
---

# pyRevit IronPython Development

## Critical Rules

1. **No wildcard imports** — `from Autodesk.Revit import DB`, never `from Autodesk.Revit.DB import *`
2. **No global state** — never cache `HOST_APP.doc`/`HOST_APP.uidoc` at module level; pass as parameters
3. **All Revit API calls inside methods** — imports at top, but execution only inside functions/methods
4. **`engine.clean: true`** — enable during development for module reload; disable in release for cached performance
5. **Python 2/3 compatible** — `"{}".format(x)` not f-strings, `ParentClass.__init__(self)` not `super()`
6. **Type hints on everything** — comment-style: `# type: (str, int) -> bool`
7. **Clean code** — no god files/methods/classes; split into `constants/models/services/viewmodels/views/utils`
8. **`lib/` naming convention** — extension-level `lib/` for shared code, pushbutton-level `lib/` for tool-specific code

## Quick Reference

```python
# Imports — at module top
from Autodesk.Revit import DB
from pyrevit import HOST_APP
from pyrevit.compat import get_elementid_value_func

element_id_value = get_elementid_value_func()  # utility — safe to cache

# Revit API calls — always inside methods, doc passed as parameter
def collect_schedules(doc):
    # type: (DB.Document) -> list[DB.ViewSchedule]
    return list(DB.FilteredElementCollector(doc).OfClass(DB.ViewSchedule).ToElements())

def main():
    # type: () -> None
    doc = HOST_APP.doc  # local scope only
    schedules = collect_schedules(doc)
```

## bundle.yaml

```yaml
title: My Tool Name
context: doc-project          # doc-project | zero-doc | selection
tooltip:
  en_us: |
    Version = 1.0
    Description: ...
author: "Author.Name"
engine:
  clean: true                 # development: true (reload modules); release: false (cached, faster)
```

## Reference Files

| File | When to Read |
|------|-------------|
| [Project Structure](./references/project-structure.md) | Extension layout, bundle hierarchy, shared vs local lib, entry point pattern |
| [Coding Conventions](./references/coding-conventions.md) | Python 2/3 compat, import rules, type hints, transactions, error reporting |
| [WPF & MVVM](./references/wpf-mvvm.md) | ObservableBase, RelayCommand, WPFWindow, notifications, XAML templates |
