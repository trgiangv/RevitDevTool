# 0036 Revit Link Tokens, Element Finder, And Tools Extract

Date: 2026-09-17  
Amended: 2026-09-20

## Status

Accepted

## Context

Monitor click-to-select (`RevitLinkifier` + `ElementSearcher`) resolves
`ElementId`, `UniqueId`, and IFC GUID only against the **active host
document**, then calls `Selection.SetElementIds` and `UIDocument.ShowElements`.

That is correct for host elements. It is wrong for elements that live in a
loaded RVT link:

- `ElementId` is unique **inside one document**. The same numeric id can exist
  in the host and in a link. Host lookup can miss or, worse, select the wrong
  host element.
- `Document.GetElement(string uniqueId)` and `FilteredElementCollector` do not
  walk `RevitLinkInstance.GetLinkDocument()`.
- `SetElementIds` / `ShowElements` accept host ids only. A linked element must
  be selected through a `Reference` created on the link instance.
- `LinkElementId` is the Revit identity of “element in a link”:
  `LinkInstanceId` + `LinkedElementId`. `Object.ToString()` on that type is the
  CLR type name, so interpolating `{linkElementId}` is not a usable token
  unless we format it ourselves.
- Zooming a linked element in the host view requires transforming its bounding
  box through `RevitLinkInstance.GetTotalTransform()`. Link-local min/max are
  not host-view coordinates when the instance is moved, rotated, or mirrored.

A second caller appeared: **Element Finder** — a chromeless tool window that lets
the user type the same token shapes (host or `linkInstanceId@inner`), pick a
document, select, and optionally frame with a section box. That is the same
select/zoom policy as the monitor, not a separate product.

Select/search must not live in `RevitDevTool.Core` (transactions / dockable
panes only — [0007](0007-revit-core-and-visualization-boundaries.md)). Shared
`DevTools.Logging` and ZLogger.Scintilla stay host-neutral.
[host-boundaries](../agents/host-boundaries.md).

## Decision

### 1. Identity: three kinds, optional `linkInstanceId` scope

Keep `TokenKind` as **ElementId, UniqueId, IfcGuid**. Do **not** add a
fourth kind. A linked hit is the same kind searched in
`RevitLinkInstance.GetLinkDocument()` when the token carries a host
`linkInstanceId` prefix.

Canonical scoped token (one tokenizer atom):

```text
{linkInstanceId}@{inner}
```

- Left side: decimal host `RevitLinkInstance` id (`Value` / `IntegerValue`),
  invariant culture, positive.
- Right side: **the same inner token as today** — ElementId digits, UniqueId
  (45 chars), or IFC GUID (22 chars).
- Exactly one `@`. `@` is not a monitor token separator (colon is; a colon
  pair would split and never match).
- Examples:
  - `8821@445566` — ElementId `445566` in instance `8821`
  - `8821@3d8e2c1a-4b5f-4a10-9c2e-7f1a0b3d4e5f-00003039` — UniqueId in that link
  - `8821@1aBcDeFgHiJkLmNoPqRsT_` — IFC GUID in that link

A pick `Reference` gives two `ElementId`s (`ElementId` = instance,
`LinkedElementId` = inner), not a `LinkElementId`. Write
`{instance.Id}@{linked.Id}` (or `{pick.ElementId}@{pick.LinkedElementId}`)
in the log line. UniqueId / IFC GUID stay `{instance.Id}@{inner}`. Direct
interpolation of a `LinkElementId` object is the CLR type name and is **not**
a click target.

Unscoped tokens (no `@`) keep today’s host meaning. The monitor line text is
the source of truth; typed `ParameterSpan` exists only on ZLogger
interpolations, not on `Trace` / `print`.

### 2. End-user logging: what to write, what appears, what click does

Three ingest paths hit the same monitor. They do **not** have the same
clickable surface:

| Path | How it reaches the monitor | Typed `ElementId` click |
|------|----------------------------|-------------------------|
| `ILogger` + `ZLog*` | ZLogger interpolator + `ParameterSpan` | Yes — `{element.Id}` is typed `ElementId` |
| `Trace.Write` / `WriteLine` | `LoggerTraceListener` bridge | **No** — the line is a string; a bare number is not UniqueId/IfcGuid/`@` pair |
| Python / IronPython `print` | stdout → `ConsoleRedirector` → `Trace` | **No** — same as `Trace` |

