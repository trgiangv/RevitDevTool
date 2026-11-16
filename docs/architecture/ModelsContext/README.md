# Model Context Architecture

The Model Context panel is three layers: a host-free core, WPF views, and a Revit
adapter that implements the core's ports. The core and views build, run and test
without Revit; Revit plugs in through the ports.

Behavior: [product/models-context.md](../../product/models-context.md).
Policy and spike results: [0062](../../decisions/0062-model-context-panel.md).

Topic pages:

| Page | Covers |
|------|--------|
| [snapshots.md](snapshots.md) | Document keys, snapshot store, versions, deltas, links, scopes, the link view collector |
| [filters.md](filters.md) | Filter model, parameter identity, family parameters, value kinds, compiling to Revit filters, case-sensitive rules |
| [parameter-catalog.md](parameter-catalog.md) | Catalog per document, parameter and value caches, invalidation, measurements |
| [ui.md](ui.md) | HandyControl conventions, `AutoCompleteComboBox`, validation, AutomationIds |

## Gotchas

Read these before changing the panel. Each one cost a live crash or a wrong
count.

1. **Never build the link view collector for a sheet or other non-model view.**
   `new FilteredElementCollector(model, viewId, linkInstanceId)` crashes Revit
   natively (access violation, no managed exception) on the first filter call in
   such a view. Always go through `LinkElements.InView`, which returns `null` for
   template views and for every view type except plans, sections, elevations,
   detail, 3D and walkthrough.
2. **Family parameters are invisible to the usual lookups.** A non-shared family
   parameter has a positive id and a `ParameterElement`, but no
   `ParameterBindings` entry, it is not in `GetFilterableParametersInCommon`, and
   `OfClass(typeof(ParameterElement))` does not return it. Find its ids on
   sampled elements. `ElementParameterFilter` still filters on it.
3. **One parameter key can map to several ids in one document.** The same family
   parameter gets a different id per family. A rule compiles to an OR over all
   ids.
4. **Native "not equals" passes elements that lack the parameter.** Every per-id
   rule is ANDed with "parameter exists" (has value OR has no value).
5. **Native string rules always ignore case.** Case-sensitive rules need a
   widened native pass plus an exact pass in memory.
6. **Do nothing inside `SelectionChanged`.** Revit forbids changing the
   selection or document there. The adapter only posts the ids to the WPF
   dispatcher; all work runs later.
7. **No Revit object crosses a thread or a port.** Ports carry `DocumentKey`,
   `SourceId`, `long` ids, strings and plain records. The adapter resolves a key
   to a `Document` on every call, and a closed or unloaded document resolves to
   nothing.
8. **A document version does not change per transaction.** `GetDocumentVersion`
   changes on save or sync only, so caches of the Current model must also be
   dropped from `DocumentChanged`, not just by version stamp.
9. **Several link instances share one link document.** Snapshots and parameter
   caches are keyed by the link source (its path), not by the instance.
10. **Implicit HandyControl styles do not apply in the Revit pane.** Give every
    label an explicit HC `TextBlock` style. Text inside list and combo rows has
    no style so it inherits the row foreground and stays readable when selected.
11. **No self `;component/` URIs in `DevTools.Presentation`.** The add-in
    ILRepacks it; only `/DevTools.UI;component/...` URIs survive. A guard test
    fails on any other.

## Layers and ports

```text
DevTools.ModelsContext                  core: no Revit API, no WPF
  Model/       DocumentKey, SourceId, ElementRef, DocumentSnapshot, TreeRow,
               FilterDefinition, ParameterKey, ParameterInfo, ValueKind, ...
  Engine/      SnapshotStore, DeltaApplier, VersionRule, ScopeCounter,
               ValueIndexCache, CaseSensitiveRules, VisibilityPlanner,
               SelectionGuard, TemporaryJournal, Debouncer
  Ports/       IModelSource, IModelEvents, ISelectionPort, IViewActions,
               IFilterEvaluator, IHostFilterCatalog, ICustomFilterStore,
               IClock, IScheduler
  ViewModels/  ModelContextViewModel, ContextBar, ExplorerTab, FilterTab,
               RuleViewModels, TemporaryBar, ContextMenu, VisibilityController
  Diagnostics/ ModelContextLog, TraceTimer, FailureTally

DevTools.Presentation/Views/ModelsContext   XAML views, DispatcherScheduler, SystemClock
DevTools.UI/Controls                        FlatTreeList, TagCheckComboBox, AutoCompleteComboBox

RevitDevTool.Tools/ModelsContext            Revit adapter
  RevitModelSource, RevitModelEvents, RevitSelectionPort, RevitViewActions,
  LinkedHideShow, RevitFilterEvaluator, FilterCompiler, ParameterResolver,
  ParameterCatalogReader, ParameterCache, RevitFilterCatalog, CustomFilterStore,
  DocumentRegistry, ElementReader, LinkElements, ScopeCache, RevitProfiler,
  ModelsContextPane
```

The core decides and the adapter executes. Examples:

- `VisibilityPlanner` turns a row action plus each document's capabilities into
  a `VisibilityPlan` (which tier, which ids, or a disabled reason).
  `RevitViewActions` and `LinkedHideShow` run it.
- `FilterDefinition` is host-neutral. `FilterCompiler` turns it into Revit
  `ElementFilter`s per document. The test fakes evaluate it over fake data.
- `CaseSensitiveRules` (core) rewrites the rule tree; the adapter compiles the
  result.

