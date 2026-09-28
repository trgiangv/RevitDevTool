# 0037 HandyControl Replaces MahApps

Date: 2026-09-26

## Status

Accepted. Landed.

HandyControl source compiles into `DevTools.UI`. `DevTools.UI.dll` stays on
`RepackBinariesExcludes`. Hosts do not ILRepack WPF. MahApps.Metro,
ControlzEx, and Xaml Behaviors are gone. Do not add them back, and do not
ship `HandyControl.dll`.

## Context

Host add-ins ILRepack copy-local DLLs into the host assembly
([0019](0019-ilrepack-and-polyfill-isolated-alc.md)). WPF theme libraries were
the exception: `RevitDevTool.csproj` and `ACadDevTool.csproj` listed
`DevTools.MahApps.Metro.dll`, `DevTools.ControlzEx.dll`, and
`DevTools.Microsoft.Xaml.Behaviors.dll` in `RepackBinariesExcludes` so they
stayed loose. Those assemblies used absolute `pack://application:,,,/{Assembly};component/...`
URIs. After a merge the assembly name in the URI no longer exists, so the
dictionaries fail to load. Those three names are no longer exclude entries.

Revit does not create `System.Windows.Application`, so `Application.Current`
is null. Theme lookup therefore stops at the root of the tree that merged
the dictionary. MahApps `Styles/Controls.xaml` ends with keyless styles
(`<Style BasedOn="..." TargetType="Button" />` and the same for `TextBox`,
`ComboBox`, and the other stock controls). The resource key is the WPF
type, not the MahApps assembly. Renaming the fork to `DevTools.MahApps.Metro`
does not change that key. Any window that merges `Controls.xaml` restyles
every matching control in that tree, including controls that belong to
another library. Inheritable attached properties
(`ControlsHelper`, `TextBoxHelper`, `ItemHelper`) then flow through the
template that implicit style installed. That is the process-visible style
leak, and it does not need `Application.Resources`.

The product used to carry three forks for that stack: MahApps.Metro,
ControlzEx (window chrome / `MetroWindow`), and the Xaml Behaviors port.
Style keys were prefixed (`DevTools.MahApps.Styles.*`,
`DevTools.MahApps.Brushes.*`) to reduce string collisions. The prefix did
not stop implicit styles. Those forks are deleted.

HandyControl's combined theme and skins compile into `DevTools.UI`.
`ResourceUtils` and `ResourceHelper.GetComponentUri` build
`pack://application:,,,/{executing assembly};component/...`, so the name is
`DevTools.UI`, not `HandyControl`. Skin files use a relative source
(`Basic/Colors/Colors.xaml` and the dark/violet siblings). An absolute
`pack://application:,,,/HandyControl;component/...` URI looks for an
assembly this host does not ship.

`ConfigHelper.SetWindowDefaultStyle` calls `StyleProperty.OverrideMetadata`
on `System.Windows.Window` and is not part of theme load.
`HandyControl.Controls.Window` overrides metadata only for its own subclass.

Chrome is the other coupling. MahApps expects `MetroWindow` (ControlzEx
`WindowChrome` on the window). HandyControl chrome is the `hc:Window`
subclass, or a control-level overlay. Dialogs that are ordinary
`Window` instances are not pulled into a window-wide chrome style.

## Decision

1. **One UI fork.** HandyControl replaces MahApps.Metro, ControlzEx, and
   `Microsoft.Xaml.Behaviors` in Revit and AutoCAD hosts. Those three forks
   stay deleted.
2. **Compile HandyControl into `DevTools.UI`. Do not ILRepack WPF.** Do not
   ship `HandyControl.dll`. Do not merge `DevTools.UI` into the host.
   `DevTools.UI.dll` stays on `RepackBinariesExcludes`. Resource load uses
   `Assembly.GetExecutingAssembly()` and `/{DevTools.UI};component/Themes/...`.
3. **No host-wide implicit restyle.** Do not call
   `ConfigHelper.SetWindowDefaultStyle` or
   `SetNavigationWindowDefaultStyle`. Do not merge the HandyControl
   dictionary into `Application.Resources`. A view that needs a specific
   look sets `Style="{StaticResource ...}"`.
4. **Skins stay relative.** `SkinDefault.xaml`, `SkinDark.xaml`, and
   `SkinViolet.xaml` use a source relative to the skin dictionary
   (`Basic/Colors/Colors.xaml` and the dark/violet siblings). Do not
   restore `pack://application:,,,/HandyControl;component/...` inside
   those files.
5. **Chrome is per control.** New windows that need HandyControl chrome
   use `HandyControl.Controls.Window` or a control overlay on that
   window. Do not introduce a `MetroWindow` equivalent that restyles
   every `Window` in the tree.
6. **Daemon is the exception for `Application.Resources`.** Standalone
   `DevTools.Daemon` merges the HandyControl skin and theme compiled into
   `DevTools.UI` at application scope
   ([0032](0032-daemon-mewui-and-aot.md)). That reload does not call
   `ThemeManager`, so host views that merge `Theme/Theme.xaml` on each
   control are unchanged. That process does not host another add-in.
   Host add-ins still follow rule 3.

## Alternatives Considered

1. **Keep the three forks loose and keep prefixing MahApps keys.**
   Rejected. The leak is the implicit style key (`typeof(Button)`), which
   a renamed assembly and a prefixed brush key do not change. Three repos
   stay on the update path, and the DLLs stay out of ILRepack because of
   pack URIs.
2. **ILRepack `HandyControl.dll` into the host, then merge `DevTools.UI`.**
   Rejected. Pack URIs cannot name both `RevitDevTool` and `AcadDevTool`,
   and Release `/noRepackRes` can drop the BAML. Compiling the source
   into the loose `DevTools.UI` assembly keeps one component name.
3. **Ship `HandyControl.dll` beside the add-in.** Rejected. Another
   add-in in the same Revit process can load a different `HandyControl`
   with the same simple name. Compiling into `DevTools.UI` removes that
   identity from the host output.

## Consequences

Positive:

- One fork to patch instead of MahApps, ControlzEx, and Xaml Behaviors.
- Theme BAML and the code that loads it share `DevTools.UI`. Both hosts
  keep the same component name. `DevTools.UI.dll` stays loose, so ILRepack
  never has to rewrite WPF pack URIs.
- Window chrome does not require every dialog to be a `MetroWindow`.

Tradeoffs:

- `DevTools.UI.dll` carries the HandyControl source and stays a loose
  assembly under [0019](0019-ilrepack-and-polyfill-isolated-alc.md).
- Pack URIs inside the fork must use the executing assembly name. A
  hardcoded `HandyControl` component name does not resolve.

## Follow-Up

None. Do not reopen an ILRepack of `DevTools.UI` or `HandyControl`.