For `Trace` and `print`, log a **UniqueId**, an **IFC GUID**, or a **scoped**
`{instanceId}@{inner}` token. Do not expect `Trace.WriteLine($"wall {wall.Id}")`
to become a hyperlink.

Tokens must stand alone (whitespace or other separators around them). Do not
glue extra characters onto UniqueId (45 chars) or IFC GUID (22 chars).

#### Host element — `ILogger` (typed `ElementId`)

```csharp
logger.ZLogInformation($"Created wall {wall.Id}");
logger.ZLogDebug($"Host wall uid {wall.UniqueId}");
```

Monitor (examples):

```text
Created wall 12345
Host wall uid 3d8e2c1a-4b5f-4a10-9c2e-7f1a0b3d4e5f-00003039
```

Click `12345` → host `GetElement` → `SetElementIds` + `ShowElements`.
Click the UniqueId → host `GetElement` only.

#### Host element — `Trace` (no typed `ElementId`)

```csharp
Trace.WriteLine($"Created wall {wall.Id}");          // 12345 is NOT clickable
Trace.WriteLine($"Created wall {wall.UniqueId}");    // UniqueId IS clickable
Trace.WriteLine($"IFC {wall.get_Parameter(BuiltInParameter.IFC_GUID)?.AsString()}");
```

Use UniqueId (or IFC GUID) when the caller only has `Trace`.

#### Linked element — prefix the same three kinds

The linked element’s own `Id` / UniqueId / IFC GUID is resolved **in the link
document**. Prefix the host instance id so search does not run on the host:

```csharp
var instance = (RevitLinkInstance)hostDoc.GetElement(pick.ElementId);
var linked = instance.GetLinkDocument().GetElement(pick.LinkedElementId);

Trace.WriteLine($"Linked ElementId {instance.Id}@{linked.Id}"); // "8821@445566"
Trace.WriteLine($"Linked UniqueId {instance.Id}@{linked.UniqueId}");

logger.ZLogInformation($"Linked wall {instance.Id}@{linked.Id}");
```

Monitor:

```text
Linked wall 8821@445566
```

Click `8821@445566` → kind **ElementId**, document =
`instance.GetLinkDocument()`, id `445566` → `SetReferences` (2023+) +
host-space zoom.

Python / IronPython (`print` → Trace):

```python
print("Host wall {0}".format(wall.UniqueId))
print("Linked wall {0}@{1}".format(link_instance.Id, wall.Id))
print("Linked uid {0}@{1}".format(link_instance.Id, wall.UniqueId))
```

#### UniqueId / IFC GUID without instance

```csharp
Trace.WriteLine($"Linked uid {linked.UniqueId}");
```

Unscoped UniqueId / IFC GUID search the **host** document only. A linked
element needs `{instanceId}@{inner}`. Do not scan every loaded link to guess
the instance.

#### What not to log (no useful click)

```csharp
logger.ZLogInformation($"Linked {linked.Id}");
// monitor: "Linked 445566" — host ElementId search; miss or wrong host element

logger.ZLogInformation($"Linked {linkElementId}");
// monitor: "Linked Autodesk.Revit.DB.LinkElementId" — not a token we match

Trace.WriteLine($"wall {wall.Id}");
// monitor: "wall 12345" — bare number, no ParameterSpan → not clickable
```

### 3. Existing tokens stay host-first unless scoped

| Kind | Unscoped (`inner` only) | Scoped (`instanceId@inner`) |
|------|-------------------------|-----------------------------|
| `ElementId` | Host `GetElement` only. | That instance’s `GetLinkDocument()` only. Unloaded / missing → no-op. |
| `UniqueId` | Host `GetElement` only. | That instance’s link document only. |
| `IfcGuid` | Host parameter filter only. | That instance’s link document only. |

A scoped token never falls back to the host or to other instances. That is
what disambiguates duplicate placements of the same RVT.

### 4. Select and zoom are two steps; linked uses references + host-space box

Split presentation from search. Search returns a `SearchMatch`: host
ids or a linked instance + ids. Presentation then:

**Host match (unchanged intent):**

1. Optional: if the element is a `View`, `RequestViewChange` and stop.
2. Optional: if all matches share a host `OwnerViewId`, switch to that view.
3. `Selection.SetElementIds`.
4. `ShowElements` on visible ids (expand local tags as today).

**Link match:**

1. Do **not** call `SetElementIds` or `ShowElements` with linked ids.
2. Select (Revit **2023+**):

   ```csharp
   new Reference(linkedElement).CreateLinkReference(linkInstance)
   uiDocument.Selection.SetReferences(references)
   ```

