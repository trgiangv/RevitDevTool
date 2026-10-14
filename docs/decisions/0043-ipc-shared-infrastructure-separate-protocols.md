# 0043 IPC: Shared DevTools.Ipc, Separate Pipes

Date: 2026-10-13

## Status

**Accepted 2026-10-13. Implemented 2026-10-13** by
[2026-10-13-ipc-shared-pipes](../plans/completed/2026-10-13-ipc-shared-pipes.md).
The defect list below is the code before that change.

Does not change [0010](0010-daemon-sole-mcp-host.md),
[0027](0027-mcp-product-surface.md),
[0031](0031-daemon-json-source-gen.md), or
[0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md).
The host MCP pipe stays an SDK session. Daemon JSON stays source-generated.
Each message format keeps its own `JsonSerializerContext`.

## Context

Each host process listens on two pipes. The Daemon listens on one pipe.
The count is `2N + 1` for N host processes. The gap is the listener code,
not the pipe count.

| Pipe | Name | Message format | Owner |
|------|------|------|--------|
| MCP pipe | `DevToolsMcp_{Host}_{Version}_{PID}` | SDK newline JSON-RPC | `McpPipeServer` + `McpServer` |
| Test pipe | `DevTools_{Host}_{Version}_{PID}` | Length-prefix `BridgeMessage` | `DevToolsPipeServer` |
| Daemon control pipe | `DevToolsDaemon_Control` | One JSON command line. No reply | `ControlPipeHostedService` |

The test pipe carries `tests/run`, `ipytests/run`, `testing/hello`,
`testing/run`, `testing/cancel`, `testing/progress`, and `instance/info`.
These are methods on one message format. `FormatTest` and `IsTestPipe` are the code
names of this pipe. `BridgeMessage` is the message type, not the pipe name.

`NamedPipes` opens a current-user server. `McpPipeServer` and
`DevToolsPipeServer` call it. `ControlPipeHostedService` does not.
`BridgePipeConnection` frames a `Stream` for the test pipe only.
The max frame is 16 MiB.

Defects on `develop`:

- The daemon control pipe reads once into 4096 bytes and writes a JSON line
  back. `control/sign_in` blocks the accept loop. Client disconnect cancels
  that sign-in. The add-in calls `control/status`, `control/sign_in`, and
  `control/open_dashboard`. `control/status` sets the text "Daemon: Running".
  `control/auth_state`, `control/sign_out`, and `control/connected_hosts`
  have no production caller. The Daemon window already shows auth state.
- `TestPipeClient` polls `_inbox` every 25 ms. It puts back a message for
  another request id. It keeps late ids in `_discardedResponseIds`.
  This client is the .NET test runner. The Python pytest plugin is a second
  client of the same test pipe.
- `OnMessageReceived` and `SendNotification` are `async void`.
  `SendNotification` writes to every test-pipe connection.
  `StartReadLoop` drops the read task. A bad length, bad JSON, and a clean
  EOF all return null and raise `Disconnected`.
- Disconnect cancels the connection `CancellationTokenSource`.
  `TestingRequestHandler` passes that token to `provider.Run`.
  `MarshaledTestRequestHandler` and `PytestRequestHandler` do not pass it
  to `IHostContextExecutor.ExecuteAsync`. Cancel of that dispatcher task,
  while the host thread is in a breakpoint, disposes the idle-work token.
  The next `testing/run` then waits and does not start. Cancel at a
  breakpoint does not free the host thread until Continue. The next
  `testing/hello` starts a new session.

## Decision

