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
| [test-matrix.md](test-matrix.md) | Use cases with golden counts on the Snowdon sample, L1 / L2 / L3 layers, oracle, readiness signal |

## Gotchas

Read these before changing the panel. Each one cost a live crash or a wrong
count.

1. **Never build the link view collector for a sheet or other non-model view.**
   `new FilteredElementCollector(model, viewId, linkInstanceId)` crashes Revit
   natively (access violation, no managed exception) on the first filter call in
   such a view. Always go through `LinkElements.InView`, which returns `null` for
   template views and for every view type except plans, sections, elevations,
   detail, 3D and walkthrough. The allowed types are only safe for the active
   view: a probe that built this collector for every plan, section, elevation and
   3D view of the HVAC model (most never opened in the session) crashed Revit
   2026 the same way. Call it for the active view only, never in a loop over
   views, and do not run view collectors on views of a link document either.
2. **Family parameters are invisible to the usual lookups.** A non-shared family
   parameter has a positive id and a `ParameterElement`, but no
   `ParameterBindings` entry, it is not in `GetFilterableParametersInCommon`, and
   `OfClass(typeof(ParameterElement))` does not return it. Find its ids on
   sampled elements. `ElementParameterFilter` still filters on it.
3. **One parameter key can map to several ids in one document.** The same family
   parameter gets a different id per family. A rule compiles to an OR over all
   ids.
4. **Native "not equals" passes elements that lack the parameter.** Every per-id
   rule is ANDed with *defined*.
