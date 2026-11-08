# Changelog

## [Unreleased]

### Added
- Breaking: Four release trains in build/ with version files
- Breaking: Run NUnit from the user's output and stop the add-in shipping it
- Log tasks/* requests at the protocol boundary
- Send the Daemon sender on register
- Show the product version in window titles
- Open the window when the user starts the Daemon
- Host the update engine and add the Updates tab
- Rank catalog search with schema coverage and BM25F
- Save a large code_mode return and include its path
- Replace search and invoke with code_mode
- Add STJ source-gen facades for daemon wires
- MewUI desktop shell and folder layout split
- Compose host launch via DI specs
- Introduce hosting utilities for AutoCAD and Revit integration
- Symmetric catalogs + vision/undo workflows
- Add dynamic resources + symmetric primitive pattern
- Add DevTools.Daemon

### Changed
- Route remaining direct loads through the kernel
- Move tray composition into control, tunnel, desktop, and host connections
- Simplify search index scoring and related cleanup
- Fold list_machines into ListMachinesTool
- Fold Server/Client/Core/Adapter into Daemon and Catalog
- Replace MewUI with HandyControl WPF
- Adopt SDK types on the host wire
- Drop self-contained R2R for Daemon and TestRunner
- Rename host test handlers and drop unused IPC aliases
- Cut dead load paths and drop fake in-process DI
- Move HostApp identity out of Logging
- Breaking: Adopt MCP SDK 2.0 with platform split and spec-first host
- Reorganize Pytest bridge and update property names
- Typed McpSchemaBuilder + DynamicToolCatalog models
- Typed IHostBridgeClient API and handler deserialization
- Replace anonymous serialization with typed DTOs and enum HostApp
- Consolidate MCP layer architecture
- Decouple Presentation from Execution.External, remove dead code
- Add MCP bridge abstractions and move routing types to DevTools.Mcp
- Create DevTools.Mcp and move MCP models from McpParser
- Move IPC transport types to DevTools.Ipc with TypeForwardedTo
- Move ExecutionMode/State to DevTools.Execution.Abstractions with TypeForwardedTo
- Consolidate DaemonConstants and update references
- Update partial accessors (MVVMTK0042)

### Fixed
- Reference single-target FileMetadata projects without SetTargetFramework
- Show the install progress on the Update button
- Correct the Updates tab and the payload install flow
- Reuse an in-flight sign-in and reopen the browser
- Drop list_machines from the daemon
- Keep host pipe discovery from stalling on one connect
- Pass host source through a verbatim string in code_mode
- Return code_mode objects as JSON text
- Keep Revit command samples on TransactionMode.Manual
- Refresh host catalog on protocol 2026-07-28 and simplify sessions
- Own theme enum so Settings can drop UI
- Add Program entry point
- Prevent console log pollution on stdio MCP transport
- Update pipe name to prevent cross-app collisions
- Fresh token on tunnel reconnect, add TunnelStatus for dashboard
