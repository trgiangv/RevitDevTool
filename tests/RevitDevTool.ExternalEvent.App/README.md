# Revit External Event Benchmark App

A fair, scenario-based benchmark that compares Revit external event dispatch libraries under controlled conditions. It does **not** produce a single ranking — each library has distinct design trade-offs.

> **Important context:** This benchmark is primarily for fun and educational purposes. In real-world Revit add-in development, **dispatch overhead is negligible compared to Revit API execution time**. The TransactionRollback workload proves this clearly — all dispatcher libraries converge to similar throughput because the Revit transaction dominates the total latency. The differences in raw NoOp dispatch speed only matter in edge cases like chatty IPC bridges or real-time dashboards with hundreds of rapid-fire API reads. For most add-in workflows (commands, modeless dialogs, event handlers), any of these libraries will perform identically in practice.

## Libraries Under Test

| Library | NuGet / Source | Dispatch Model | Suite |
|---------|---------------|----------------|-------|
| **RevitDevTool.Core** | This repo | Central FIFO dispatcher with ExternalEvent batch drain | 1 — Central Dispatcher |
| **ricaun.Revit.UI.Tasks** | [NuGet](https://www.nuget.org/packages/ricaun.Revit.UI.Tasks) | Idling/event-creation service | 1 — Central Dispatcher |
| **Revit.Async** | [NuGet](https://www.nuget.org/packages/Revit.Async) | FutureExternalEvent per RunAsync call | 1 — Central Dispatcher |
| **RevitToolkit** | [NuGet](https://www.nuget.org/packages/Nice3point.Revit.Toolkit) | AsyncExternalEvent fixed-handler reuse | 2 — In-Context Event Reuse |
| **Native ExternalEvent** | Revit API built-in | Raw `ExternalEvent` + `IExternalEventHandler` | 2 — In-Context Event Reuse |

## Why Two Suites?

The libraries use fundamentally different dispatch models:

- **Suite 1 (Central Dispatcher)** — adapters accept **arbitrary delegates** (`Func<UIApplication, T>`) and queue them for execution on the Revit thread. Measures queue throughput, latency breakdown, cancellation, error propagation, and ordering.

- **Suite 2 (In-Context Event Reuse)** — adapters have a **fixed handler baked at construction time**. Only measures raise/dispatch overhead, not per-call delegate injection. Comparing these directly to Suite 1 throughput numbers would be misleading.

## Adapter Details

### RevitDevTool.Core

```
Interface:  IDispatchAdapter
Mechanism:  RevitContextExecutor.RaiseAsync(func, token)
Queue:      Central queue, drained in batch when Revit invokes the dispatcher handler
```

**Strengths:**
- Highest throughput across Suite 1 dispatcher scenarios. Synthetic NoOp burst completes near-instantly due to batch drain — this measures queue efficiency, not Revit API throughput
- Lowest sequential dispatch latency among Suite 1 adapters
- Only Suite 1 adapter supporting `DirectInvocation` (inline execution when already on Revit thread — uses `RevitContext.IsRevitInApiMode`, a hacky API-context detection mechanism inspired by RevitToolkit)
- Full cancellation token support (pre-cancel, after-enqueue, during-callback)
- Batch drain architecture: processes entire pending queue per ExternalEvent handler invocation
- Simple one-liner API like Revit.Async: `RevitContextExecutor.RaiseAsync(app => ...)` — no setup, no DI, static access
- Also serves as the execution bridge for Python scripts (pytest, IronPython) and MCP tools — any language that can call .NET gets Revit API access through the same dispatcher

**Weaknesses:**
- Heavier per-request allocation than Revit.Async (central queue + TaskCompletionSource per request vs per-event model)
- Requires RevitDevTool.Core dependency

**Best for:** IPC bridges (MCP, WebView, named pipes), Python/IronPython script execution, dashboards, chatty API patterns, any scenario requiring high-throughput arbitrary delegate dispatch with cancellation support.

---

### ricaun.Revit.UI.Tasks

```
Interface:  IDispatchAdapter
Mechanism:  IRevitTask.Run(func, token)
Queue:      Per-request event creation via Idling subscription
```

**Strengths:**
- Good cancellation support (pre-cancel and after-enqueue)
- Clean API: `revitTask.Run(func, token)`
- Solid throughput for moderate sequential and sustained workloads
- Full error propagation (PASS)
- FIFO ordering (PASS)

**Weaknesses:**
- Degrades under burst: timeouts at high concurrent request counts due to Idling event scheduling model
- No DirectInvocation support
- Higher enqueue overhead than DevTool.Core

**Best for:** Projects needing simple dispatch with cancellation support but without extreme throughput requirements.

---

### Revit.Async

```
Interface:  IDispatchAdapter
Mechanism:  RevitTask.RunAsync(func) — creates a new FutureExternalEvent per call
Queue:      One ExternalEvent per request (no central queue)
```

**Strengths:**
- Lightweight per-request allocation (one ExternalEvent per call, no central queue overhead)
- Simplest API: one-liner `RevitTask.RunAsync(func)`
- Clean error propagation (PASS)
- FIFO ordering (PASS)
- Full nested reentry support

**Weaknesses:**
- No cancellation token support (SKIPPED)
- No DirectInvocation support (SKIPPED)
- Highest sequential latency among Suite 1 adapters (per-request ExternalEvent creation overhead)
- Lower sustained throughput than DevTool.Core and ricaun due to one-event-per-call model
- **Context loss with async delegate overload** — `RevitTask.RunAsync<T>(Func<UIApplication, Task<T>>)` allows `await` inside the callback, but after any `await` the continuation may resume on a thread pool thread that is **outside the supported ExternalEvent execution window**. Calling Revit API (transactions, element queries) after the `await` may throw, fail unpredictably, or execute without a valid Revit API context. See `ContextLostRepro.Unsafe_AsyncDelegateOverload_ContextLoss()` in this project for a reproduction. The safe pattern requires splitting into two separate `RunAsync` calls with async I/O in between.

**Best for:** Simple fire-and-forget scenarios with **synchronous callbacks only**, projects prioritizing minimal dependencies and low memory footprint.

### API Context Safety Comparison

Revit.Async is the **only library** that exposes async delegate overloads (`Func<UIApplication, Task<T>>`), which can lose the Revit API context after any `await` inside the callback. All other libraries avoid this pitfall by accepting synchronous delegates only:

| Library | Handler Signature | Async Delegate Overload | Async Context Loss Risk |
|---------|------------------|------------------------|------------------------|
| **RevitDevTool.Core** | `Action<UIApplication>`, `Func<UIApplication, T>` | No | No async-delegate context-loss risk — sync-only by design |
| **ricaun.Revit.UI.Tasks** | `Func<UIApplication, object>` | No | No async-delegate context-loss risk — sync-only by design |
| **RevitToolkit** | `Action<UIApplication>` (fixed handler) | No | No async-delegate context-loss risk — handler is sync, `RaiseAsync()` is only the bridge |
| **Native ExternalEvent** | `IExternalEventHandler.Execute(UIApplication)` | No | No async-delegate context-loss risk — Revit API contract is sync |
| **Revit.Async** | `Func<UIApplication, T>` + `Func<UIApplication, Task<T>>` | **Yes** | **Risk** — `await` inside callback may lose API context |

See `ContextLostRepro.cs` in this project for a reproduction of the Revit.Async context loss issue and the safe workaround pattern (split into separate `RunAsync` calls).

---

### RevitToolkit (AsyncExternalEvent)

```
Interface:  IInContextEventAdapter
Mechanism:  AsyncExternalEvent created once with fixed handler, reused via RaiseAsync()
Concurrency: SemaphoreSlim(1,1) serializes concurrent raises
Option:     ExternalEventOptions.AllowDirectInvocation
```

**Strengths:**
- Concurrent raise safety: all requests succeed (vs Native's near-total failure under concurrent raise)
- Solves the core Revit API limitation: `ExternalEvent.Raise()` returns `Denied` when called concurrently
- DirectInvocation support: executes inline when already on Revit thread
- Intended-usage design: create once, raise many times — matches the `IExternalEventHandler` pattern

**Weaknesses:**
- Fixed handler only — cannot inject arbitrary delegates per call
- Not in Suite 1 — cannot be directly compared on arbitrary dispatch throughput
- Slightly higher sequential latency than Native due to SemaphoreSlim serialization overhead

**Best for:** UI event triggers (button clicks, model sync, view refresh), any pattern where a fixed handler is raised from multiple threads concurrently. The only safe concurrent alternative to Native ExternalEvent without building a central dispatcher.

---

### Native ExternalEvent (Baseline)

```
Interface:  IInContextEventAdapter
Mechanism:  Autodesk.Revit.UI.ExternalEvent.Create(handler) + Raise()
Bridging:   TaskCompletionSource for async await
```

**Strengths:**
- Lowest raise overhead (no wrapper/semaphore cost)
- Zero external dependencies — pure Revit API
- Establishes the performance baseline for all other libraries

**Weaknesses:**
- **Critical: concurrent raise fails catastrophically** — nearly all requests faulted with `ExternalEventRequest.Denied`
- No DirectInvocation support
- No cancellation support
- Requires manual `TaskCompletionSource` bridging for async/await

**Best for:** Understanding raw Revit API behavior. Not recommended for production use without a wrapper — use RevitToolkit or a central dispatcher instead.

## Scenarios

### Suite 1 — Central Dispatcher

| Scenario | What It Measures | Parameters |
|----------|-----------------|------------|
| **SequentialLatency** | Round-trip dispatch latency with full timing breakdown (enqueue → wait → callback → completion) | 1000 req (NoOp), 200 req (LightRevitRead), 50 req (TransactionRollback) |
| **ProducerSequential** | Multi-producer throughput where each producer awaits one request before submitting the next | 1000 req, 4 producers |
| **TrueBurst** | All requests enqueued simultaneously from N threads, then all awaited | 1000 req, 8 threads |
| **SustainedLoad** | Continuous throughput with bounded in-flight requests over a fixed duration | 5 seconds, 4 producers, max 50 in-flight |
| **DirectInvocation** | Inline execution when already on the Revit API thread (no queue round-trip) | 100 req |
| **NestedReentry** | Sequential re-entrant dispatch: `await RunAsync()` then dispatch again from callback | depth=50 |
| **CancellationLifecycle** | Three phases: pre-cancelled token, cancel after enqueue, cancel during callback | 100 req per phase |
| **ErrorPropagation** | Whether exceptions thrown in callbacks propagate correctly to the caller | 50 req |
| **FIFO Order** | Whether requests execute in the order they were enqueued | 200 req |

### Suite 2 — In-Context Event Reuse

| Scenario | What It Measures | Parameters |
|----------|-----------------|------------|
| **SequentialRaise** | Sequential raise → handler execution latency (enqueue + total only, no callback timing) | 1000 req |
| **DirectInvocation** | Inline execution when raised from the Revit API thread | 100 req |
| **ConcurrentRaise** | Fire N raises from background threads simultaneously — tests concurrent raise safety. Includes scenario-level timeout matching Suite 1 TrueBurst | 1000 req, 8 threads |

### Workload Profiles (Suite 1 SequentialLatency)

| Profile | What It Does |
|---------|-------------|
| **NoOp** | Empty callback — measures pure dispatch overhead |
| **LightRevitRead** | `app.Application.VersionNumber` — minimal Revit API read |
| **TransactionRollback** | Query wall → read parameters → start transaction → write → rollback. Real Revit DB round-trip validating execution inside a usable Revit API context |

## Fairness Mechanisms

- **Randomized adapter order** — execution order is shuffled each run to prevent ordering bias
- **Warmup** — 5 warmup requests per adapter before measurement begins
- **Cooldown** — 200ms pause between scenarios to let Revit return to an idle scheduling state
- **Equal work distribution** — burst/concurrent tests distribute requests evenly across threads
- **Per-request timeout** — 10-second timeout prevents individual hangs from blocking the entire benchmark
- **Scenario-level timeout** — 120-second wall-clock timeout prevents stuck scenarios
- **Separate suites** — fixed-handler adapters are never mixed with arbitrary-delegate throughput tests

## Metrics

### Timing Breakdown (Suite 1)

| Phase | T0 → T1 | T1 → T2 | T2 → T3 | T3 → T4 | T0 → T4 |
|-------|---------|---------|---------|---------|---------|
| **Label** | Enqueue | Wait | Callback | Completion | Total |
| **Meaning** | Time to submit the request | Queue wait until Revit thread picks it up | Actual callback execution time | Signaling completion back to caller | End-to-end latency |

### Timing (Suite 2)

Only Enqueue (T0 → T1) and Total (T0 → T4) are reported — callback timing is not available for fixed handlers since the handler is baked into the event.

### Statistics

Each timing phase reports: `mean`, `p50`, `p95`, `p99`, `max` (in μs).

### Capability Verdicts

| Verdict | Meaning |
|---------|---------|
| **PASS** | Full support — all requests handled correctly |
| **PARTIAL** | Mostly works with documented limitations (e.g., during-callback cancellation is inherently difficult) |
| **SKIPPED** | Adapter does not support this capability — reported as design difference, not failure |

## Quick Reference: Which Library Wins Where?

> **Reality check:** These throughput differences rarely matter in production. With real Revit API workloads (transactions, element queries, parameter writes), all libraries bottleneck on the Revit API — not the dispatcher. Pick a library based on **API design, capability support, and your architecture** rather than raw dispatch speed.

| Use Case | Best Choice | Why |
|----------|-------------|-----|
| IPC / MCP / WebView bridge | **RevitDevTool.Core** | Highest sustained throughput, batch drain, DirectInvocation, cancellation |
| Python / IronPython script execution | **RevitDevTool.Core** | Same dispatcher serves pytest bridge, MCP tools, and script runners |
| Concurrent event raise from multiple threads | **RevitToolkit** | Full concurrent safety vs Native's near-total failure |
| Fixed handler (sync model / refresh view) | **RevitToolkit** | Designed for this pattern, concurrent-safe |
| Simple fire-and-forget (C# only) | **Revit.Async** | One-line API, minimal dependencies |
| Dispatch with cancellation (moderate load) | **ricaun.Revit.UI.Tasks** | Strong cancel support, clean API |
| Inline execution on Revit thread | **RevitDevTool.Core** or **RevitToolkit** | Both support DirectInvocation |
| Heavy transaction workload | **Any** | Revit API dominates — all libraries converge to similar throughput |

### Design lineage

RevitDevTool.Core combines ideas from multiple libraries:
- **From Revit.Async:** Simple static API — `RevitContextExecutor.RaiseAsync(app => ...)`, no DI or setup required
- **From RevitToolkit:** The hacky `RevitContext.IsRevitInApiMode` mechanism (detecting whether the caller is already inside a valid Revit API context) that enables DirectInvocation — executes inline when already on the Revit thread, skipping the queue entirely
- **Original:** Batch drain architecture, cancellation token propagation, and cross-language bridge (Python, MCP, IPC)

## Benchmark Scope

Results depend on Revit version, model size and complexity, machine hardware, other loaded add-ins, UI idle state, and ExternalEvent scheduling behavior. Observations in this README describe **relative behavior from this specific benchmark setup**, not universal performance guarantees. Run the benchmark yourself to see actual numbers in your environment.

## Running

The benchmark runs as a Revit `IExternalCommand`. Load the add-in, click the External Event Benchmark button. The WPF window provides:

- **Run All** — executes both suites sequentially with randomized adapter order
- Individual scenario buttons for targeted testing
- Results displayed in a scrollable textbox (copy/paste the output)
- Cancel button to abort mid-run

## Architecture

```
ExternalEventCommand.cs          → IExternalCommand entry point, creates adapters
StressTestWindow.cs              → WPF UI, wires buttons to runner
StressTestRunner.cs              → Orchestrator, runs suites and scenarios
BenchmarkReport.cs               → Generates Markdown summary tables

Scenarios/
  LatencyScenarios.cs            → SequentialLatency (3 workload profiles)
  LoadScenarios.cs               → ProducerSequential, TrueBurst, SustainedLoad
  DispatcherCapabilityScenarios.cs → DirectInvocation, NestedReentry, Cancellation, Error, FIFO
  InContextEventScenarios.cs     → SequentialRaise, DirectInvocation, ConcurrentRaise

Adapters/
  RevitDevToolAdapter.cs         → IDispatchAdapter → RevitContextExecutor
  RicaunTaskAdapter.cs           → IDispatchAdapter → IRevitTask
  RevitAsyncAdapter.cs           → IDispatchAdapter → RevitTask
  RevitToolkitAdapter.cs         → IInContextEventAdapter → AsyncExternalEvent
  NativeExternalEventAdapter.cs  → IInContextEventAdapter → ExternalEvent

Interfaces:
  IDispatchAdapter.cs            → RunAsync(Func/Action) + cancellation + DirectInvocation
  IInContextEventAdapter.cs      → RaiseAndWaitAsync() — fixed handler, no per-call delegate

Models:
  BenchmarkModels.cs             → BenchmarkResult, BenchmarkSuite, BenchmarkCategory, WorkloadProfile
  RequestTiming.cs               → T0-T4 timestamp struct for latency breakdown
  PercentileStats.cs             → mean/p50/p95/p99/max computation
  BenchmarkCounters.cs           → Thread-safe completed/faulted/cancelled/timedOut counters
  BenchmarkHelpers.cs            → Warmup, cooldown, shuffle, work distribution utilities
```
