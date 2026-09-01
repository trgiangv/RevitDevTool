# 0030 Host-Owned CPython and Package Managers

Date: 2026-09-02

## Status

Accepted

Companion to [0014](0014-pep723-skip-if-listed-search-first.md). Constraint record:
[`python-runtime.md`](../architecture/Execution/python-runtime.md).

## Context

RevitDevTool runs in-process CPython via pythonnet inside CAD/BIM hosts. Some hosts
(Plant 3D and similar) initialize CPython before the add-in loads. A second
in-process interpreter (another `python3xx.dll`, conda `Library\bin` activation,
or tearing down the host interpreter) causes ABI crashes, Win32 loader conflicts,
or use-after-free on exit.

Package installation is a separate concern from who owns the interpreter. The
product must support PyPI wheels for MCP, pytest, debugpy, and PEP 723 script
deps without replacing the host’s CPython or its stdlib layout.

An internal analysis argued that uv fits host-owned attach better than Pixi used
as an unused Conda prefix, and proposed deferring uv: keep Pixi for Plant with
`python = "3.13.*"` + `[pypi-dependencies]` only, then migrate later for
complexity. **The architectural argument was right; the recommended timeline was
not.** Host-owned already ships **uv as the primary manager**. Pixi is never
tried on that branch. Shipped Pixi is the **owned-interpreter** path (Revit
typical), with `python = "3.14.*"` and conda `[dependencies]` — not a Plant
sidecar.

The same analysis correctly named the real in-process hazard: **native DLL
closure and loader order**, not “Conda vs PyPI” as a marketplace brand.

## Decision

1. **Two axes, not one chain.** Separate (a) **interpreter owner** — host
   `python3xx.dll` already initialized vs this process owns pixi or pip — from
   (b) **package manager** — can `uv.exe` / `pixi.exe` run, else pyRevit
   `cengines` + `python -m pip`. Pip fallback does not change interpreter ownership.

2. **Host-owned CPython → uv (shipped default).** When
   `TryGetHostPythonDll` succeeds (`HostOwnsInterpreter`):
   - Bind pythonnet to the host versioned DLL (`python313.dll`), not `python3.dll`.
   - Run `InitializeEngine(hostDll)` **before** `ResolveProviderAsync` (CAD start
     thread; init lock via `WaitAsync`).
   - **Primary manager is uv only** — Pixi is not attempted on this path.
   - uv provides a **version-matched PyPI sidecar** keyed to host major.minor
     (`python313.dll` → `%APPDATA%\RevitDevTool\uv-env\3.13`).
   - **Why uv over Pixi-as-sidecar:** Pixi’s conda prefix carries `Library\bin`,
     a second Python layout, and activation semantics that must never be applied
     to a CAD-owned interpreter. uv is PyPI-native, installs packages into a
     detached venv, and uses `uv python install --no-bin` for a matching stdlib
     prefix without mapping a second in-process `PythonDLL` or replacing user
     Python shims.

3. **Venv sidecar, not `uv pip install --target` (today).** Shipped layout:
   - `uv python install --no-bin {major.minor}` → `uv-env\uv-python`
   - `uv venv --clear --python {major.minor}` → `uv-env\{major.minor}`
   - Package ops: `uv pip install --python <venv\Scripts\python.exe>` (not
     `python -m pip` on the host).
   - **Why not `--target`:** host attach still needs a **matching CPython prefix**
     (`Lib`, `DLLs`, PEP 384 `python3.dll`) for Plant-style overlays — host
     `python313.zip` stdlib, append sidecar `DLLs` then `Lib`, patch
     `encodings.__path__`, `LoadLibrary` sidecar `python3.dll` (never sidecar
     `python313.dll`). A flat `--target` directory is package-only; it does not
     supply that stdlib/native closure. `--target` remains a **future**
     simplification only if overlay of stdlib + forwarder is no longer required.

4. **No host-owned interpreter → Pixi owns in-process CPython (Revit typical).**
   - `ResolveProviderAsync` primary is `PixiEnvironmentProvider`.
   - Pixi conda `Library\bin` PATH / `AddDllDirectory` / OpenSSL preload run only in
     `PrepareProcess` before `InitializeEngine` when **this process owns** CPython.
   - **Do not** import that activation model into host-attach paths.
   - [0014](0014-pep723-skip-if-listed-search-first.md) search-first conda vs PyPI
     applies here; no rewrite required for host-attach (uv is PyPI-only).

