# Execution Plan: Revit Monitor Link-Element Tokens And Host-Space Zoom

Date: 2026-09-17

## Status

Completed 2026-09-17

## Outcome

Clicking a monitor token for a **linked** element selects that element in the
correct `RevitLinkInstance` and zooms the active host view to the element’s
transformed bounding box. Host `ElementId` / `UniqueId` / IFC GUID clicks keep
today’s host-document behavior. Linked clicks require `{instanceId}@`; an
unloaded instance is a silent miss.

Observable:

- Token `8821@445566` (ElementId in that link) is clickable; same prefix
  works with UniqueId / IFC GUID inners.
- Linked select uses `Reference.CreateLinkReference` + `Selection.SetReferences`
  on Revit 2023+.
- Zoom uses eight-corner `GetTotalTransform` + `UIView.ZoomAndCenterRectangle`.
- Unscoped tokens never scan links.
- Misses stay silent (no throw, no message box).

## Context

- Decision (authority): [0036](../../decisions/0036-revit-monitor-link-element-tokens.md)
- Product (update on land): `docs/product/logging.md`
- Architecture (update on land): `docs/architecture/Logging/README.md`
  (Revit-specific extensions only)
- Code today: `source/RevitDevTool/Logging/Linkify/RevitLinkifier.cs`,
  `ElementSearcher.cs`, `Logging/Enums/RevitTokenKind.cs`
- Boundaries: host-only; no `DevTools.*` Revit types
- Gaps: `docs/agents/test-matrix.md` — `RevitDevTool` is out of the headless
  coverage gate; do not add fake host tests “for %”

## Scope

In scope:

- `LinkToken` parse of `{instanceId}@{inner}` (three kinds)
- Linkifier match for the pair token
- Search: host-only for unscoped kinds; scoped token resolves that
  `RevitLinkInstance` (`GetLinkDocument()` null → ignore)
- Presenter split: host path vs link path (select + zoom)
- IndependentTag expansion via `GetTaggedElementIds()` when the tag targets a
  link
- `#if REVIT2023_OR_GREATER` selection; 2022 instance-select fallback
- Product + logging architecture one-layer updates after code lands
- Compile `RevitDevTool` 2022 and 2025 (year-gated APIs)

Out of scope:

- AutoCAD linkify
- 3D section-box mutation on click
- Changing ZLogger interpolator / Scintilla `ICustomSerializer` as a
  prerequisite (optional pretty-JSON later)
- Scanning links on bare `ElementId`
- Moving presenter into `RevitDevTool.Core`
- Headless coverage % for `RevitDevTool`
- Nested-link UI beyond `GetTotalTransform` (already the total host transform)

## Approach

Keep `RevitLinkifier` thread-safe (pump thread): match + parse only, then
`HostUiHelper.RunOnMainThread` for search/present.

Suggested files (all under `source/RevitDevTool/Logging/`):

| File | Role |
|------|------|
| `Enums/RevitTokenKind.cs` | Unchanged: ElementId, UniqueId, IfcGuid |
| `Linkify/LinkToken.cs` | `TrySplit` of `{instance}@{inner}` |
| `Linkify/RevitLinkifier.cs` | Match pair token; keep existing three kinds |
| `Linkify/ElementSearcher.cs` | Public `TrySearch`; resolve a `SearchMatch` |
| `Linkify/ElementSelector.cs` | Host select/show vs link references + zoom |
| `Linkify/LinkBoundingBox.cs` | Union AABB, eight corners, transform, pad 1.25 |

Do not grow `ElementSearcher` into select+zoom+geometry in one type.

### 1. Token format/parse

`LinkToken.TrySplit`:

- Split on a single `'@'` (not `':'` — colon is a monitor token separator)
- Left: positive instance id (`long` 2024+, `int` before)
- Right: inner token classified as UniqueId (45), IfcGuid (22), or ElementId digits
- Reject extra `@`, empty inner, signs, thousands separators

End-user log lines write `{instance.Id}@{linked.Id}` (and UniqueId / IFC GUID
the same way). The monitor matches the text. Host elements stay unscoped
`{element.Id}`.

Linkifier match order:

1. `TrySplit` + classify inner → same three kinds, with `linkInstanceId`
2. `ParameterSpan` `Autodesk.Revit.DB.ElementId` → unscoped ElementId
3. UniqueId length 45
4. IfcGuid length 22

Scoped match is **shape-based** so Trace/`Format()` strings click. False
positives (`2025@1`) click and no-op if nothing resolves.

Do not treat `Autodesk.Revit.DB.LinkElementId` `ToString()` (type name) as a
hit. Do not add a fourth enum value.

### 2. Search matches

Replace `object` return with an explicit result:

```text
SearchMatch =
  HostElements(ICollection<ElementId>)
  | LinkedElements(RevitLinkInstance instance, ICollection<ElementId> ids)
```

Scoped (`linkInstanceId` set): resolve that `RevitLinkInstance`,
`GetLinkDocument()`, run the **same** ElementId / UniqueId / IfcGuid finder
on the link document only. No host fallback, no other instances.

Unscoped ElementId: host only.

Unscoped UniqueId / IfcGuid: host only. No scan of loaded links.

### 3. Presenter — host

Move current `ShowElements` / `HasSameOwnerView` / local-tag expand here.
Behavior unchanged for host matches.

### 4. Presenter — linked

Select:

