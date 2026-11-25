# Testing and Verifying UI with FlaUI

Three levels of proof, cheapest first. Stop at the level the change needs.

| Level | Proves | Tool |
|-------|--------|------|
| View test | A control does the right thing when driven through UIA | MSTest + FlaUI UIA3, in process |
| Live check | The real app, wired to real services, shows and does it | `scripts/uia.ps1` on a running process |
| Screenshot | It looks like the design, in Light and Dark | `uia.ps1 shot` or FlaUI `Capture` |

## Contents

- [View tests in process](#view-tests-in-process)
- [Live checks on a running app](#live-checks-on-a-running-app)
- [Waiting](#waiting)
- [Offscreen and virtualized items](#offscreen-and-virtualized-items)
- [Focus, DPI, theme](#focus-dpi-theme)
- [Screenshots against a design](#screenshots-against-a-design)
- [Flaky patterns and their fixes](#flaky-patterns-and-their-fixes)
- [Edge cases worth a test](#edge-cases-worth-a-test)

## View tests in process

The test owns a WPF dispatcher on an STA thread, shows a real window, and talks
to it through UIA from the test thread. UIA calls cross into the window's
thread, so the test thread must not be the dispatcher thread, or it deadlocks.

```csharp
using var ui = PanelUi.Open();
ui.Host.FindInWindow("Filter.Categories").Patterns.Toggle.Pattern.Toggle();
ui.Press("Filter.CategoryAll");
Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Categories.All(c => c.IsChecked)),
    "All did not tick every category.");
```

How the helpers in `tests/DevTools.ModelsContext.Views.Tests` fit:

- `WpfSta`: one background STA thread with an `Application` and a running
  `Dispatcher`. `Invoke` runs code on it and rethrows exceptions with their
  stack; `Run` awaits async view-model work.
- `ThemeHost`: shows the control in a borderless, non-activating window with
  the app theme merged, owns a `UIA3Automation`, and finds elements from the
  window HWND (`FindInWindow`), a context menu (`FindInMenu`), or a popup
  (`FindInPopup`), each with a retry and a clear assert message.
- `PanelUi`: builds the view model with fakes and a fake clock, so time-based
  behavior (debounce, live delay) advances on demand instead of by sleeping.

Rules for the test body:

- Act through UIA (`Invoke`, `Toggle`, `Select`, `SetValue`, `Expand`). Setting
  the view-model property directly skips the binding you are trying to prove.
- Assert either on UIA state (`ToggleState`, `IsSelected`, Name of a count
  text) or on the view model via `WpfSta.Invoke`. Read view-model state on the
  dispatcher thread.
- Mark the class `[DoNotParallelize]`; windows and focus are shared state.
- Dispose the window and the automation object in `Dispose`, or the next test
  finds the previous window.
- Name the test for the behavior in a few plain words (`AllChecksListedCategories`).

## Live checks on a running app

Use the script against the process you started, by id:

```powershell
$uia = '.agents/skills/ui-automation/scripts/uia.ps1'
$app = Start-Process <path-to-exe> -PassThru
pwsh -File $uia wait -ProcessId $app.Id -Id Explorer.Search -Timeout 15
pwsh -File $uia select -ProcessId $app.Id -Id ModelContext.View.Filter
pwsh -File $uia toggle -ProcessId $app.Id -Id Filter.SelectionSync -Value Off
pwsh -File $uia find -ProcessId $app.Id -Id Filter.ResultCount
pwsh -File $uia invoke -ProcessId $app.Id -Id Close
```

- `wait` before the first action: the window exists before its content loads.
- `-Root <id>` limits the search to one pane when the window is large (a host
  app with many panes); it is faster and avoids matching a same-named id
  elsewhere.
- For a pane hosted inside another app (a Revit dockable pane), target the host
  process and scope with `-Root` to the pane's root id. Do not act on the host's
  own UI unless asked.
- Close what you started. Leave running apps you did not start alone.

## Waiting

- FlaUI: `Retry.WhileNull(() => find(), timeout, ignoreException: true)` for
  "appears", `Retry.WhileFalse(() => condition, timeout)` for a state. Use
  `ignoreException` because an element can vanish between find and read.
- Script: `wait -Id X` and `wait -Id X -Gone`; every action already retries
  its find until `-Timeout`.
- A timeout is a failure with a message that says what was expected. Do not
  raise the timeout until the test passes; find out what it was waiting for.
- Debounce and delays: drive them with a fake clock in tests. In a live check,
  wait on the result, not on the delay length.

## Offscreen and virtualized items

- `IsOffscreen=True` means scrolled out or clipped. Patterns still work on it,
  but a screenshot will not show it. Call `ScrollItem.ScrollIntoView()` first
  when visibility matters.
- A virtualized list only lists realized rows (`items` shows `count=30` where
  the list has hundreds). To reach row N: ItemContainer `FindItemByProperty`
  then `VirtualizedItem.Realize()`, or `items -Realize` in the script.
- With recycling, re-find after any scroll; a held element may now be another row.
- A collapsed `ComboBox` lists no items. Expand, read or select, collapse.

## Focus, DPI, theme

- Patterns do not need focus, but on another process an action pattern
  (Invoke, Toggle, Select) still brings its window to the front; reads do not.
  Keyboard input and UIA `Focus()` take the keyboard as well. Prefer `SetValue`
  to typing, and keep live UI runs opt-in.
- An IME or a non-English layout rewrites typed keys (Vietnamese Telex turns
  "Door" into "Dổ"). The script types text as Unicode input to avoid that;
  `SetValue` avoids it entirely.
- `BoundingRectangle` is in physical pixels for the monitor; a 150% display
  makes every rect 1.5x the XAML size. Compare sizes as ratios, or run at 100%.
- Theme: run the same check in Light and Dark (the sandbox opens Dark and takes `--theme light`, the
  panel has a theme toggle). Colors come from theme brushes, so a Dark-only
  bug is a missing `DynamicResource`, not a test issue.

## Screenshots against a design

1. Capture the element, not the screen: `uia.ps1 shot -Root <pane id>` or
   `Capture.Element(element).ToFile(path)`. A capture copies screen pixels, so
   the window must be visible, not minimized, and not covered.
   `PrintWindow(hwnd, PW_RENDERFULLCONTENT)` (`WindowShot` in the Model Context
   UI tests) draws a covered window too.
2. A top-level window's rect includes the invisible resize border; the edges of
   a window shot can show what is behind it. Capture an inner element for a
   clean image.
3. Capture both themes and the states the design shows (empty, loading,
   error, overflow).
4. Compare by looking: alignment, spacing, truncation, overflow, colors in both
   themes, focus and selection visuals. Report differences in words with the
   PNG paths. Do not assert pixel equality in tests; fonts, DPI, and
   ClearType make it flaky.

## Flaky patterns and their fixes

| Symptom | Cause | Fix |
|---------|-------|-----|
| Passes alone, fails in the suite | Previous window not closed, or tests run in parallel | Dispose host and automation; `[DoNotParallelize]` |
| Element not found right after an action | UI not updated yet | Retry the find with a timeout |
| Click does nothing on CI | Coordinate click, no interactive desktop | Use the pattern |
| Popup item not found | Searched the main window; popup is its own HWND | Search the popup HWND or all process windows |
| Row N not found | Virtualized | Realize via ItemContainer, or scroll |
| Wrong row acted on | Recycled container held across a scroll | Re-find after scroll |
| Value set but not committed | Editable combo or text box commits on lost focus | Select the item, or assert the view model after the commit path |
| Text typed wrong | IME or keyboard layout | Value pattern, or Unicode input |
| Deadlock | UIA call made on the dispatcher thread | Call UIA from the test thread; only view-model reads go through `Invoke` |
| `ElementNotAvailableException` | Element destroyed between find and use | `ignoreException: true` and re-find |

## Edge cases worth a test

- Empty state shows its hint, and the hint goes when data arrives.
- Disabled controls report `IsEnabled=False` and actions on them do nothing.
- Read-only mode hides the add and remove buttons (assert they are absent).
- Long names: truncation and the overflow counter (`+N`).
- Toggling twice returns to the first state.
- Keyboard path: Enter commits, Esc cancels, losing focus commits or restores.
