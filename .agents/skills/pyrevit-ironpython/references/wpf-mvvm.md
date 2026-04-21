# WPF & MVVM Patterns

## Stack

pyRevit IronPython WPF uses:
- **MahApps.Metro** — `MetroWindow` base, modern controls (via `pyRevitLabs.MahAppsMetro`)
- **CommunityToolkit.Mvvm** — `ObservableObject`, `RelayCommand` (via `clr.AddReference`)
- **pyRevit framework** — WPF loader, threading, resource dictionaries

## ObservableBase

Wraps `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` with a Python-friendly dict store.

```python
import clr
clr.AddReference("CommunityToolkit.Mvvm")

from CommunityToolkit.Mvvm.ComponentModel import ObservableObject

class ObservableBase(ObservableObject):
    def __init__(self):
        # type: () -> None
        ObservableObject.__init__(self)
        self._values = {}

    def Get(self, name, default=None):
        # type: (str, object) -> object
        return self._values.get(name, default)

    def Set(self, name, value, propertyName=None, ...):
        # type: (str, object, str, ...) -> bool
        # Compares, invokes hooks, calls SetProperty, returns changed
```

### ViewModel Pattern

```python
class MyViewModel(ObservableBase):
    _PROP_NAME = "Name"
    _PROP_IS_CHECKED = "IsChecked"

    def __init__(self, data):
        # type: (object) -> None
        ObservableBase.__init__(self)
        self._apply_command = RelayCommandBase(self._on_apply, self._can_apply)
        self.Set(self._PROP_NAME, data.name)
        self.Set(self._PROP_IS_CHECKED, False)

    @property
    def Name(self):
        # type: () -> str
        return self.Get(self._PROP_NAME, "")

    @property
    def IsChecked(self):
        # type: () -> bool
        return bool(self.Get(self._PROP_IS_CHECKED, False))

    @IsChecked.setter
    def IsChecked(self, value):
        # type: (bool) -> None
        self.Set(self._PROP_IS_CHECKED, bool(value))

    @property
    def ApplyCommand(self):
        # type: () -> object
        return self._apply_command.Command

    def _on_apply(self):
        # type: () -> None
        pass

    def _can_apply(self):
        # type: () -> bool
        return self.IsChecked
```

### Convention Hooks

`ObservableBase.Set()` auto-calls hook methods by name if they exist:

```python
class MyViewModel(ObservableBase):
    def OnNameChanged(self, newValue):
        # type: (object) -> None
        # Called after Name changes
        pass

    def OnNameChanging(self, newValue):
        # type: (object) -> None
        # Called before Name changes
        pass

    def OnNameChangedEx(self, oldValue, newValue):
        # type: (object, object) -> None
        # Extended version with old value
        pass
```

### Dependent Properties

Use `Raise()` or `RaiseMany()` to notify computed properties:

```python
def OnIsFieldsChanged(self, newValue):
    # type: (object) -> None
    self.RaiseMany(["IsFilterEnabled", "IsSortingEnabled"])
```

### Command Invalidation

Use `SetAndNotifyCommands()` when property changes affect command availability:

```python
self.SetAndNotifyCommands(
    self._PROP_IS_CHECKED,
    value,
    commands=[self._apply_command]
)
```

## RelayCommandBase

Wraps `CommunityToolkit.Mvvm.Input.RelayCommand` for IronPython. Usage is shown in the ViewModel Pattern above.

Key points:
- `RelayCommandBase(execute)` — command that always can execute
- `RelayCommandBase(execute, canExecute)` — with availability check
- Bind `.Command` property in XAML: `<Button Command="{Binding ApplyCommand}" />`
- Call `RaiseCanExecuteChanged()` when availability state changes

### Typed Commands (RelayCommandTBase)

For commands that receive a parameter from XAML:

```python
from AureconMEP.WPF import RelayCommandTBase
from System import String

self._select_command = RelayCommandTBase(
    String,
    self._on_select,
    canExecute=self._can_select,
)
```

## WPFWindow

Extends MahApps `MetroWindow` with pyRevit integration.

### View Pattern

```python
# views/main_view.py
import os.path as op
from AureconMEP.WPF import WPFWindow

class MainView(WPFWindow):
    def __init__(self, view_model):
        # type: (MainViewModel) -> None
        xaml_file = op.join(op.dirname(__file__), "main_view.xaml")
        WPFWindow.__init__(self, xaml_file)
        self.DataContext = view_model
```

### XAML Template

```xml
<mah:MetroWindow
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:mah="http://metro.mahapps.com/winfx/xaml/controls"
    Title="My Tool"
    Width="400"
    Height="500"
    ShowMaxRestoreButton="False"
    ShowMinButton="False"
    WindowStartupLocation="CenterOwner"
    ResizeMode="CanResizeWithGrip">

    <Grid Margin="10">
        <!-- Content bound to DataContext (ViewModel) -->
        <Button Command="{Binding ApplyCommand}" Content="Apply" />
    </Grid>
</mah:MetroWindow>
```

### WPFWindow Features

- **Owner**: set to Revit's main window automatically (`set_owner=True`)
- **ESC to close**: handled by default (`handle_esc=True`)
- **Icon**: default Aurecon icon, override with `set_icon(path)`
- **Resource dictionaries**: MahApps styles merged automatically
- **Localization**: place `view.en_us.xaml` / `view.{locale}.xaml` alongside the base XAML

### Show Dialog

```python
# Modal (blocks until closed)
window.show_dialog()

# Non-modal
window.show()

# Temporarily hide window (e.g. for element picking)
with window.conceal():
    picked = uidoc.Selection.PickObject(...)
```

### Threading

Use `dispatch()` for background work that needs UI updates:

```python
def _on_apply(self):
    # type: () -> None
    self.owner_window.dispatch(self._do_work)

def _do_work(self):
    # type: () -> None
    # Runs in background thread
    # To update UI from here, use Dispatcher
    self.owner_window.Dispatcher.Invoke(
        System.Action(lambda: self.Set("Status", "Done")),
        Threading.DispatcherPriority.Background
    )
```

## Notifications

```python
from AureconMEP.WPF import NotificationContent, NotificationManager, NotificationType
from pyrevit.api import AdWindows

dispatcher = AdWindows.ComponentManager.Ribbon.Dispatcher
manager = NotificationManager(dispatcher)

manager.Show(NotificationContent(
    Title="Success",
    Message="Operation completed",
    Type=NotificationType.Success
))
```

`NotificationType` values: `Success`, `Error`, `Warning`, `Information`.

## Bindable List Items

Use the same ViewModel Pattern (above) for list items. XAML binding:

```xml
<ListBox ItemsSource="{Binding Items}">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <CheckBox IsChecked="{Binding IsChecked}" />
                <TextBlock Text="{Binding Name}" Margin="8,0,0,0" />
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```
