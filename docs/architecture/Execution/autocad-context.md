# AutoCAD Execution Context

`AcadHostContextExecutor` (`source/AcadDevTool/Adapters/AcadHostContextExecutor.cs`) is the AutoCAD-family implementation of `IHostContextExecutor`. MCP, pytest (`tests/run`), Microsoft Testing Platform (`testing/run`), command run, and `AcadDocumentBridge` all enter the host through it. Revit uses `ExternalEvent` in `RevitHostContextExecutor`. AutoCAD does not.

`Document.LockDocument()` locks a document for read/write. It does not move work onto the UI thread and it does not enter command context.

## Three separate axes

| Axis | What it is | Failure when ignored |
|------|------------|----------------------|
| UI thread | The thread that owns the AutoCAD dispatcher | `Dispatcher.VerifyAccess()` |
| Execution context | Application context, or the document command context | `Editor.Command` / `acedCommand` rejected; some document activation is deferred |
| Document lock | `acDocManager->lockDocument()`, managed as `Document.LockDocument()` | Lock violation when modifying a database from application context |

Application context and document context both run on AutoCAD's command processor, normally the UI thread. A worker thread is outside both. Command context is not a synonym for "the UI thread".

Autodesk defines the two contexts in [Explicit Document Locking](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide/files/GUID-9FCD49BC-ADD0-44D2-A599-F625BB63BE22.htm) and the application-context limits in [Code Differences under the Application Execution Context](https://help.autodesk.com/cloudhelp/2019/ENU/OARX-DevGuide/files/GUID-634ADE0B-35FD-4146-A2D0-3621D2FB5B0C.htm).

### Document context

A `[CommandMethod]` without `CommandFlags.Session` runs here. AutoCAD locks and unlocks the current document. `Editor.Command` is valid. Changing the active document suspends the command; the new document does not process input until the command returns.

### Application context

`CommandFlags.Session`, modeless UI, COM/ActiveX, and some `acrxEntryPoint` messages run here. The command is not part of one document's command processor. Modifying a database requires `LockDocument()`. `Editor.Command` is not valid until code re-enters command context. The command can activate another document and keep running; that document processes input only after this command returns.

`CommandFlags.Session` matters only when AutoCAD's command processor dispatches the method. `AcadCommandRunner` invokes the method by reflection, so the attribute does not change context. Context is whatever the executor established before the call.

### How to enter command context

| API | What it does |
|-----|----------------|
| `Document.SendStringToExecute` | Queues a command-line string. The command runs after the current .NET command ends. It does not run a callback or return its result. [Access the Command Line](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-DevGuide-Managed/files/GUID-F4A36181-39FB-4923-A2AF-3333945DB289.htm). |
| `DocumentCollection.ExecuteInCommandContextAsync` | Runs a callback in command context when AutoCAD can start a command. On AutoCAD 2022–2027 the return value is `DocumentCollection.ExecutionResult` (awaitable; not a `Task`). |
| `ExecuteInApplicationContext` | The opposite direction: defers a callback into application context. |

`LockDocument()` stays necessary when code stays in application context, or when it modifies a document other than the current one. [Lock and Unlock a Document](https://help.autodesk.com/cloudhelp/2025/ENU/OARX-DevGuide-Managed/files/GUID-A2CD7540-69C5-4085-BCE8-2A8ACE16BFDD.htm).

## What the executor does

```text
caller (MCP pipe, pytest, test run, command strategy, document bridge)
  -> Dispatcher.InvokeAsync on the ribbon dispatcher
       HostUiHelper.HostDispatcher, else ComponentManager.Ribbon.Dispatcher
  -> no active document, or IsApplicationContext == false
       run the handler on that UI thread
  -> active document and application context
       await DocumentCollection.ExecuteInCommandContextAsync
```

Rules of that path:

- `DocumentCollection.IsApplicationContext` is the context check. There is no thread-static depth counter.
- The returned task completes when the AutoCAD callback finishes. A cancelled token skips the handler if the callback has not started. It does not complete the task while the handler is still running.
- Exceptions from the handler are caught inside the callback. The callback returns `Task.CompletedTask`. The original exception is rethrown with `ExceptionDispatchInfo` after AutoCAD accepts that callback result.
- The handler is a synchronous `Func<T>`. An `await` inside user code is not an AutoCAD API context.
- The executor does not take a document lock, start a transaction, or record which document was active when the call was scheduled.

`IHostContextExecutor` stays that primitive. Document identity, transactions, and MCP timeouts belong to the operation that calls it.

## What it does not solve

- Setting `MdiActiveDocument` inside the callback does not switch the active document before the callback returns. The next executor call observes the new active document.
- `Database.SaveAs` can write the file while `Database.Filename` still names the previous drawing for the rest of that callback.
- A handler that runs with no active document is on the UI thread in application context. It can open a drawing. It cannot assume `Editor` or `Database` exist.
- `ComponentManager.Ribbon.Dispatcher` is only a fallback. The dispatcher captured at host startup (`HostUiHelper.Initialize` from `HostBackgroundController`) is the one the executor prefers.
- Cancelling a token does not abort an AutoCAD API call that has already entered the handler.
- A modal dialog or a native command such as `LINE` waiting for input can delay the callback. `ExecuteInCommandContextAsync` may also cancel the in-progress native command when it is dispatched. That behavior was not exercised.
- If AutoCAD never runs the callback, the task stays pending until the host can enter command context.

## Checked on Plant 3D 2027

Live pass on 2026-10-11. `launch_host` started `acad.exe /product PLNT3D` (process 19212) after deploying `Debug.Autodesk.2027` to `%AppData%\Autodesk\ApplicationPlugins\RevitDevTool.bundle`. Calls went through `execute_python_code` and `execute_csharp_code`.

| Scenario | Observed |
|----------|----------|
| MCP worker reads the drawing, `Editor.GetCurrentView`, `Editor.WriteMessage` | `IsApplicationContext` is false. View width returned. Editor write succeeded. Managed thread was 2. |
| `Editor.Command("._ZOOM", "_E")` | Succeeded. Context stayed document context. |
| Transaction appends a `Line`, then reads it back | Handle `26E` created and found. |
| Python raises `RuntimeError` | Tool returns `[RUNTIME ERROR]`. The host stays up. |
| Next call after that error | Same drawing, document context, editor write succeeds. |
| C# `[CommandMethod]` | Succeeds. The method throws if `IsApplicationContext` is still true; it did not throw. |
| `Database.SaveAs` to `%TEMP%\RevitDevTool-executor-probe.dwg` | File written (32,666 bytes). During the callback, `Database.Filename` was still the template `acad.dwt`. |
| `DocumentCollectionExtension.Add("acad.dwt")` and assign `MdiActiveDocument` | `Drawing2.dwg` opened. Active document was still `Drawing1.dwg` before the callback returned. |
| Following MCP call | Active document is `Drawing2.dwg`, document context, `BlockTable` read and editor write succeed. |

Not run: cancel before the callback starts, cancel while the handler is inside an API call, a modal message loop, closing Plant 3D with a callback queued, and the no-document branch.

The Plant 3D session was left with `Drawing1.dwg` (the probe line), `Drawing2.dwg`, and the temp DWG above.
