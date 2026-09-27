# Execution Plan: HandyControl Replaces MahApps

Date: 2026-09-26

## Status

Completed 2026-09-27. Policy:
[0037](../../decisions/0037-handycontrol-replaces-mahapps.md).

## Outcome

HandyControl source compiles into `DevTools.UI`. `DevTools.UI.dll` stays on
both hosts' `RepackBinariesExcludes`. There is no `HandyControl.dll` in the
host output. MahApps.Metro, ControlzEx, and Xaml Behaviors are gone from
`libs/`, project references, and the exclude lists.

The cutover that would ILRepack `DevTools.UI` into the host and rewrite
`/DevTools.UI;component/...` pack URIs is withdrawn. WPF resources stay in
the loose assembly, so ILRepack never has to repack them.

## What landed

- `DevTools.UI.csproj` compiles `HandyControl_Shared.projitems` and runs
  `XamlCombine` before markup compile. `LinkHandySharedItems` keeps BAML
  names as `Themes/...`.
- `ResourceUtils` and `ResourceHelper.GetComponentUri` use
  `Assembly.GetExecutingAssembly()`.
- `SkinDefault.xaml`, `SkinDark.xaml`, and `SkinViolet.xaml` source
  `Basic/Colors/...` with a relative path.
- Product views use HandyControl controls. The local color picker keeps
  `SelectedColor`.

## Result

Reviewed against the tree on 2026-09-27. No further packing work.
