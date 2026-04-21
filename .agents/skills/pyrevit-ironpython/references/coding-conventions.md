# Coding Conventions

## File Header

```python
# coding: utf-8
```

## Imports

### Revit API

Revit API is pre-referenced — no `clr.AddReference` needed. All API calls must be inside methods, never at module level.

```python
from Autodesk.Revit import DB
from Autodesk.Revit import UI

def collect_schedules(doc):
    # type: (DB.Document) -> list[DB.ViewSchedule]
    return list(
        DB.FilteredElementCollector(doc)
        .OfClass(DB.ViewSchedule)
        .WhereElementIsNotElementType()
        .ToElements()
    )

# WRONG — wildcard pollutes namespace
from Autodesk.Revit.DB import *

# WRONG — Revit API calls at module level
element = doc.GetElement(DB.ElementId(123))
```

### pyRevit Built-ins

```python
from pyrevit import HOST_APP, DB, UI
from pyrevit import framework
from pyrevit import EXEC_PARAMS
from pyrevit.api import AdWindows
from pyrevit.compat import get_elementid_value_func

element_id_value = get_elementid_value_func()  # utility — safe to cache
```

### No Global State

In release mode (`engine.clean: false`), pyRevit caches the IronPython engine between runs for performance. Module-level references to `HOST_APP.doc`, `HOST_APP.uidoc`, or Revit elements will persist, become stale, and leak memory.

Always write code that works correctly regardless of `engine.clean` setting:

```python
# WRONG — module-level cache, persists when engine is cached
doc = HOST_APP.doc
uidoc = HOST_APP.uidoc

# CORRECT — access at call time, pass as parameter
def main():
    doc = HOST_APP.doc      # local variable, discarded after return
    service = MyService(doc)
    service.collect()

class MyService(object):
    def __init__(self, doc):
        # type: (DB.Document) -> None
        self.doc = doc      # parameter injection, scoped to this run

    def collect(self):
        # type: () -> list
        return list(DB.FilteredElementCollector(self.doc).ToElements())
```

**`engine.clean` usage:**
- **Development** (`true`) — fresh engine per run, modules reload on code changes
- **Release** (`false`) — cached engine, faster startup, modules stay in memory

### .NET CLR References

Only use `clr.AddReference()` for assemblies not already loaded by Revit:

```python
import clr
clr.AddReference("CommunityToolkit.Mvvm")
from CommunityToolkit.Mvvm.ComponentModel import ObservableObject
```

The assembly must be loaded in Revit's AppDomain before the script runs.

### .NET Generic Types

```python
from System.Collections.Generic import List

id_list = List[DB.ElementId]()
id_list.Add(element_id)
```

## Type Hints

Comment-based type hints for Python 2/3 compatibility. Every function/method must have one.

```python
def apply_copy(doc, source, targets, selected_names, mode):
    # type: (DB.Document, DB.ViewSchedule, list[DB.ViewSchedule], set, str) -> CopyResult
    pass

class CopyResult(object):
    def __init__(self, source_name, mode):
        # type: (str, str) -> None
        self.source_name = source_name
        self.mode = mode
        self.applied = []   # type: list[str]
        self.skipped = []   # type: list[dict]
```

## Python 2/3 Compatibility

### String Formatting

```python
# CORRECT — str.format() works in Python 2.6+ and 3
message = "Source: {}, Count: {}".format(name, count)
message = "Field {0}: {1}".format(index, heading)

# WRONG — f-strings are Python 3.6+
message = f"Source: {name}"
```

### Class Declaration

```python
# Always explicit object base and explicit parent init
class MyViewModel(ObservableBase):
    def __init__(self):
        # type: () -> None
        ObservableBase.__init__(self)  # never super()
```

### Exception Handling

```python
try:
    do_something()
except Exception as error:
    handle_error(error)

# WRONG — chained exceptions are Python 3 only
raise ValueError("msg") from original_error

# WRONG — nonlocal is Python 3 only, use mutable container:
result = [None]
def callback():
    result[0] = compute()
```

### Dictionary Iteration

```python
for key, value in my_dict.items():
    pass

# If mutating during iteration
for key in list(my_dict.keys()):
    del my_dict[key]
```

## Transactions

### Single Transaction

```python
def run_transaction(doc, name, action):
    # type: (DB.Document, str, object) -> object
    transaction = DB.Transaction(doc, name)
    transaction.Start()
    try:
        result = action()
        transaction.Commit()
        return result
    except Exception:
        transaction.RollBack()
        raise
```

### Transaction Group (multi-step)

```python
group = DB.TransactionGroup(doc, "Operation Name")
group.Start()
try:
    run_transaction(doc, "Step 1", step_1_action)
    run_transaction(doc, "Step 2", step_2_action)
    group.Assimilate()
except Exception:
    group.RollBack()
    raise
```

Transaction names should be descriptive: `"ToolName: action description"`.

## Error Reporting

```python
from Autodesk.Revit import UI

def show_message(title, message):
    # type: (str, str) -> None
    dialog = UI.TaskDialog(title)
    dialog.TitleAutoPrefix = False
    dialog.MainInstruction = message
    dialog.Show()
```