5. **Has-value rules do not see type parameters from an instance.** Value rules
   do. *Defined*, *empty* and *not empty* of a type parameter are decided on
   the element types and applied to the candidates by type
   ([filters.md](filters.md#type-parameters)).
6. **Native string rules always ignore case.** Case-sensitive rules need a
   widened native pass plus an exact pass in memory.
7. **Do nothing inside `SelectionChanged`.** Revit forbids changing the
   selection or document there. The adapter only posts the ids to the WPF
   dispatcher; all work runs later.
8. **No Revit object crosses a thread or a port.** Ports carry `DocumentKey`,
   `SourceId`, `long` ids, strings and plain records. The adapter resolves a key
   to a `Document` on every call, and a closed or unloaded document resolves to
   nothing.
9. **A document version does not change per transaction.** `GetDocumentVersion`
   changes on save or sync only, so caches of the Current model must also be
   dropped from `DocumentChanged`, not just by version stamp.
10. **Several link instances share one link document.** Snapshots and parameter
   caches are keyed by the link source (its path), not by the instance.
11. **Implicit HandyControl styles do not apply in the Revit pane.** Give every
    label an explicit HC `TextBlock` style. Text inside list and combo rows has
    no style so it inherits the row foreground and stays readable when selected.
12. **No self `;component/` URIs in `DevTools.Presentation`.** The add-in
    ILRepacks it; only `/DevTools.UI;component/...` URIs survive. A guard test
    fails on any other.
13. **Never run T3 (`LinkedHideShow`) from a test body.** Hide in View and its
    Show go through the internal `invokeCommandHandler`, which runs a Revit UI
    command re-entrantly inside the in-host test runner. After L2 runs that did
    it, the test host answered "Host testing session is busy" to every later run,
    and Revit's fatal-error journal shows the "Model Context Show" transaction
    still being undone. L2 checks the T3 split of a plan only
    (`expected.plannedTiers` in `visibility.json`); `VisibilityRun` fails any
    plan with a `ReferenceCommandStep` before it runs, and the guard test
    `LinkTiersArePlanOnly` fails a T3 case without `plannedTiers`. L3 UI-06
    runs T3 from outside Revit through the panel's eye, the path a user takes.
14. **One copy of `DevTools.UI` per Revit.** WPF resolves
    `/DevTools.UI;component/...` to the most recently loaded assembly named
    `DevTools.UI`. When an L2 run loaded its own copy beside the add-in's, the
    next control the panel built for the first time (`AutoCompleteComboBox` at
    *+ Condition*) threw "does not have a resource identified by the URI"
    during layout, and Revit closed with a fatal error (0xe0434352; the Revit
    crash dumps of 2026-11-17 10:18 and 10:32 hold that `XamlParseException`).
    The host's `HostAssemblies` include `DevTools.UI`, and every test session
    binds to those loaded copies instead of the copies in its output. L2 then
    L3 in one Revit is the normal flow; L3 attaches to the Revit that ran L2 of
    the same run folder.
15. **Element overrides outlive Temporary View Properties.** The mode covers
    template-controlled settings (category overrides) but not
    `SetElementOverrides`: a T0 or T1 Color or Halftone stays in the view, and
    is saved with it, after the mode ends. `RevitViewActions` therefore records
    each element's overrides before its first change in a view and puts exactly
    those back on Reset (VI-03, VI-04, VI-08 compare every touched element with
    its state before the action).
16. **Keep UI Automation off Revit while it is busy.** Wait for Revit to be idle
    (the panel's `Idle <n>` signal) before any UIA call, and never poll the whole
    desktop tree: a helper that clicks *Always Load* searches only Revit's
    top-level dialogs (`#32770`) and stops once the model is open.
17. **Do not touch the pane's visual tree in `OnShutdown`.** By then Revit has
    torn down the dockable pane's render channel. `ModelsContextPane.Shutdown`
    once set the host `Content` to null; the template change released visuals
    on the dead channel and WPF ended Revit with `FailFast` ("Unrecoverable
    system error", `DUCE.Channel.ReleaseOnChannel`) on a normal close
    (2026-11-17 21:44). Shutdown only unsubscribes events and disposes the view
    model.
18. **Every panel command runs through `PanelRuntime.RunAsync`.** An exception
    that leaves an `AsyncRelayCommand` is rethrown on the WPF dispatcher, and in
    Revit that is a fatal error (0xe0434352). The temporary bar's Reset once
    called `VisibilityController.ResetAsync` directly; when Show for linked
    elements failed ("Revit did not show these linked elements", active view a
    sheet, L3 UI-06, 2026-11-17 21:51) Revit closed. `RunAsync` logs the error
    and shows it as the panel message.
19. **A view opened inside a run is not drawn.** The view collector answers for an
    undrawn view with far too many elements (L3 counted 1,669 Current model
    elements instead of 917; EX-06 was Inconclusive). Revit draws it only after the
    run returns, and `RefreshActiveView` does not change that. L2 therefore needs
    `L3` open from an earlier run: `scripts/test-models-context.ps1` runs
    `--filter OpensSampleView` first, then the full run. `SampleViews.Activate` stops as Inconclusive
    otherwise; no retry.
20. **Panel jobs always resume on the UI context.** `PanelRuntime.RunAsync` ran a
    job on whatever thread called it. A document opened inside a test run
    (`OpenAndActivateDocument` from `OpensSampleView`) raised pane visibility and view
    changes with no ambient `SynchronizationContext`, so the `await` continuations
    resumed on the pool and `ExplorerTabViewModel.Publish` changed `Rows` off the
    dispatcher thread. Log: "CollectionView does not support changes to its
    SourceCollection from a thread different from the Dispatcher thread". The next
    layout pass threw `InvalidOperationException: An ItemsControl is inconsistent with
    its items source` (dump `pe`, `VirtualizingStackPanel.MeasureChild`, control
    `RowList`), and Revit crash protection ended the session. The DirectContext3D
    `getBoundingBox` line in the journal is the add-in's visualizer and unrelated.
    `RunAsync` now posts to `ModelContextServices.Context` when
    `SynchronizationContext.Current` is null. Tests never open a document inside a run.

## Layers and ports

```text
DevTools.ModelsContext                  core: no Revit API, no WPF
  Model/       DocumentKey, SourceId, ElementRef, DocumentSnapshot, TreeRow,
               FilterDefinition, ParameterKey, ParameterInfo, ValueKind, ...
  Engine/      SnapshotStore, DeltaApplier, VersionRule, ScopeCounter,
               ValueIndexCache, CaseSensitiveRules, FilterScopes, ParameterScopes,
               FilterTreeMoves, VisibilityPlanner, SelectionGuard, TemporaryJournal,
               Debouncer
  Ports/       IModelSource, IModelEvents, ISelectionPort, IViewActions,
               IFilterEvaluator, IHostFilterCatalog, ICustomFilterStore,
               IClock, IScheduler
  ViewModels/  ModelContextViewModel, ContextBar, ExplorerTab, FilterTab
               (FilterLibrary, CategoryPicker, FilterRunner, RuleSet / Rule /
               UnsupportedRule view models), TemporaryBar, ContextMenu,
               VisibilityController
  Diagnostics/ ModelContextLog, TraceTimer, FailureTally

DevTools.Presentation/Views/ModelsContext   XAML views, DispatcherScheduler, SystemClock
DevTools.UI/Controls                        FlatTreeList, TagCheckComboBox, AutoCompleteComboBox
DevTools.UI/Behaviors                       DragReorder (drag and drop with insertion line), CheckedItems

RevitDevTool.Tools/ModelsContext            Revit adapter
  RevitModelSource, RevitModelEvents, RevitSelectionPort, RevitViewActions,
  LinkedHideShow, RevitFilterEvaluator, FilterCompiler, ParameterLocator, ParameterValues, ValueReader,
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

`FilterRunner.EvaluateAsync(sync)` is the one evaluation path:

- An edit (categories, rules, mode, source) triggers the Live debouncer, which
  evaluates with `sync: true` and sends the result to Revit.
- A model edit or context refresh reloads the tab and evaluates with
  `sync: false`: the count updates, the selection does not. This is why an edit
  in Revit's Properties palette never drops the element being edited.
- Live pauses above `FilterRunner.LivePauseLimit` (5,000) and sends nothing for an empty
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
  models with a fake clock and scheduler. Shared fakes live in
  `tests/DevTools.ModelsContext.Tests.Shared`: `FakeHost` wires them.
  `FakeModel` and `FakeViewActions` are hand-written because their members
  share state (snapshots, versions, gates, events, executed plans). Host
  filters and the custom filter store are Moq mocks. Model data comes from a
  seeded Bogus `Randomizer`, so a seed always gives the same model.
- **View tests** (`tests/DevTools.ModelsContext.Views.Tests`) host the real XAML
  on the fake model and drive it through UI Automation by AutomationId (FlaUI,
  UIA patterns, no mouse events). `XamlGuardTests` blocks self `;component/`
  URIs.
- **Sandbox** (`tests/DevTools.ModelsContext.Sandbox`) opens the panel on a
  generated or replayed model without Revit. `--measure` prints timings,
  `--shots <folder>` saves screenshots, every run opens in the dark theme (`--theme light` for the light one), and
  `--trace` prints the panel Trace lines.
- **Live tests** run on the Snowdon sample in Revit: adapter tests in
  `tests/RevitDevTool.ModelsContext.LiveTests` (inside Revit, golden counts from an
  API oracle) and a few panel flows in `tests/RevitDevTool.ModelsContext.UiTests`
  (FlaUI from outside Revit, waiting on the `ModelContext.Panel` status). Rows,
  golden values and commands: [test-matrix.md](test-matrix.md).

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
