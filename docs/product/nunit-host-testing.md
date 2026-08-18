# NUnit host testing

RevitDevTool runs NUnit tests inside Revit, AutoCAD, and Civil 3D through
Microsoft Testing Platform (MTP). `RevitDevTool.NUnit` is the only public test
integration package. The VSTest adapter and NUnit-specific bridge protocol are
not part of the supported product on `develop`; their final baseline is retained
on branch `testing/nunit-vstest`.

## User contract

- Test projects reference `RevitDevTool.NUnit`, `NUnit`, and
  `Microsoft.Testing.Platform.MSBuild`.
- Test projects are executable MTP applications (`OutputType=Exe`) and declare
  `HostName`, `HostVersion`, and optional launch/timeout settings.
- IDE discovery reads PE metadata locally. It must not locate, launch, or contact
  an Autodesk host.
- Running a test starts or reuses the selected host and sends only the neutral
  `testing/hello`, `testing/run`, and `testing/cancel` contracts.
- The NUnit provider keeps NUnit filters, traits, source locations, skip reasons,
  attachments, output, and result semantics without exposing a second wire model.

## Ownership

| Module | Responsibility |
|---|---|
| `DevTools.Testing.Abstractions` | Neutral run/result/runtime contracts shared across host boundaries |
| `DevTools.Testing.Transport` | Neutral `testing/*` JSON and pipe transport |
| `DevTools.Testing.Host` | Neutral request marshaling, assembly preflight, provider registry, generation store, and runtime-session lifecycle |
| `DevTools.Testing.Mtp` | Framework-neutral MTP runner/session plumbing |
| `DevTools.NUnit.Discovery` | Host-free NUnit metadata discovery and NUnit selection mapping |
| `DevTools.NUnit.Runtime` | NUnit execution inside an isolated generation |
| `DevTools.NUnit.Host` | NUnit closure/version policy, Dynamo-safe framework sharing, isolated runtime activation, and host provider wiring |
| `DevTools.NUnit.Mtp` | Published `RevitDevTool.NUnit` MTP framework |
| `DevTools.TestRunner` | Out-of-process host orchestration using `testing/*` only |

The cross-load-context identity is `DevTools.Testing.Abstractions`. Runtime
payloads do not carry a provider-specific transport assembly. Supported runtime
targets are net48, net8, and net10; the former `netstandard2.0` compatibility
target is removed.

## Sample and execution

The maintained samples are:

- `samples/DevTools.NUnit.SampleTests` for Revit;
- `samples/DevTools.NUnit.Civil3D.SampleTests` for Civil 3D.

Run the generated test executable or use the MTP-aware `dotnet test`/IDE surface
provided by the installed SDK. Discovery remains host-free; host launch occurs
only after an execution request.

## Packaging

`RevitDevTool.NUnit` is packed from `source/DevTools.NUnit.Mtp`. Modern targets
keep implementation assemblies in a private runtime closure; net48 keeps the
established single-assembly repack. Consumers see only the MTP framework compile
surface. The release workflow publishes this one package.

See [decision 0022](../decisions/0022-nunit-mtp-only-testing-stack.md) for the
MTP-only boundary and [decision 0021](../decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md)
for the neutral kernel extraction history.
