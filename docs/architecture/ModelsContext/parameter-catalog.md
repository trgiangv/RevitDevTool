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
3. **Build.** Drop built-ins that were only sampled and are not filterable. Turn
   each id into a `ParameterInfo` (key, name, kind, storage, spec label, origin,
   binding, categories) and merge infos with the same key. Origin is Shared for
   a `SharedParameterElement`, Project when it has a `ParameterBindings` entry,
   else Family. The key → ids map is kept for the compiler.

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

## `ParameterCache` (adapter)

One instance per pane, shared by the catalog reader, the filter evaluator and
the event hub. It holds:

| Entry | Key | Value |
|-------|-----|-------|
| Catalog | `SourceId` | `ParameterInfo` list + key → ids |
| Values | `ValueKey(SourceId, ParameterKey, GroupSet)` | total, value text → count, value text → integer (enum map) |

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
save or sync, not per transaction (gotcha 8). So `RevitModelEvents` also drops
entries from `DocumentChanged`:

| Change | Drops |
|--------|-------|
| A `ParameterElement` or `Family` added, modified or deleted | Catalog and values of that source; the panel reloads the picker (`ParameterDefinitionsChanged`) |
| More than 2,000 changed ids | Same as above |
| Any other change | Values of that source only |
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
(`ParameterResolver.ValueText`: Yes/No as Yes/No, numbers and enums by their
display string, references by element name). Elements without the parameter,
or with an empty value, count toward `[No parameter]`. The same tally fills the enum map the compiler
uses, so a rule on an Enum parameter after opening its drop-down needs no extra
Revit read.
