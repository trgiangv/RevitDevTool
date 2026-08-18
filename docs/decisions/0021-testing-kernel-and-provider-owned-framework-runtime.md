# 0021 Testing Kernel And Provider-Owned Framework Runtime

Date: 2026-08-17

## Status

Accepted. Refines [0020](0020-framework-neutral-mtp-host-testing.md) after an
independent review of the implemented boundaries. Items 1-6 are the current
priority. Polyfill/PolySharp work is gated behind review of items 1-6.

## Context

The first framework-neutral extraction introduced `DevTools.Testing.Abstractions`,
`DevTools.Testing.Transport`, `DevTools.Testing.Host`, and
`DevTools.Testing.Mtp`, but the implementation is not yet neutral end to end:

- generic contracts and host dispatch still contain NUnit identifiers and
  `nunit/*` compatibility routing;
- most coherent-generation, runtime-session, assembly-resolution, and unload
  mechanics remain in `DevTools.NUnit.Host`;
- `DevTools.NUnit.Core` mixes generic runner configuration, NUnit discovery and
  filters, legacy wire DTOs, and the cross-load-context runtime contract;
- MTP and VSTest packages link source files from `DevTools.NUnit.Core`, creating
  duplicate compilation and packaging ownership;
- the generic result contract loses NUnit hierarchy, skip, attachment, and
  provider-extension information;
- `DevTools.TestRunner` is a generic executable name over an NUnit-coupled
  implementation.

The goal is not to genericize NUnit semantics. It is to separate reusable
testing control-plane and runtime-isolation mechanisms from provider-owned
framework behavior and compatibility workarounds.

## Decision

1. **Make `DevTools.Testing.Abstractions` the only shared testing identity.**
   It owns framework-neutral run, selection, event, result, attachment,
   diagnostic, cancellation, and runtime-session contracts. Framework IDs are
   opaque provider-owned strings; the project contains no NUnit constant.
   Contracts preserve hierarchy, full name, skip reason, content type, inline
   attachment content, and a versioned opaque provider payload. It references
   no MTP, JSON, IPC, framework, reflection, process, or Autodesk assembly.

2. **Move compatibility out of the generic handler.**
   `DevTools.Testing.Host` registers and handles only `testing/*`. Legacy
   `nunit/*` DTOs, protocol negotiation, serialization, and request routing
   remain temporarily in `DevTools.NUnit.Transport` and
   `DevTools.NUnit.Host`. Host composition may register both handlers, but the
   generic handler never rewrites a request onto NUnit.

3. **Extract a policy-driven generation and runtime kernel.**
   `DevTools.Testing.Host` owns immutable generation staging, content hashing,
   coherent copy/publish retries, manifests, managed/native indexes, current
   and obsolete generation tracking, active runs, cancellation, session
   retirement, and unload diagnostics. Providers supply an explicit generation
   plan and runtime/assembly-resolution policies. The kernel knows no framework
   DLL name, version, filter, result type, or framework load workaround.

4. **Keep NUnit policy and semantics in `DevTools.NUnit.*`.**
   `DevTools.NUnit.Host` owns NUnit 4.6.1 closure validation,
   `nunit.framework` host-sharing, NUnit-specific shared/private resolution,
   selection mapping, provider activation, result mapping, and legacy routing.
   `DevTools.NUnit.Runtime` retains the real NUnit runner, listeners, filters,
   identities, source locations, result interpretation, output routing, and
   NUnit lifecycle. It implements the neutral runtime contract without exposing
   NUnit objects across the load-context boundary.

5. **Split Runner infrastructure from the NUnit command provider.**
   `DevTools.TestRunner` remains the one executable and composition root.
   Framework-neutral command parsing, host locate/launch/reuse, debugger policy,
   and generic `testing/*` process transport live in `DevTools.TestRunner.Core`.
   NUnit CLI (`discover`/`run`, filter XML) lives in the executable as an
   `IRunnerCommandModule`. `DevTools.TestRunner.Core` does not reference any
   framework provider. Additional frameworks register another module; they do
   not require a separate `*.Runner` project.

6. **Decompose and remove `DevTools.NUnit.Core`.**
   Generic runner configuration, paths, timing, and process contracts move to
   `DevTools.Testing.Abstractions` or `DevTools.Testing.Transport`. NUnit
   discovery/filter/mapping moves to the appropriate NUnit MTP/Runner provider.
   Legacy contracts move to `DevTools.NUnit.Transport`. The cross-load-context
   runtime contract moves to `DevTools.Testing.Abstractions`. Source linking
   from NUnit MTP/VSTest/Runner is removed. `DevTools.NUnit.Core` is deleted only
   after all consumers compile and tests prove equivalent behavior.

