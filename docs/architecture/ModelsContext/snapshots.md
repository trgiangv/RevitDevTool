# Snapshots, Documents and Links

How the panel knows what is in each model without holding Revit objects.

## Keys

| Key | Means | Used for |
|-----|-------|----------|
| `DocumentKey(ModelId, LinkInstanceId)` | One entry in the documents box: the Current model (`LinkInstanceId = -1`) or one link instance inside it. | Ticks, tree groups, selection, view actions, the T3 journal. |
| `SourceId` | One document file: `model:<path>` for a Current model, `link:<path, lower case>` for a link document. | Snapshots, parameter catalogs, value caches. |
| `ElementRef(DocumentKey, long)` | One element in one entry. | Selection and results. |

`ModelId` is the model's path (or `unsaved:<title>`). `DocumentRegistry`
resolves a key to `Document` / `RevitLinkInstance` on every call and returns
nothing for a closed, unloaded or replaced document, so a stale key can never
reach a dead object.

**Why two keys.** Several instances of one link file share one link `Document`.
What is *in* the file is the same for all of them, so snapshots and caches are
per `SourceId` and shared. What the user *sees* differs per instance (transform,
visibility, selection references), so those stay per `DocumentKey`.

## Snapshot

A `DocumentSnapshot` is plain data for one source: arrays of structs for
elements (id, type index, level, workset), a type table (type, family,
category), a name table that stores each string once, and an id index. It holds
no Revit object, so a closed model or reloaded link is never pinned. Parameter
values are not in the snapshot.

It is built in slices: one call collects the ids (model categories, no element
types, no secondary design options) and the document version, then
`ElementReader.ReadSlice` reads 5,000 elements or 30 ms per Revit call, and the
builder aggregates on the thread pool.

While snapshots build, the Explorer shows first rows from a quick count per
category (`CountGroupsAsync`) so the panel never waits on a full read.

## Snapshot store

`SnapshotStore` (core) keeps one snapshot task per `SourceId`.

- `SetDocuments` replaces the document list of one model and drops snapshots no
  entry uses any more. A link source shared by two open models stays until the
  last one closes (`UsersOf`).
- `GetAsync` reuses a running or finished build, and starts a new one when the
  last build failed, was canceled, or is dirty.
- A dirty snapshot is kept for display but rebuilt on next use.
- An unloaded link keeps its snapshot (rows disabled) until a reload decides by
  version.

## Deltas from `DocumentChanged`

`RevitModelEvents.OnDocumentChanged` handles the Current model only and ignores
events raised for link documents. A link changes only when it is reloaded, which
shows up in the Current model as a modified `RevitLinkType`.

1. It reads the added and modified elements into records and builds an
   `ElementDelta` (types, added, modified, deleted, undo/redo flag).
2. More than `DeltaApplier.DirtyThreshold` (2,000) changed ids: it skips the
   reads and sends a delta that only marks the snapshot dirty. Reading thousands
   of elements inside the event would stall Revit; a rebuild in slices later is
   cheaper.
3. The panel applies queued deltas after a 200 ms debounce.

`DeltaApplier` removes deleted ids, adds or replaces the rest, and marks the
snapshot dirty instead of patching when a type moved to another category or
family, or an element cannot be placed. Undo and redo use the same path and
also mark the T3 journal stale.

The same handler also tells caches what changed (see
[parameter-catalog.md](parameter-catalog.md#invalidation)), clears the active
view id cache, and reports added or deleted link instances and link reloads.

## Versions

`DocumentVersion` is `Document.GetDocumentVersion`: `VersionGUID` plus number of
saves. Only the GUID is compared. It changes when the file is saved or synced,
not per transaction.

- **Link reload** (`RevitLinkType` modified): `SnapshotStore.ReloadAsync`
  - same GUID: keep the snapshot, no collection;
  - other GUID: `GetChangedElements(oldGuid)` gives created, modified and
    deleted ids, applied through the delta path;
  - unknown old version, deleted ids not available, or more than 20% of the
    snapshot changed (`VersionRule.RebuildShare`): rebuild.
- **Current model after sync or Reload Latest**: `VerifyAsync` asks for the
  changes since the stamp and checks the snapshot already reflects them. On a
  mismatch it marks dirty and the panel refreshes.

## Scopes

- **Whole model** reads the snapshot directly.
- **Active view** and **Selection** collect an id set per document and recount it
  against the snapshot rows in managed code.
- Active view id sets are cached in `ScopeCache` per (document, view) and
  cleared on any `DocumentChanged`, view activation or model close.

### Linked elements in a view

`new FilteredElementCollector(hostDocument, viewId, linkInstanceId)` returns the
linked elements visible for one link instance in a host view. It drops elements
hidden with Hide in View and costs a few ms per link once the view is drawn.

On Revit 2026 it **crashes Revit natively** (access violation, nothing to catch)
at the first filter call when the view is a sheet or another view that does not
draw model links. So every use goes through `LinkElements.InView`, which builds
the collector only for non-template views of type FloorPlan, CeilingPlan,
EngineeringPlan, AreaPlan, Section, Elevation, Detail, ThreeD and Walkthrough,
and returns `null` otherwise. Callers treat `null` as "no linked elements in
this view". Keep the list an allow list: a new view type stays excluded until it
is tried on a live host.

## Events

| Revit event | Panel effect |
|-------------|--------------|
| `DocumentChanged` | Delta, cache invalidation, link status, document list. |
| `SelectionChanged` | Posted ids only; Explorer sync and Selection scope run after 200 ms. |
| `ViewActivated` | View id cache cleared; a new Current model switches session. |
| `DocumentClosing` | Session, journal, snapshots (by user count) dropped. |
| `DocumentOpened`, `DocumentCreated`, `DocumentClosed` | Document list reload; busy state. |
| `DocumentSynchronizedWithCentral`, `DocumentReloadedLatest` | Snapshot verify. |

Nothing subscribes to `Idling`.