The pane (`ModelsContextPane`) wires it: it builds one `ModelContextServices`
with the Revit port implementations, `SystemClock`, a `DispatcherScheduler` on
the pane's dispatcher, and the host `ILogger`.

## Threading and dispatch

| Where | What runs there |
|-------|-----------------|
| Revit API thread | Every Revit call, through `RevitProfiler.RunAsync` → `RevitContextExecutor.RaiseAsync` with a cancellation token. |
| WPF dispatcher | View models, port events, debouncers. |
| Thread pool | Snapshot building and aggregation over plain records. |

- **Slices.** Long reads (snapshot, counts, catalog, filter match, value counts)
  run as many short Revit calls of at most 5,000 elements or about 30 ms each
  (`ElementReader.SliceSize`, `SliceTime`). The WPF side awaits one slice, then
  asks for the next, so Revit stays responsive and a cancel lands between
  slices.
- **Events.** `RevitModelEvents` subscribes once to the Revit events and does
  only cheap work in the handler: it reads ids, builds a plain `ElementDelta`,
  and posts the panel event to the WPF dispatcher at `Background` priority.
  `SelectionChanged` is ignored while the pane is hidden and while
  `LinkedHideShow` runs (that call changes the selection on purpose).
- **Sessions and generations.** Each open Current model has a `PanelSession`.
  Every async job carries a ticket (session plus generation); a result whose
  session closed, whose generation moved on, or that arrives while the pane is
  hidden is dropped instead of applied.
- **Hidden pane.** Changes only mark snapshots dirty and drop caches; on show
  the panel resyncs once.

### Debouncers

Debounce lives in the core (`Debouncer` on `IScheduler`); the views implement
`IScheduler` with a `DispatcherTimer`. Tests use a fake clock and scheduler.

| Delay | What |
|-------|------|
| 150 ms | Explorer search; `AutoCompleteComboBox` typing |
| 200 ms | Panel events: Revit selection, element deltas, document list, view activation |
| 200 ms | Explorer Selection sync to Revit |
| 300 ms | Filter Live push to Revit |

`SelectionGuard` remembers each selection the panel sent for 2 s and drops the
matching `SelectionChanged` echo, so the panel never reacts to its own change.

### Live versus refresh

`FilterTabViewModel.EvaluateAsync(sync)` is the one evaluation path:

- An edit (categories, rules, mode, source) triggers the Live debouncer, which
  evaluates with `sync: true` and sends the result to Revit.
- A model edit or context refresh reloads the tab and evaluates with
  `sync: false`: the count updates, the selection does not. This is why an edit
  in Revit's Properties palette never drops the element being edited.
- Live pauses above `LivePauseLimit` (5,000) and sends nothing for an empty
  custom filter.

## Logging

- The pane receives `ILogger<ModelsContextPane>` from host DI. It hands it to the
  core through `ModelContextServices` and to static adapter helpers through
  `RevitProfiler.Use`.
- The core logs through source-generated `ModelContextLog` messages. A failure
  shown to the user is Error; timings and swallowed errors are Trace.
- `RevitProfiler.RunAsync` measures queue time (waiting for the Revit thread)
  and run time per call. When Trace is off it calls `RaiseAsync` directly, so
  the default level pays nothing. `ProfileStats` sums sliced work into one line
  and logs a single slice only when it runs 250 ms or more.
- `FailureTally` counts errors skipped inside a loop and logs one line with the
  first exception.

How to read the lines: [verification.md#model-context-diagnostics](../../agents/verification.md#model-context-diagnostics).

## Visibility tiers

Visibility is planned in the core and run by the adapter in four tiers (0062 §9):
T0 Current model elements by id, T1 a whole link instance, T2 a category
everywhere, T3 elements inside one link through Hide in View. T3 calls an
internal Revit command handler; the adapter probes for it once and, when it is
missing, link rows below the model row get no visibility actions. Details and
the measured spike: 0062 §5, §7–§10.

## Testing

- **Core tests** (`tests/DevTools.ModelsContext.Tests`) run the engine and view
  models on a fake model that implements every port, with a fake clock and
  scheduler. Shared fakes live in `tests/DevTools.ModelsContext.Tests.Shared`.
- **View tests** (`tests/DevTools.ModelsContext.Views.Tests`) host the real XAML
  on the fake model and drive it through UI Automation by AutomationId (FlaUI,
  UIA patterns, no mouse events). `XamlGuardTests` blocks self `;component/`
  URIs.
- **Sandbox** (`tests/DevTools.ModelsContext.Sandbox`) opens the panel on a
  generated or replayed model without Revit. `--measure` prints timings,
  `--shots <folder>` saves screenshots, `--dark` switches to the dark theme, and
  `--trace` prints the panel Trace lines.
- **Live checks** drive the panel inside Revit with FlaUI through the same
  AutomationIds and compare counts with direct API queries. Results are recorded
  in the plan and in [filters.md](filters.md#live-checks).

## Change rules

- Keep Revit types out of `DevTools.ModelsContext` and out of every port.
- New Revit reads go through `RevitProfiler.RunAsync`, sliced when they walk
  elements.
- A new link-view read goes through `LinkElements.InView`.
- New caches key on `SourceId` (not the instance) and stamp with
  `DocumentVersion`, and get an invalidation path from `RevitModelEvents`.
- New controls get an AutomationId and HandyControl stock styles.

## Related

- [0062 Model Context panel](../../decisions/0062-model-context-panel.md)
- [Host boundaries](../../agents/host-boundaries.md)
- [Execution plan](../../plans/active/2026-11-13-models-context-panel.md)
