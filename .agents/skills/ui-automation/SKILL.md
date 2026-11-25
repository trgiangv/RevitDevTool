---
name: ui-automation
description: >
  Design WPF so it can be automated, verify UI with FlaUI (view tests, live checks
  on a running app, screenshots against a design), and drive any Windows desktop
  app through UI Automation: find windows and modal dialogs, press dialog buttons,
  read text, type keys. Use whenever a task says "check the UI", "verify the tab
  after my change", "click this dialog", "click Always Load", "take a screenshot of
  the panel", "automate this app", or mentions AutomationId, FlaUI, UIA, or a UI
  test that is flaky, even when it does not name this skill.
---

# UI Automation

UI Automation (UIA) is how Windows exposes controls to screen readers and test
tools. Every control is an element with a ControlType, an AutomationId, a Name,
and patterns (Invoke, Toggle, SelectionItem, Value, ExpandCollapse, Scroll, ...).
Drive the app through those patterns and the result does not depend on screen
position, DPI, theme, focus, or which window is on top. That is the whole trick.

```text
design          ->  verify                      ->  drive
AutomationIds       view test (in process)          any app, by pid or title
right patterns      live check (scripts/uia.ps1)    dialogs, keys as fallback
peers, popups       screenshot vs design            safety limits
```

## Rules

1. **Find by AutomationId, act through a pattern.** Name and ControlType are
   fallbacks for apps you do not own. Never click by coordinates, hit-test, or
   raise synthetic mouse events: that input depends on the desktop and is what
   makes UI tests flaky. Mouse is a last resort for a third-party app that
   exposes nothing else, and you say so when you use it.
2. **Wait for a condition, never sleep.** Poll until the element exists, a
   state changes, or a timeout passes (FlaUI `Retry.WhileNull`/`WhileFalse`, or
   the script's `wait`). A fixed sleep is either too short on a slow machine or
   wastes time on a fast one.
3. **Assert state through UIA or the view model, not pixels.** Use a screenshot
   to compare with a design or to show the user, not as the test oracle.
4. **Do only what the user asked.** Touch only the target app. Leave every other
   window alone, including an open Revit or editor the user is working in.
5. **Stop at the safety limits** below; say what blocked you instead of working
   around it.
6. **A live UIA action uses the screen.** Invoke, Toggle or Select on another
   process's window brings that window to the front (seen on Revit and on a plain
   WPF window); reading (`find`, `tree`) does not. Do not add `Focus()` or
   `SetForeground` on top, and run live UI flows only when the user asked and
   is not working at the machine. API-level checks (MCP, host tests) need none
   of this.

## Pick the path

| The ask | Do this | Depth |
|---------|---------|-------|
| Add or fix a WPF control so tests can reach it | AutomationId on every interactive control, check the pattern it exposes, add an AutomationPeer if it hides one | [wpf-design.md](references/wpf-design.md) |
| "Verify the X tab after my change" | Run the view tests, then a live check with the script, then a screenshot if layout changed | [flaui-verify.md](references/flaui-verify.md) |
| Write or fix a FlaUI view test | Real window on an STA thread, find by id, act by pattern, wait on state | [flaui-verify.md](references/flaui-verify.md) |
| "Click Always Load" / any dialog button | `uia.ps1 dialog` to list it, then press by name | [desktop-automation.md](references/desktop-automation.md) |
| Automate another Windows app | `windows` and `tree` first, then act by pattern; keys if no pattern fits | [desktop-automation.md](references/desktop-automation.md) |

## The script: `scripts/uia.ps1`

A FlaUI UIA3 driver for PowerShell 7 on Windows. It loads FlaUI from the NuGet
cache (highest installed version), so nothing is installed. If the cache has no
FlaUI it prints one error that says to `dotnet restore` a project that
references `FlaUI.UIA3`.

```text
pwsh -File scripts/uia.ps1 <action> (-ProcessId N | -ProcessName exe | -WindowTitle 'pat*')
     [-Root <AutomationId>] [-Id <AutomationId>] [-Name 'pat*'] [-ControlType Button]
     [-Index n] [-Value v] [-Item 'pat*'] [-Title 'pat*'] [-Button 'pat*'] [-Keys Ctrl+S]
     [-Path out.png] [-Depth 3] [-Timeout 5] [-Gone] [-Realize] [-Yes]
```

| Action | Does | Needs |
|--------|------|-------|
| `windows` | Top-level windows of the target plus owned windows, popups, dialogs | target |
| `tree` | Depth-limited dump of the target, `-Root`, or the matched element | target |
| `find` | Every element matching `-Id`, `-Name`, `-ControlType` (all given must match) | a query |
| `invoke` | Invoke pattern; falls back to LegacyIAccessible default action | a query |
| `toggle` | Toggle; with `-Value On` or `Off` it is idempotent | a query |
| `select` | SelectionItem.Select; with `-Item` it expands a list or combo and selects the item | a query |
| `expand` / `collapse` | ExpandCollapse, waits for the new state | a query |
| `setvalue` | Value.SetValue (RangeValue for numbers) | a query, `-Value` |
| `items` | List, tree, menu, or tab items, expanding a combo first; `-Realize` walks virtualized items | a query |
| `wait` | Until the element exists, or with `-Gone` until it disappears | a query |
| `dialog` | Lists dialogs with their text and buttons; with `-Button` presses one | `-Title`, `-Button` |
| `keys` | Brings the window to the front, focuses the element, types `-Value` as Unicode, sends `-Keys` chord. Takes the user's keyboard: only for an app with no pattern | `-Value` or `-Keys` |
| `shot` | PNG of the element, `-Root`, or the main window | `-Path` |

