# Parameter Catalog and Caches

Where the parameter picker and the value drop-down get their data, and how it
stays fresh without re-reading the model on every click.

## Catalog per document (`ParameterCatalogReader`)

One catalog per `SourceId`, built in slices on the Revit thread:

1. **Filterable parameters per category.** For each model category Revit can
   filter on, `GetFilterableParametersInCommon(document, [category])`, 20
   categories per call. Each id records that category.
2. **One sampled element per type.** Walk the model element ids; for the first
   element of each type (or of each category when it has no type) read every
   parameter of the element and of its type. This finds family parameters
   (gotcha: they are not filterable-in-common) and gives built-ins their storage
   and spec. The element's category is recorded on each parameter, and the
   binding (Instance or Type) comes from where it was seen.
3. **Build.** Turn each id into a `ParameterInfo` (key, name, kind, storage,
   spec label, origin, binding, categories, and for the tooltip: discipline,
   group and families) and merge infos with the same key. Discipline is
   `UnitUtils.GetDiscipline` of a measurable spec only; group is the
   definition's `GetGroupTypeId` label (the Properties palette section), read
   from the `ParameterElement` or the first sampled `Parameter`, and left out
   when unknown; families are the family names of the sampled elements that
   carry a non-built-in parameter. Labels are cached per spec and group id
   during one build. The core adds `Models` (the documents that have the key)
   and `CategoryNames` when it merges catalogs per scope.
   Origin is Shared for a `SharedParameterElement`, Project when it has a
   `ParameterBindings` entry, else Family. The key → ids map is kept for the
   compiler. A built-in that was only sampled (not filterable in common) is
   listed too, unless its storage is None.

### Sampled built-ins

`GetFilterableParametersInCommon` leaves out built-ins that native rules still
filter on, for example `RBS_START_LEVEL_PARAM` (Reference Level) on ducts: an
equals rule on L3 passes 319 ducts. The picker lists them, scoped to the
categories they were seen on, except `ParameterCatalogReader.Unfilterable`.

**Check.** L2 case SB-01 (`CatalogTests`, helper `Sample/SampledBuiltIns.cs` in
`tests/RevitDevTool.ModelsContext.LiveTests`, read-only) samples one element
per type like the catalog, and for every sampled-only built-in and every sample
with a value builds an equals rule from that element's own display text
(`ParameterValues.ValueText`) and compiles it with the panel's own
`FilterCompiler`, `ParameterLocator`, `ValueReader` and `RuleValueParser`. A
double is compiled as a pick (the stored range `ValueReader` records for that
text), the way a user takes it from the value list. The element must match;
the case fails with the failing parameters and elements.
See [test-matrix.md](test-matrix.md).

Revit 2026, Snowdon Towers HVAC and its six links, after the fixes:

| Document | Sampled built-ins | With a value | Samples | Passed | Failed |
|----------|-------------------|--------------|---------|--------|--------|
| HVAC | 130 | 97 | 632 | 632 | 0 |
| Architectural | 287 | 257 | 5,566 | 5,566 | 0 |
| Structural | 209 | 171 | 1,477 | 1,477 | 0 |
| Facades | 139 | 114 | 1,158 | 1,158 | 0 |
| Plumbing | 132 | 100 | 845 | 845 | 0 |
| Electrical | 209 | 173 | 1,338 | 1,338 | 0 |
| Site | 143 | 112 | 654 | 654 | 0 |

What failed before, and why:

| Parameter | Failure | Cause | Fix |
|-----------|---------|-------|-----|
| `ELEM_CATEGORY_PARAM`, `_MT` | every sample | the value is a category id; its text ("Walls") was looked up among levels, phases, materials and types | Reference text maps through the value index ([filters.md](filters.md#value-kinds-and-operators)) |
| `DESIGN_OPTION_ID` | every sample in an option | option names ("Middle Stair  <primary>") were not in that lookup; main-model elements hold id -1 and have no value text, so they are not samples | same |
| Lengths, areas, volumes, slopes, electrical loads (`RBS_OFFSET_PARAM`, `RASTER_SHEETHEIGHT`, …) | samples whose stored value is not on the display step | the rule used tolerance 1e-6 against the rounded text; slopes were parsed with the host's units | then half a display step in the target document's units; now a value from the list is a pick that keeps its stored range ([filters.md](filters.md#value-kinds-and-operators)) |
| Type parameters of curtain panel 1055511 (`ALLOW_AUTO_EMBED`, `AUTO_MULLION_BORDER1_HORIZ`, …) | that element | its type is a Walls type, not in the Curtain Panels type set, so *defined* failed | `TypePresence` also covers types used by the elements |
| `REBAR_ELEM_LENGTH` | both samples | the native rule reads a rounded bar length (11' - 10" for a bar whose value is 11' - 10 3/8"; 26' for 25' - 11 1/2") | excluded: `Unfilterable` |

Before the fixes the check failed 366 of 11,672 samples.

**Enum detection.** For an integer parameter that is not Yes/No, the first
sampled value with display text decides: text that does not parse as a number
means named values, so the kind is Enumerated.

**Category membership** is what lets the picker follow the checked categories:
the panel lists the union of parameters whose `Categories` overlap the checked
ones (`CategoryIdSet`, a sorted array with binary search).

### Why a per-type walk

Measured live on Revit 2026 (Snowdon Towers):

| Document | Per-type walk | Per-family collectors |
|----------|---------------|-----------------------|
| HVAC (Current model) | 75 ms, 597 parameters | 1,984 ms, 348 parameters |
| Architectural link | 410 ms, 1,849 parameters | 8,766 ms, 1,539 parameters (missed 310) |

One collector pass plus one sample per type is both faster and more complete
than a collector per family, so the per-type walk was kept.

### Build time with the tooltip facts

L2 CT-01 reads both catalogs on a cold `ParameterCache` and records the panel's
own Trace lines. Revit 2026, same session warm-up, before (run
`20261117-214846`) and after the discipline, group and family reads (run
`20261117-222012`, two passes):

| Step | HVAC before | HVAC after | Architectural before | Architectural after |
|------|-------------|------------|----------------------|---------------------|
| Catalog samples (run) | 9.4 ms | 8.6 / 7.4 ms | 76.2 ms | 101.3 / 74.8 ms |
| Catalog build (run) | 4.2 ms | 4.3 / 3.4 ms | 20.6 ms | 10.9 / 12.1 ms |
| Whole catalog (test timer) | 180 ms | 45 / 18 ms | 127 ms | 130 / 102 ms |

The extra reads stay inside the run-to-run noise.

## `ParameterCache` (adapter)

One instance per pane, shared by the catalog reader, the filter evaluator and
the event hub. It holds:

| Entry | Key | Value |
|-------|-----|-------|
| Catalog | `SourceId` | `ParameterInfo` list + key → ids |
| Values | `ValueKey(SourceId, ParameterKey, GroupSet)` | total, value text → count (Whole model elements), value text → stored integers or element ids (all candidates) |
| Resolved parameters | `ValueKey(SourceId, ParameterKey, GroupSet)` | `ResolvedParameter` list (id, storage, spec) from `ParameterLocator.Resolve` |
| Candidates | `(SourceId, GroupSet)` | `CandidateSet`: model element ids of the categories in every design option, the secondary-option subset, and their type ids on first use by a type parameter |

Every entry is stamped with the `DocumentVersion` it was built at; a lookup with
another version misses. All access is under one lock because the WPF side and
the Revit thread both use it.

### `GroupSet`

The category part of the value key. It sorts and de-duplicates the ids into a
`long[]` and computes the hash once in the constructor, so equality is a hash
check plus an array compare. Category sets on catalog items (`CategoryIdSet`)
use the same sorted-array idea. Measured against the earlier keys and sets:

| Benchmark | Before | After |
|-----------|--------|-------|
| Catalog merge, 200 runs | 449.8 ms, 754 MB | 246.3 ms, 151 MB |
| 1M dictionary lookups | 120.3 ms, 375 MB | 45.8 ms, 0 MB |
| Group key build, 1M | 338.7 ms, 1,251 MB | 47.9 ms, 114 MB |

### Invalidation

A version stamp alone is not enough: the Current model's version changes on
save or sync, not per transaction (gotcha 9). So `RevitModelEvents` also drops
entries from `DocumentChanged`:

| Change | Drops |
|--------|-------|
| A `ParameterElement` or `Family` added, modified or deleted | Catalog and values of that source; the panel reloads the picker (`ParameterDefinitionsChanged`) |
| More than 2,000 changed ids | Same as above |
| Any other change | Values, resolved parameters and candidates of that source |
| `RevitLinkType` modified (link reload or unload) | Catalog and values of every instance's link source |

The core keeps its own short-lived layers on top:

- `PanelRuntime` caches one catalog task per source and forgets it on
  `ParameterDefinitionsChanged` and when a source leaves the document list.
- `ValueIndexCache` keeps the summarized value lists (top 500 values,
  "more…", `[No parameter]` count) for at most 8 (source, parameter,
  categories) keys, least recently used dropped, and drops a source on any delta
  for it.

## Value index

`IFilterEvaluator.CountValuesAsync` walks the candidate elements of the checked
categories in slices and counts the display text of each value
(`ParameterValues.ValueText`: Yes/No as Yes/No, numbers and enums by their
display string, references by element name). Elements without the parameter,
or with an empty value, count toward `[No parameter]`. The same tally fills the enum map the compiler
uses, so a rule on an Enum parameter after opening its drop-down needs no extra
Revit read.