3. Zoom the **active host** `UIView` (`GetOpenUIViews`, match `ActiveView.Id`)
   with `ZoomAndCenterRectangle` in **host coordinates**.
4. Revit **2022**: `SetReferences` is unavailable. Select the host
   `RevitLinkInstance` id instead, and still zoom the transformed box.

Monitor clicks do **not** mutate the view (no `View3D.SetSectionBox`, no
extra transaction). Zoom framing is enough. Element Finder may optionally set a
section box around the same host AABB (user action, not log click).

### 5. Linked bounding box: union in link space, then eight-corner transform

Linked `Element.get_BoundingBox(View)` / `get_BoundingBox(null)` is in the
**link document** coordinate system. Host zoom and section geometry need the
instance transform.

Algorithm (required; do not transform only the AABB min and max points):

1. Collect a non-null bounding box per target element in link space. Skip
   elements with no box.
2. Union as a component-wise AABB:

   ```text
   min = (min X, min Y, min Z) of all Min corners
   max = (max X, max Y, max Z) of all Max corners
   ```

   Do not pick one Min/Max **point** by sorting a single axis.
3. Emit the eight corners of that AABB.
4. Map each corner with `linkInstance.GetTotalTransform().OfPoint` (includes
   nested-link total transform).
5. Host AABB = component-wise min/max of the transformed corners. This is
   required when the instance has rotation or mirror; transforming only min
   and max independently is not an AABB in host space.
6. Optional pad: scale the host AABB about its center by **1.25** so a single
   small element is not flush to the view edge.
7. `uiView.ZoomAndCenterRectangle(hostMin, hostMax)`.

If every box is null, skip zoom (selection may still succeed). Catch
`ZoomAndCenterRectangle` failures and swallow them the same way a missed
search is a no-op — the click must not throw on the UI thread.

Linked `View` objects: do not `RequestViewChange` into the link. Zoom the
current host view only.

Independent tags: host tags keep `GetTaggedLocalElementIds()`. Tags that
point at linked elements use `GetTaggedElementIds()` (`LinkElementId`) and
follow the link presentation path.

### 6. Ownership: `RevitDevTool.Tools` + Element Finder

Extract Revit select/search (and Command Browser chrome that hosts Element
Finder) into `source/RevitDevTool.Tools/` (`UseRevit` + `UseWpf`). The host
add-in references it; Core does not.

| Surface | Owner |
|---------|--------|
| Token parse / search / select / box | `Tools/Selection/` (`TokenParser`, `TokenKind`, `ElementSearcher`, `ElementSelector`) + `Tools/Geometry/ElementBox` |
| Element Finder window + VM | `Tools/ElementFinder/` (`ElementFinderView`, `ElementFinderViewModel`, `ElementFinderService`, `DocumentItem`) |
| Floating tool chrome | `Tools/CommandBrowser/` (`ToolWindowService`, `ChromelessToolWindow`) |
| Command Browser bar | `Tools/CommandBrowser/` |
| Monitor hyperlink wiring | Host `RevitDevTool/Logging/Linkify/RevitLinkifier.cs` — calls `ElementSelector`; no duplicate select logic |

Public façade for callers outside Tools: `ElementSearcher` / `ElementSelector`.
Element Finder parse/clipboard is `ElementFinderService`. `ElementBox` stays
**internal**.

Element Finder:

- Same token grammar as the monitor (`inner` or `instanceId@inner`).
- Document picker includes the host and loaded link instances.
- Select uses §4; optional section box uses the same host AABB as §5.
- Chromeless floating window via `ToolWindowService` (one instance per key).

API year guards stay in Tools:

- `#if REVIT2023_OR_GREATER` around `SetReferences` / `CreateLinkReference`.
- ElementId parse stays `#if REVIT2024_OR_GREATER` (long vs int), including
  both sides of the link token.

AutoCAD monitor linkify is unchanged. No Revit API types in `DevTools.*`.

### 7. Out of scope (withdrawn)

Pick-to-overlay geometry inspect (GeoViz toolbar, `Inspect/*` pick sessions,
Primitive / Intersect / Host / Spatial / Annotate / Collect) is **not** part
of this product surface. Do not revive it under this ADR. Visualization
remains logging-path `Trace.Write` → DirectContext3D only
([0007](0007-revit-core-and-visualization-boundaries.md)).

### 8. Failure policy

