# Execution Plan: IPC Shared Pipes

Date: 2026-10-13

## Status

Completed

## Outcome

Three pipes stay. `DevTools.Ipc` stays the shared project. The daemon control
pipe has no reply. `TestPipeClient` completes the response for that request
id. Test-pipe tasks are tracked. A bad frame is not a clean end of stream.
New text uses the names in
[0043](../../decisions/0043-ipc-shared-infrastructure-separate-protocols.md)
decision 1.

## Context

- Decision: [0043](../../decisions/0043-ipc-shared-infrastructure-separate-protocols.md)
- Picture: [IPC](../../architecture/IPC/README.md)
- Current daemon control API: [daemon](../../architecture/MCP/daemon.md)

## Scope

In scope:

- Daemon control pipe: `control/sign_in` and `control/open_dashboard` only.
  No reply. Client close does not cancel sign-in. Accept does not wait for
  OAuth. Server opens with `NamedPipes`.
- Add-in: remove the running line, the sign-in overlay, and reply parsing.
- `TestPipeClient`: one completion source per request id. Drop the 25 ms poll.
- `DevToolsPipeServer`: no `async void`. Log frame faults. Do not cancel
  `IHostContextExecutor` work because the client disconnected.
- `BridgePipeConnection`: a bad length or bad JSON is a frame fault. A peer
  close is disconnect. Keep the 16 MiB read cap.
- Names in comments and the IPC page: test pipe, MCP pipe, daemon control
  pipe, stream, message format. Do not use `wire`.

Out of scope:

- Rename `BridgeMessage` or `BridgePipeConnection`.
- `SendNotification` still writes to every test-pipe connection.
- One version handshake for all three pipes.
- Live Python client against a host. In-repo tests cover the .NET frame.

## Approach

1. Daemon control pipe and the add-in settings view.
2. `TestPipeClient` request ids.
3. Test-pipe tasks and frame faults.
4. Comments, `daemon.md`, `transport.md` pointer, IPC page names.

## Risks And Recovery

- A breakpoint on the host thread must not wait forever in `StopAsync`.
  Track tasks and log faults. Do not wait on a handler that is inside
  `ExecuteAsync`.
- Revert the change set if daemon control tests or test-pipe frame tests
  fail. The message format bytes stay the same, so the Python client does
  not need a protocol change.

## Progress

- [x] Plan accepted and file created
- [x] Daemon control pipe has no reply
- [x] Add-in running line and sign-in overlay removed
- [x] `TestPipeClient` waits on the request id
- [x] Test-pipe tasks tracked; bad frame is not EOF
- [x] Names updated in comments and architecture pages
- [x] Focused tests and `DevTools.Ipc` compile for net48, net8, net10

## Decisions

- 2026-10-13: Follow 0043. Do not reopen pipe count, `wire`, or a
  `BridgeMessage` rename.

## Validation

- Focused proof: `DevTools.Daemon.Tests` control pipe,
  `DevTools.TestRunner.Tests` request id,
  `DevTools.Execution.Pytest.Tests` frame fault and pipe server.
- Integration or end-to-end proof: no live host in this plan.
- Repository-required checks: `dotnet build` on `DevTools.Ipc` (net48, net8,
  net10) and the touched test projects.

## Result

Passed on 2026-10-13.

- `dotnet build source/DevTools.Ipc/DevTools.Ipc.csproj -c Debug` — net48, net8.0-windows, net10.0-windows.
- `dotnet build` of `DevTools.Presentation` (net48, net8, net10), `DevTools.Daemon`, `DevTools.Execution` (net48, net8, net10), and `DevTools.TestRunner`.
- `dotnet run --project tests/DevTools.Daemon.Tests/DevTools.Daemon.Tests.csproj -- --filter "FullyQualifiedName~ControlPipeHandlerTests|FullyQualifiedName~ControlJsonTests|FullyQualifiedName~GatewayAndControlJsonTests"` — 10 passed.
- `dotnet run --project tests/DevTools.TestRunner.Tests/DevTools.TestRunner.Tests.csproj -- --filter "FullyQualifiedName~TestPipeClientTests"` — 1 passed.
- `dotnet run --project tests/DevTools.Execution.Pytest.Tests/DevTools.Execution.Pytest.Tests.csproj -- --filter "FullyQualifiedName~PytestBridgeFramingTests|FullyQualifiedName~DevToolsPipeServerDisconnectTests|FullyQualifiedName~DevToolsPipeServerTests|FullyQualifiedName~PipeServerCoverageTests"` — 10 passed.

Behavior tests ran on net10. net48 and net8 are compile-proven. No live Python client and no host process.