7. **Defer PolySharp migration.** The current global `Polyfill` package creates
   substantial net48 metadata and duplicate ILRepack inputs. PolySharp may
   reduce compiler-polyfill noise, but it does not provide the broad downlevel
   runtime APIs currently supplied by `Polyfill`, and per-project generation
   can still create duplicate types. No Polyfill/PolySharp/package/ILRepack
   policy changes are allowed until items 1-6 pass review. A later isolated
   spike must prove net48 compilation, metadata validity, ILRepack output,
   isolated-ALC loading, and host execution before changing [0019].

## Required Dependency Direction

```text
DevTools.Testing.Abstractions
        ^
        +-- DevTools.Testing.Transport
        +-- DevTools.Testing.Host
        +-- DevTools.Testing.Mtp
        +-- DevTools.NUnit.Runtime
        +-- DevTools.NUnit.Host
        +-- DevTools.NUnit.Mtp

DevTools.TestRunner.Core
        ^
        +-- DevTools.TestRunner (exe; NUnit CLI module + host-family launch)
```

Forbidden dependency directions:

- `DevTools.Testing.*` to any `DevTools.NUnit.*` project;
- `DevTools.TestRunner.Core` to any framework provider;
- `DevTools.NUnit.Runtime` to MTP, Runner, IPC, Autodesk APIs, or host launch;
- shared contracts to JSON, reflection, process, or load-context implementation.

## Runtime Kernel Boundary

The common kernel consumes descriptions and policies rather than framework
conditionals:

```csharp
public sealed record TestingGenerationFile(
    string SourcePath,
    string RelativePath,
    TestingGenerationFileKind Kind);

public sealed record TestingGenerationPlan(
    string FrameworkId,
    string SourceAssemblyPath,
    IReadOnlyList<TestingGenerationFile> Files,
    string RuntimeAssemblyRelativePath);

public interface ITestingGenerationPolicy
{
    TestingGenerationPlan CreatePlan(string testAssemblyPath);
    void ValidatePublished(TestingGenerationManifest manifest);
}

public interface ITestingRuntimeSessionFactory
{
    ITestingRuntimeSession Create(TestingGenerationManifest generation);
}
```

Provider policy decides which files enter the plan and how framework closure is
validated. The common store decides how that plan is copied, hashed, published,
tracked, and retired.

## Compatibility And Capability Rules

- New execution uses only `testing/*` and neutral contracts.
- Legacy `nunit/*` remains an NUnit-owned adapter until its remaining VSTest
  callers are removed or deliberately retained by a later decision.
- Discovery remains local and cannot launch or contact a host.
- Common result fields must not discard framework information. Data that is
  not common is preserved in a versioned opaque provider payload.
- A compatibility adapter may map old DTOs to new DTOs exactly once. Generic
  code must not map a neutral DTO back through an NUnit DTO.

## Alternatives Rejected

1. **Rename `DevTools.NUnit.Core` to `DevTools.Testing.Core`.** Rejected because
   the project contains NUnit discovery, filters, legacy DTOs, and mappings;
   renaming would hide rather than correct the dependency boundary.
2. **Move all NUnit runtime code into the common kernel.** Rejected because
   NUnit discovery, identity, filters, lifecycle, output, and result semantics
   must remain framework-owned.
3. **Keep legacy NUnit routing inside `TestingRequestHandler`.** Rejected
   because it makes every future provider inherit an NUnit compatibility path.
4. **Replace Polyfill while moving boundaries.** Rejected because assembly
   movement already changes the ILRepack input closure; combining both changes
   would make invalid metadata and load failures difficult to attribute.

## Consequences

Positive:

- a future framework provider can reuse host generation/session mechanics
  without referencing NUnit-named code;
- NUnit DLL-hell workarounds remain intact but isolated behind provider policy;
- the shared loose assembly is a real cross-ALC contract rather than a partial
  façade over NUnit DTOs;
- source-link duplication and framework leakage become enforceable architecture
  violations;
- PolySharp can later be evaluated against a stable packaging graph.

Tradeoffs:

- the migration temporarily carries both neutral and legacy NUnit contracts;
- extracting the loader requires parity tests across net48 and modern ALCs;
- `DevTools.TestRunner` remains a composition root that references the NUnit
  module even though its reusable core is framework-neutral;
- deletion of `DevTools.NUnit.Core` is the final migration step, not the first.

## Validation Gates

- architecture tests reject NUnit strings/references in `DevTools.Testing.*`;
- neutral contract round-trip tests preserve all capability fields;
- generation hash, atomic publish, corruption, retry, native asset, session,
  cancellation, and unload tests pass through the common kernel;
- NUnit host/net48/runtime/MTP/TestAdapter/Runner tests remain green;
- all touched multi-target projects compile for net48, net8, and net10;
- IDE/local discovery proof confirms no host process is opened;
- no Polyfill, PolySharp, or ILRepack policy file changes occur in items 1-6.