1. **Use one name per object.** New comments, new docs, and the refactor
   use this table. Type names in the table stay as they are in code.

   | Term | Use for | Do not call it |
   |------|---------|----------------|
   | test pipe | `DevTools_{Host}_{Version}_{PID}` | Bridge, pytest pipe, endpoint, control pipe |
   | MCP pipe | `DevToolsMcp_{Host}_{Version}_{PID}` | SDK pipe, host stream |
   | daemon control pipe | `DevToolsDaemon_Control` | control, control pipe |
   | stream | the bytes of one connection | pipe, message format |
   | message format | how those bytes are arranged | stream, protocol, wire |
   | `DevTools.Ipc` | the shared project | infrastructure, primitives, shared layer, factory |
   | `NamedPipes` | the server factory in that project | the name of `DevTools.Ipc` |
   | `BridgeMessage` | the test-pipe message type | the name of the test pipe |
   | no reply | the daemon control pipe writes nothing | one-way, writes nothing, close-no-read |

   `control/` stays on the method name `control/sign_in`. It is not the name
   of a pipe. `instance/info` is a method on the test pipe. It is not the
   daemon control pipe.

2. **Keep three pipes.** Each listener keeps its own stream.
   Share `NamedPipes`, a bounded read, shutdown, and the fault log.
   Do not put daemon control commands or MCP frames on `BridgeMessage`.
   Do not put the test pipe or the daemon control pipe on the MCP SDK.

3. **Do not add an RPC engine.** No `StreamJsonRpc`. No `IRpcEngine`
   around `McpClient` or `McpServer`. The MCP pipe keeps
   `StreamClientTransport` and `StreamServerTransport`.
   The test pipe keeps request, response, and notification, matched by
   message id. The daemon control pipe has no reply.

4. **`DevTools.Ipc` keeps the test-pipe frame and the server factory.**
   It owns `NamedPipes`, `BridgePipeConnection`, `BridgeMessage`, and
   shutdown signals. It does not own host execution, retry, MCP dispatch,
   or timeouts. `IBridgeRequestHandler` stays the test-pipe method hook.
   Do not add a handler interface for the MCP pipe or the daemon control
   pipe. Method names stay with their owner: `TestingProtocol`, pytest
   methods, and `IpcConstants` for `control/sign_in`.

5. **The daemon control pipe has no reply.** The add-in writes one JSON
   line and closes the stream. The Daemon reads that line up to a size
   limit. It does not write. The command is `control/sign_in`. Remove
   `control/status`, `control/auth_state`, `control/sign_out`,
   `control/connected_hosts`, `control/open_dashboard`, and the reply records
   those methods serialize. `ControlJsonContext` stays for token and
   gateway payloads ([0031](0031-daemon-json-source-gen.md)).
   Client close does not cancel sign-in. The accept loop does not wait
   for OAuth. Open the server with `NamedPipes`. Remove the add-in text
   "Daemon: Running", the sign-in overlay, and `DaemonClient` reply
   parsing. The Sign In button sends the command.
   Auth result stays on the Daemon window. A connect probe can still start
   the Daemon process. That probe is not a status method.

6. **`TestPipeClient` waits on the request id.** Replace the inbox, the
   25 ms poll, and the requeue with
   `ConcurrentDictionary<string, TaskCompletionSource<BridgeMessage>>`.
   A response completes only its id. A late or unknown id is dropped.
   Keep `testing/hello`, `testing/run`, `testing/cancel`, and
   `testing/progress`. Keep `tests/run` and `ipytests/run`.
   The Python plugin is out of this client change.

7. **Track test-pipe tasks. Pipe cancel is not host cancel.** Replace
   `async void` with tasks that are logged. Stop waits for the read loop.
   It does not wait for a handler that is inside host execution.
   Disconnect closes that stream. It does not cancel or dispose
   `IHostContextExecutor` work while the host thread is in a breakpoint.
   Keep the split in `MarshaledTestRequestHandler`: the pipe token is not
   the dispatcher token. Dispose the connection token only after in-flight
   host work has left that token. Disconnect does not make the next
   `testing/run` wait.

8. **A bad test-pipe frame is not EOF.** A length below 1, a length above
   16 MiB, and JSON that is not a `BridgeMessage` are protocol errors.
   Peer close stays `Disconnected`. Each pipe keeps its own limit.
   The daemon control pipe does not use the test-pipe reader.

