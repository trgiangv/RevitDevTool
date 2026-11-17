# Automating Any Windows Desktop App

UIA works for Win32, WinForms, WPF, WinUI, UWP, and most Electron or Chromium
apps. What differs is how much each framework exposes. Read the tree first,
then pick the least fragile way to act.

## Contents

- [Find the target](#find-the-target)
- [Windows, owned windows, dialogs](#windows-owned-windows-dialogs)
- [Press a dialog button](#press-a-dialog-button)
- [Read text](#read-text)
- [Order of fallbacks](#order-of-fallbacks)
- [UIA3, UIA2, and MSAA](#uia3-uia2-and-msaa)
- [Framework notes](#framework-notes)
- [Limits and safety](#limits-and-safety)

## Find the target

| You have | Use | Why |
|----------|-----|-----|
| A process you started | `-ProcessId $p.Id` | Exact; never hits another instance |
| One running instance | `-ProcessName notepad` | The script stops if more than one matches and lists the pids |
| Only a title | `-WindowTitle 'Report*'` | Wildcard; titles change with the open document |

Some launchers start a different process than the one you ran (Store apps,
`notepad.exe` on Windows 11, apps with an updater). Take the process list before
and after, and target the new pid. An app with a single-instance policy may
hand your request to a window that already holds the user's work; check
`windows` before typing anything.

## Windows, owned windows, dialogs

`uia.ps1 windows` lists the target's top-level windows and every Window element
below them. That covers:

- **Owned windows and modal dialogs**: UIA places an owned top-level window
  under its owner. `modal=True` comes from the Window pattern.
- **Win32 dialogs**: class `#32770` (MessageBox, the classic add-in security
  prompt, file dialogs). Buttons are Button elements with the label as Name,
  including any `&` mnemonic (the script strips `&` when matching).
- **TaskDialog**: also `#32770`; command links show as Buttons, the expanded
  text and verification checkbox are separate elements.
- **WPF popups and menus**: separate windows of class `Popup`.
- **WinUI ContentDialog** (Windows 11 Notepad, Settings): a Window of class
  `Popup` with `modal=True` inside the main window, buttons with ids such as
  `PrimaryButton`, `SecondaryButton`, `CloseButton`.
- **Teaching tips and toasts**: windows that are offscreen most of the time.
  The `dialog` action ignores offscreen windows and prefers modal or `#32770`
  ones.

## Press a dialog button

```powershell
pwsh -File $uia dialog -ProcessId $pid
pwsh -File $uia dialog -ProcessId $pid -Title 'Notepad' -Button "Don't save" -Yes
```

The first call prints the dialog, its text lines, and its buttons. Read the
message before choosing a button. The second presses by Name (wildcards allowed;
straight and curly apostrophes match each other), through Invoke or, when the
button has none, the LegacyIAccessible default action. It then reports
`closed=True` once the dialog is gone.

Startup dialogs that block a host (an "unsigned add-in" security prompt) are
answered with the button the user asked for, usually `Always Load` or
`Load Once`. Never pick `Do Not Load` or `Cancel` on your own, and do not press
anything when a test harness is already resolving the same dialog.

## Read text

- Labels and static text: Text elements; their Name is the text.
- Edit boxes and documents: Value pattern (`value='...'` in the line), or the
  Text pattern for large documents (FlaUI `Patterns.Text.Pattern.DocumentRange.GetText(-1)`).
- Win32 controls without UIA support: LegacyIAccessible `Name` and `Value`.
- Lists and grids: `items`, then each row's Name; grid cells via the Grid and
  Table patterns in FlaUI.
- Status bars: often a single Text or a StatusBar with Text children.

## Order of fallbacks

1. Pattern on an element found by AutomationId.
2. Pattern on an element found by Name plus ControlType (Name changes with
   language; pin the ControlType to avoid matching a label).
3. LegacyIAccessible `DoDefaultAction` (MSAA), for old Win32 and owner-drawn
   controls that still answer MSAA.
4. Keyboard: `keys -Keys 'Alt+F4'`, accelerators, Tab and Enter. It needs the
   window in the foreground and is sensitive to focus and IME; read the state
   back after each step.
5. Mouse: only when nothing above works (a canvas, a game, a custom-drawn
   control with no accessibility). Use FlaUI `Mouse.Click` on the element's
   clickable point, tell the user it is fragile, and never use it in a test.

## UIA3, UIA2, and MSAA

- **UIA3** (`FlaUI.UIA3`, COM `IUIAutomation`): the default. Best for WPF,
  WinUI, UWP, Win32, and Chromium. The script uses it.
- **UIA2** (`FlaUI.UIA2`, managed `System.Windows.Automation`): better for some
  WinForms controls and older apps where UIA3 shows a flat or empty tree. To
  try it, reference `FlaUI.UIA2` and swap `UIA3Automation` for `UIA2Automation`;
  the rest of the FlaUI API is the same. Do not mix elements from the two.
- **MSAA / LegacyIAccessible**: the oldest layer. UIA3 exposes it as the
  LegacyIAccessible pattern on almost every element (`Name`, `Value`, `Role`,
  `State`, `DoDefaultAction`). Use it when the UIA name or pattern is missing.
- Chromium and Electron build their accessibility tree lazily; the first query
  can return a near-empty tree. Query again after a moment, or start the app
  with `--force-renderer-accessibility`.

## Framework notes

| Framework | Ids | Watch out for |
|-----------|-----|---------------|
| WPF | `AutomationProperties.AutomationId`, else `x:Name` | Popups are separate windows; virtualization |
| WinForms | Control `Name` becomes AutomationId | Owner-drawn lists and grids expose little; try UIA2 or MSAA |
| Win32 | Control id as a number string | Buttons have `&` mnemonics; `#32770` dialogs |
| WinUI / UWP | `AutomationProperties.AutomationId` | ContentDialog lives inside the window; teaching tips |
| Chromium / Electron | Rarely set; use Name + ControlType | Lazy tree; web content under a Document element |
| Qt, Java, games | Often none | Java needs the Access Bridge; games: keyboard or nothing |

## Limits and safety

- **Credential and elevation prompts** (UAC `consent.exe`, Windows Security
  credential dialogs, sign-in pages, password managers, payment forms): do not
  automate them, read their fields, or type into them. Ask the user to answer.
  The secure desktop that UAC uses is unreachable anyway.
- **UIPI**: a process at a lower integrity level cannot send input to or use
  patterns on a higher one. A non-elevated shell cannot drive an app that runs
  as administrator; finds return little and actions fail with access denied.
  Report "the target runs elevated" and stop. Do not restart yourself elevated
  or use a workaround.
- **Destructive actions**: Delete, Remove, Don't Save, Discard, Overwrite,
  Replace, Reset, End task, Format, Uninstall. Name the dialog and button to
  the user and wait for a yes. The script needs `-Yes` for these. A throwaway
  document you created in the same task is the only exception.
- **Other people's work**: an editor may restore the user's open documents on
  start. List the tabs or windows before acting, act only on what you created,
  and leave the rest untouched.
- **Remote and locked sessions**: UIA needs an interactive desktop. A locked
  screen, a minimized RDP window, or a service session gives an empty tree or
  black screenshots.
- **Screenshots** show whatever is on screen at that rectangle, including other
  apps and private content. Capture the element you need, not the desktop, and
  do not share captures of windows the user did not point at.