5. **In-process package policy (host-attach).** Primary failure mode is **native
   DLL closure** and loader order (`Library\bin`, exe-adjacent Autodesk stubs),
   not marketplace label. For host-owned attach:
   - Prefer PyPI wheels whose native deps stay in private `.libs` / wheel layout.
   - Pure Python from either marketplace is technically fine.
   - Conda native stacks that require prefix `Library\bin` activation (GDAL,
     CUDA, OpenSSL-from-conda, etc.) are **out-of-process**, not injected into
     the host interpreter.
   - Never `os.add_dll_directory` pixi `Library\bin` or the uv prefix into the
     host interp; never set `PythonEngine.PythonHome` to the sidecar.
   - PEP 723 installs into the sidecar; `site.addsitedir(sidecar site-packages)`
     last after stdlib overlay.

6. **Pip is last-resort manager, not a parallel interpreter owner.** When
   `uv.exe` or `pixi.exe` cannot run (`VerifyRunnableAsync` / setup failure):
   - **Host owns interpreter:** pip uses a **matching** pyRevit `cengines\CPY*`
     as package sidecar only (same minor as host); pythonnet stays on host DLL.
   - **No host interpreter and primary failed:** pip’s ready `cengines\CPY*` **is**
     the in-process interpreter (`InitializeEngine(null, pipProvider)`).
   - Pip has no manager exe; it is not a third primary backend choice.

7. **Package UI follows active backend.** `PythonBackend` enum: Pixi / Uv / Pip.
   Three `IPythonPackageStore` implementations; `PackageService` picks the store
   whose `Backend` matches `PythonInitializer.Provider` (no backend `switch` in
   `PackageService`).

## Non-goals

- Duplicating the constraint laundry list in this ADR — see
  [`python-runtime.md`](../architecture/Execution/python-runtime.md).
- Replacing Pixi for **owned** interpreter (Revit default) with uv for symmetry.
- Pixi shell-hook / fake `CONDA_PREFIX` activation on host-owned attach.
- A “migrate Plant from Pixi to uv” project — uv is already the host-owned path.
- Mandating `uv pip install --target` without proving stdlib/forwarder overlay
  can be dropped.

## Alternatives Considered

1. **Pixi sidecar for host-owned (conda prefix, pypi-only, ignore conda Python).**
   Rejected. Conda prefix layout and `Library\bin` are the hazard; using Pixi
   only for PyPI does not remove that layout or the risk of accidental DLL mapping.
   uv venv + `uv-python` prefix matches the attach model with less conda surface.

2. **`uv pip install --target` as the primary sidecar layout.**
   Deferred. Spike (`samples/PythonDemo/commands/host_python_overlay_spike_script.py`)
   proved package overlay only; shipped code needs a full prefix for Plant stdlib
   + `python3.dll`. Revisit if host stdlib layout changes or overlay rules simplify.

3. **Single backend (Pixi or uv only).**
   Rejected. Owned interpreter benefits from Pixi conda-forge + search-first;
   host-owned needs a PyPI sidecar without conda activation. Pip retains
   offline / manager-exe-failure fallback.

## Consequences

Positive:

- Clear product rule: probe host DLL → uv sidecar; no host DLL → Pixi owns interp.
- “uv vs Pixi for Plant” debates close — the Plant path is already uv.
- Package policy focuses engineers on DLL closure, not Conda-vs-PyPI politics.

Tradeoffs:

- Two manager stacks (uv + Pixi) remain; complexity is bounded by the
  interpreter-owner axis.
- Host-attach depends on uv CLI + overlay logic; Plant-style hosts need ongoing
  constraint discipline ([python-runtime.md](../architecture/Execution/python-runtime.md)).
- Pip host-attach still requires pyRevit cengine version match and attached clone.

## Follow-Up

- Keep [`python-runtime.md`](../architecture/Execution/python-runtime.md) as the
  living constraint record when overlay or init order changes.
- Package orchestration: [`code-execution.md`](../architecture/Execution/code-execution.md)
  (Package Service section).

## References

- Source: `source/DevTools.Execution/Providers/Python/` (`PythonInitializer`,
  `UvEnvironmentProvider`, `PixiEnvironmentProvider`, `PipEnvironmentProvider`,
  `PythonNativeEnvironment`, `PythonDepsManager`)
- Tests: `tests/DevTools.Execution.Tests/UvHostCaptureTests.cs`, `UvArgsTests.cs`,
  `PipCengineTests.cs`