```text
#if REVIT2023_OR_GREATER
  refs = linked.Select(e => new Reference(e).CreateLinkReference(instance))
  uiDoc.Selection.SetReferences(refs)
#else
  uiDoc.Selection.SetElementIds([instance.Id])
#endif
```

Zoom (all years):

1. `LinkBoundingBox.TryGetBoundingBox(elements, instance, pad: 1.25)`
2. `uiDoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == activeView.Id)`
3. `uiView.ZoomAndCenterRectangle(aabb.Min, aabb.Max)` inside try/catch

`LinkBoundingBox`:

1. `element.get_BoundingBox(null)` first; if null, `get_BoundingBox(activeView)`
2. Component-wise union in **link** space
3. Eight corners → `GetTotalTransform().OfPoint`
4. Component-wise host AABB
5. Scale about center by 1.25

Never pass link-local min/max to `ZoomAndCenterRectangle`.
Never transform only the two AABB endpoints.

Tags: if a resolved host element is `IndependentTag`, union
`GetTaggedLocalElementIds()` into the host path; union `GetTaggedElementIds()`
into the link path when `LinkInstanceId` is valid.

### 5. Docs (after code)

One layer each, no duplication of 0036:

- `docs/product/logging.md`: clickable kinds, pair token, linked zoom, 2022
  fallback, silent miss
- `docs/architecture/Logging/README.md`: point at `ElementSelector` /
  `LinkToken`; link 0036

### 6. Proof

1. Compile host 2025 (primary) and 2022 (2022 fallback + int ElementId):

   ```text
   dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025
     -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
   dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2022
     -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
   ```

2. No new `tests/*.Tests` project for this host UI path (`test-matrix.md`).
   Optional: a tiny parse-only test only if `LinkToken` is extracted to a
   host-free type — **do not** extract just for coverage.

3. Live Revit checklist (manual, host running):

   - Host wall `ElementId` still selects and `ShowElements`
   - Host UniqueId / IFC GUID still work
   - Log `{instance.Id}@{linked.Id}` / `8821@445566` for an element
     in a **moved and rotated** link → click selects the element (2023+) and
     zooms onto it, not the whole instance, not a host element with the same
     numeric id
   - Same file placed twice → pair token hits the named instance
   - Unscoped UniqueId of a linked element → host miss, silent (no link scan)
   - Unloaded link → click no-op
   - Revit 2022 (if available): instance selected, zoom still on the element
     box

## Risks And Recovery

- **False-positive pair tokens** (`2025@1`). Mitigation: both sides `> 0`;
  miss is silent. Do not add a `link@` prefix unless live logs prove noise.
- **`SetReferences` year / API exceptions.** Gate 2023+; catch and still zoom.
- **Null bounding boxes** (some types, annotations). Skip zoom; keep select.
- **Active view is a sheet / drafting view.** `ZoomAndCenterRectangle` may
  throw — swallow.
- **Pump-thread vs Revit API.** Linkifier must not touch `Document` on match.
- **ZLogger `ToString()` of `LinkElementId`.** Document helper; do not block
  on Scintilla serializer (it would not rewrite the interpolator anyway).
- Recovery: revert the Linkify folder + token enum; host `ElementId` path
  remains the previous code.

## Progress

- [x] 0036 accepted (token pair, host-first ElementId, eight-corner zoom)
- [x] `LinkToken` + three kinds (`linkInstanceId@` scope, no fourth kind)
- [x] `RevitLinkifier` pair match
- [x] `SearchMatch` (scoped instance only; no loaded-link scan)
- [x] `ElementSelector` host vs link
- [x] `LinkBoundingBox` eight-corner transform + 1.25 pad
- [x] Tag expansion for linked tagged elements
- [x] Product + architecture logging updates
- [x] Compile 2025 + 2022
- [x] Live-host checklist (user verified select + zoom)

## Decisions

- 2026-09-17: Lasting policy is 0036; this file is execution memory only.
- 2026-09-17: Shape-match the pair even when `ClrType` is `string` so
  `Format()` interpolations click.
- 2026-09-17: Do not require a Scintilla/`ICustomSerializer` change for v1.
- 2026-09-17: Pair delimiter is `@` because `:` is a Scintilla token
  separator. End-user Trace/print examples live in 0036 §2.
- 2026-09-17: No fourth token kind. `linkInstanceId@` scopes ElementId /
  UniqueId / IfcGuid to that `GetLinkDocument()`.
- 2026-09-17: Do not scan loaded links on unscoped UniqueId / IfcGuid.
  Resolve the named `RevitLinkInstance`; unloaded → ignore.

Promote further lasting choices to 0036 rather than expanding this list.

## Validation

- Focused proof: `LinkToken.TrySplit` exercised by compile +
  live clicks (no new testhost unless a host-free parser is later extracted).
- Integration: live Revit checklist above (identity transform **and** rotated
  instance).
- Repository-required checks: host compile 2025 and 2022 with compile-only
  props (build skill). Do not `kill-host` all years.

## Result

Compile (compile-only props; no deploy / ILRepack):

```text
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025
  -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
→ pass (0 warnings, 0 errors)

dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2022
  -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
→ pass (0 warnings, 0 errors)
```

Live-host checklist: **passed** 2026-09-17 (user verified). Click on
`{instanceId}@{inner}` selects the linked element (2023+) and zooms the
active host view to the transformed box. Plan moved to
`docs/plans/completed/`.
