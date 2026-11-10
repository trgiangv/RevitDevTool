# Changelog

## [Unreleased]

## [4.0.0-beta.8]

### Changed

- Updates come from a signed `channels.json`. The Daemon and the add-in update separately, each from the channel the
  install is on (`stable` or `prerelease`).
- Daemon updates download a delta when the feed holds the installed version, otherwise the full package.
- The add-in update installs one zip per host and host version, so a busy Revit no longer holds back the AutoCAD
  update. `installation.json` moves to `%APPDATA%\RevitDevTool`.
- Developer diagnostics are no longer logged in the release build.

### Removed

- The GitHub release lookup. The updater reads only the signed channel.

### Upgrade

- RevitDevTool `4.0.0-beta.7` and earlier cannot read the new channel. Install this version once with
  `RevitDevTool-Setup.exe`; later versions arrive through the Updates tab.

## [4.0.0-beta.6]

### Added

- Each `tasks/*` request and its response or error is logged with the request id, task id, status, and error code.

## [4.0.0-beta.5]

### Added

- The Daemon window shows the product version (`DevTools 4.0.0-beta.5`).
- When the gateway rejects the Daemon, it stops reconnecting and shows "Not compatible with the gateway".

### Changed

- An update to a higher major waits while an add-in of that major is waiting for its host to close. Minor updates do
  not wait.

### Upgrade

- The MCP gateway rejects Daemons from before this release. Update the Daemon to reconnect the tunnel.

## [4.0.0-beta.4]

### Fixed

- Asset downloads retry after a network error, and wait when GitHub limits the request rate.

## [4.0.0-beta.3]

### Changed

- Starting the Daemon opens its window; a second start shows the window of the running Daemon. The Run value and the
  host add-ins start it in the background.
- After an update that you started, the Daemon opens on the Updates tab. A scheduled update restarts it in the
  background.

### Fixed

- The scheduled update check now runs.
- The staging folder is deleted after an update to a host selection.

## [4.0.0-beta.2]

### Fixed

- The Updates tab shows install progress on the Update button, detects Civil 3D and Plant 3D for the AutoCAD
  component, and uses the latest release of the chosen channel when none matches the Daemon version.

## [4.0.0-beta.1]

### Added

- `RevitDevTool-Setup.exe` replaces the Inno installer, and the Daemon has an Updates tab that updates the add-in by
  host and host version.
- `code_mode` replaces the MCP search and invoke tools.

### Changed

- The Daemon window moves from MewUI to HandyControl.
- The Daemon no longer lists machines.

### Fixed

- A sign-in already in progress is reused and reopens the browser.
- The tunnel uses a fresh token when it reconnects.