Clicks and Element Finder miss stay silent: no document, invalid token, unloaded
link, deleted element, empty bounding box, zoom API throw. Do not show a
message box. Do not guess a host element for a linked token.

## Alternatives Considered

1. **Scan all links on bare `ElementId`.** Rejected — numeric collision with
   the host document can select the wrong element.
2. **Scan loaded links on unscoped UniqueId / IfcGuid.** Rejected — guessing
   the first loaded instance is wrong when the same RVT is placed twice, and
   the scoped token already names the instance. Unloaded → ignore.
3. **`ShowElements(linkInstance.Id)` instead of transformed zoom.** Rejected —
   that frames the whole instance, not the element.
4. **Transform only AABB min/max, or zoom with link-local corners.** Rejected —
   rotated/mirrored instances get a wrong or degenerate rectangle.
   Eight-corner transform is the rule.
5. **Create a 3D section box on monitor click.** Rejected — it writes the
   document and is surprising for a hyperlink. Element Finder may offer section
   box as an explicit user action.
6. **Stable `Reference` string as the token.** Rejected — opaque, long, and
   view-dependent. Scope is `linkInstanceId` plus a normal inner token.
7. **Keep select logic only under host `Logging/Linkify/`.** Rejected once
   Element Finder became a second caller — shared policy belongs in Tools;
   the host keeps `RevitLinkifier` only.
8. **Put select/search in `RevitDevTool.Core`.** Rejected — Core stays
   transactions / panes / image export ([0007](0007-revit-core-and-visualization-boundaries.md)).
9. **Teach ZLogger.Scintilla `ICustomSerializer` to format `LinkElementId`.**
   Insufficient alone: Scintilla serializers do not change ZLogger’s
   interpolator `ToString()`, so the monitor line would still show the type
   name. Helper-formatted text is the contract. Serializer work is optional
   pretty-JSON only, not required for click.
10. **Colon pair (`8821:445566`).** Rejected — `:` is a monitor token
    separator, so the linkifier would see `8821` and `445566` as two tokens.
11. **Shape-match every integer from `Trace` as `ElementId`.** Rejected —
    timestamps, counts, and years would all become clicks. `Trace` / `print`
    use UniqueId, IFC GUID, or `{instanceId}@{inner}`.
12. **Fourth `TokenKind.LinkElementId`.** Rejected — the inner token is
    still ElementId, UniqueId, or IfcGuid. `linkInstanceId` is search scope,
    not a new kind.
13. **Ship a GeoViz / Inspect pick-to-overlay toolbar.** Rejected / withdrawn —
    not needed; Visualization stays `Trace.Write` driven.

## Consequences

Positive:

- Linked elements are selectable and framed at the instance transform, not
  mis-resolved as host ids.
- Token contract is still ElementId / UniqueId / IfcGuid; `linkInstanceId@`
  only chooses the link document.
- Host `ElementId` clicks keep their current, collision-safe meaning.
- Element Finder and monitor share one select/zoom policy in Tools.
- Shared logging stays host-neutral; Core stays free of select/search.

Tradeoffs:

- Callers must prefix `{instanceId}@` to click a linked element. Unscoped
  UniqueId / IfcGuid stay host-only. `{element.Id}` from a link document still
  means “host ElementId search”. `Trace` / `print` cannot click a bare
  `ElementId` number; they need UniqueId, IFC GUID, or `{instanceId}@{inner}`.
- Revit 2022 highlights the link instance, not the nested element.
- Extra Revit TFM project (`RevitDevTool.Tools`) in the host matrix.
- Live-host proof is required; headless tests cannot exercise
  `SetReferences` / `UIView` zoom.

## Follow-Up

- Token + zoom landed 2026-09-17 (plan:
  `docs/plans/completed/2026-09-17-revit-link-element-monitor-tokens.md`).
- 2026-09-19: moved Selection / Geometry / CommandBrowser into
  `source/RevitDevTool.Tools/` (renamed from Commands). Host `RevitLinkifier`
  uses `ElementSelector`. GeoViz / Inspect pick UI abandoned (no separate ADR).
- 2026-09-20: Element Finder folder (`Tools/ElementFinder/`) replaces Search
  Ids under CommandBrowser; `ElementFinderService` owns parse/clipboard.
  `IdSelectService` folded into `ElementSelector`; `RevitTokenKind` → `TokenKind`;
  `LinkToken` → `TokenParser`.
- Product: `docs/product/logging.md`. Architecture:
  `docs/architecture/Logging/README.md`. Boundaries:
  `docs/agents/host-boundaries.md`.
