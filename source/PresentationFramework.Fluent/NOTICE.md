# PresentationFramework.Fluent (vendored)

Pinned from https://github.com/dotnet/wpf branch `release/10.0`.

- Upstream commit: `87e4d30e28c1aaadf1866fa0bfdab110bbef1d6f`
- Source path: `src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent`
- License: MIT (see LICENSE.TXT)

Local changes for net48 / net8.0-windows hosts:

- SystemColors accent keys (added in .NET 9) replaced with fixed Color stand-ins
  so dictionaries resolve. Visual values stay the .NET 10 Fluent dictionaries.
- DocumentViewer styles that require PresentationUI types removed from Themes/*.xaml.
- Window backdrop DataTriggers that bind MS.Internal / Standard types unavailable
  on older TFMs removed from Themes/*.xaml.
- `SystemAccentColorSecondary` Color stand-in added where referenced.

This copy is compiled only by PresentationFramework.Fluent for net48 and
net8.0-windows. net10.0-windows does not compile it. The assembly is not
merged into RevitDevTool.