9. **`SendNotification` can stay a write to every test-pipe connection
   until a publisher has a connection id or a run id.** Clients already
   drop other run ids. Do not change progress method names for this.

10. **Each message format keeps its serializer.** MCP SDK options,
    `IpcJsonContext`, `ControlJsonContext`, and `TestingJsonContext` stay
    separate ([0031](0031-daemon-json-source-gen.md) rule 3). A shared rule
    is a bounded payload, source-generated JSON for each Daemon message
    format, and a protocol error you can tell from EOF. Do not merge the
    message types. Do not make one error-code enum for the three pipes.

The refactor is done when there is less task state to manage, and when new
text uses the names in decision 1. The goal is not more interfaces.

## Alternatives Considered

1. **Adopt `StreamJsonRpc` for the test pipe and the daemon control pipe.**
   Rejected. Both message formats are small and already shipped. The MCP pipe already
   uses the official SDK.
2. **One RPC engine for all three pipes.** Rejected. Pytest runs when the
   MCP session is absent. A breakpoint on the test pipe does not follow
   MCP cancel. The daemon control pipe has two commands.
3. **Put daemon control commands on `BridgeMessage`.** Rejected. The command
   is one short JSON line. A shared reader would tie the tray pipe to the
   test-pipe frame.
4. **Keep the daemon control reply.** Rejected. The reply blocks the accept
   loop on OAuth, cancel on client close, and the "Daemon: Running" text.
   The Daemon window already shows that state.
5. **Keep the `TestPipeClient` poll.** Rejected. The requeue is the race.
   A completion source per id removes the poll. The message format stays.
6. **Cancel `ExecuteAsync` when the test pipe disconnects.** Rejected.
   `MarshaledTestRequestHandler` already blocks that path. The next
   `testing/run` would wait.
7. **Rename `BridgeMessage` to a test-pipe type.** Rejected for this
   decision. Decision 1 separates the pipe name from the type name.
   A type rename is a separate change.

## Consequences

Positive:

- `NamedPipes` is the one place for the server ACL and the accept options.
- The add-in does not wait for Daemon auth, and it does not show a running
  line. Client close does not cancel sign-in.
- The test runner does not depend on a 25 ms poll.
- The MCP pipe stays on the SDK path from
  [0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md).

Tradeoffs:

- The add-in cannot tell if the Daemon is up, or if sign-in succeeded.
  That state stays on the Daemon.
- Three serializers remain. The daemon control pipe has no reply records.
- `SendNotification` still writes to every test-pipe connection until a
  publisher has a connection id.
- Host work can still run after the client disconnects. That keeps the
  next `testing/run` able to start after a breakpoint.

## Follow-Up

The refactor applies decision 1 in the same change as the behavior:

- Comments that call the test pipe "pytest/control" or "Bridge".
  `HostPipeName` is one of these.
- [IPC](../architecture/IPC/README.md) uses the names in decision 1.
  It does not keep a second glossary.
- Add-in strings and `DaemonClient` lose the reply parse and the running
  line.

Order:

1. **Reliability.** Daemon control pipe with no reply. `TestPipeClient`
   waits on the request id. The test-pipe message format stays. Proof: sign-in still
   runs after the add-in closes the stream. The window still opens. The
   settings view has no running line and no sign-in overlay. A test-pipe
   response for the right id is not lost.
2. **Tasks.** Replace `async void`. Log the read-loop fault. Keep pipe
   cancel separate from host cancel. Proof: disconnect, cancel, and a host
   thread in a breakpoint do not fault the process. The next `testing/run`
   or `tests/run` starts.
3. **Faults.** A bad frame is not EOF. Cover net48, net8, and net10, and
   the Python client. Include two request ids, a frame over the limit, a
   bad frame, disconnect, and cancel. Do not add one version handshake for
   all three pipes.

When step 1 lands, point `docs/architecture/MCP/transport.md` at this
decision for the daemon control pipe. Use the names in decision 1.
Do not copy this decision into that file.