Every element prints as one line, stable and easy to grep:

```text
Button id=Sandbox.Theme name='Dark' class=Button patterns=LegacyIAccessible,SynchronizedInput,Toggle enabled=True offscreen=False toggle=Off rect=384,143,47,28
```

Exit codes: `0` ok, `1` not found or timeout, `2` bad usage or setup, `3` the
element lacks the pattern (the line lists what it has), `4` refused for safety.
When several elements match, a `note:` line says so and `-Index` picks one.

Typical runs:

```powershell
$uia = '.agents/skills/ui-automation/scripts/uia.ps1'
pwsh -File $uia windows -ProcessId 1234
pwsh -File $uia tree -ProcessId 1234 -Root Filter.Rules -Depth 4
pwsh -File $uia select -ProcessId 1234 -Id Sandbox.Delay -Item '100 ms'
pwsh -File $uia toggle -ProcessId 1234 -Id Filter.SelectionSync -Value Off
pwsh -File $uia wait -ProcessId 1234 -Id Filter.NoRules -Gone -Timeout 10
pwsh -File $uia shot -ProcessId 1234 -Root ModelContext.View -Path out/filter.png
pwsh -File $uia dialog -ProcessName Revit -Title 'Security*'
pwsh -File $uia dialog -ProcessName Revit -Title 'Security*' -Button 'Always Load'
```

## Verify a WPF change (this repo)

1. Build and run the view tests for the area you touched; they open a real
   window and drive it by AutomationId. The patterns live in
   `tests/DevTools.ModelsContext.Views.Tests` (`ThemeHost`, `WpfSta`, `PanelUi`)
   and `tests/DevTools.HandyControl.Tests`. Expected skips and known gaps are in
   `docs/agents/test-matrix.md`.
2. Live check: run the app (`tests/DevTools.ModelsContext.Sandbox` hosts the
   Model Context panel without Revit:
   `dotnet run --project tests/DevTools.ModelsContext.Sandbox`), then use
   the script to read state and act. Close the app when done.
3. Layout or theme change: `shot` in Light and Dark and look at the PNGs next to
   the design. Say what differs in words; pixel equality is not the goal.
4. Report the commands and the output lines that prove the behavior.

AutomationIds are `<Area>.<Part>` and listed per area in
`docs/architecture/<Module>/ui.md`. Renaming one breaks tests on purpose; update
both together.

## Drive another Windows app

1. Target it by process id when you started it yourself, otherwise by window
   title. Run `windows` and `tree` before acting; read before you write.
2. Prefer, in order: a pattern on an element found by AutomationId, by Name and
   ControlType, LegacyIAccessible default action, keyboard (`keys`), and only
   then the mouse.
3. After every action, read the state back (the action line already does) or
   `wait` for the next element. Do not chain blind actions.
4. Dialogs: `dialog` without `-Button` first. It shows the title, the message
   text, and each button, so you can tell the user exactly what you are about
   to press.

## Safety limits

- **Credential prompts, UAC, the secure desktop, sign-in or payment forms:**
  never automate them. The user answers those. The script refuses known
  credential and elevation processes with exit `4`.
- **Elevation (UIPI):** a process that is not elevated cannot drive a window of
  an elevated one; patterns fail or return nothing. Tell the user the target
  runs as administrator. Do not elevate yourself or look for a bypass.
- **Destructive buttons** (Delete, Remove, Don't Save, Discard, Overwrite,
  Replace, Reset): confirm with the user first, naming the dialog and the
  button. The script refuses them without `-Yes`. Pass `-Yes` only after that
  confirmation, or when the data is a throwaway you created in this task.
- **Scope:** do not close, focus-steal, or type into windows you were not asked
  to touch. `keys` goes to the foreground window; target first, then send.
- **Startup dialogs in tests:** a test harness may already resolve them (the
  pytest bridge clicks only Always Load or Load Once). Do not add a second
  resolver that presses other buttons.

## References

- [references/wpf-design.md](references/wpf-design.md): AutomationIds, which
  control exposes which pattern, custom AutomationPeers, virtualization, popups,
  ComboBox Value, HandyControl quirks.
- [references/flaui-verify.md](references/flaui-verify.md): in-process view
  tests, live checks by process id, waits, offscreen items, focus, DPI, theme,
  screenshots vs a design, flaky patterns and fixes.
- [references/desktop-automation.md](references/desktop-automation.md): Win32,
  WinForms, WinUI, and browser-shell apps; dialogs (#32770, TaskDialog,
  ContentDialog); UIA3 vs UIA2 vs MSAA; keyboard and mouse fallbacks; limits.
