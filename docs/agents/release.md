# Release Runbook

One shape for every product (ADR `docs/decisions/0059-independent-product-releases-and-one-gate-ci.md`,
plan `docs/plans/active/2026-11-06-product-releases-0059.md`). All four products are enabled in `release.yml`.

| Step | Who | How |
|------|-----|-----|
| 1. Bump PR | by hand | Raise the version in the product's version file, write the `## [X.Y.Z]` section in its `CHANGELOG.md`, move pins in docs that name the version |
| 2. Check | automatic | `ci / result` on the PR; `dotnet run --project build -- release <product> --dry-run` locally prints version, tag, notes source, artifacts, or one sentence naming the fix |
| 3. Run | by hand | Actions, workflow `release`, input `product`, from `develop` (or `release/<product>/X.Y` for a hotfix) |
| 4. Validate | automatic | `release <product> --dry-run` on the chosen commit; stops with the refusal sentence |
| 5. Build | automatic | `release <product>`: tests, then the artifacts and `notes.md`, no secrets, uploaded as `release-<product>` |
| 6. Approve | by hand | Reviewer approves environment `<product>` (twice for `daemon`/`addin`: `publish`, then `channels`) |
| 7. Publish | automatic | Attest provenance, then `publish <product>` (`nettest` logs in to NuGet by OIDC first) |
| 8. Channels | automatic | `daemon`/`addin` only: `channels <product> --version <v>` moves the product in the signed `channels.json` |
| 9. Install check | automatic | `daemon`/`addin` only: `verify-install` installs the channel's Setup on a clean runner; opens an issue on failure |

## daemon and addin

- `release daemon` downloads `releases.<channel>.json` and the full package of the previous `daemon/v*` release with the
  same prerelease state (`gh release download`), so `vpk pack` writes a delta. The feed then names this version and one
  delta base; a client further behind takes the full package. Velopack channels: `win` (stable), `prerelease`.
- `release addin` refuses until a `daemon/v*` tag at or above `MinUpdaterVersion` exists; a stable add-in needs a stable one.
- `publish <daemon|addin>` is the only step besides `channels` that reads `Update__SigningKey`. It signs the feed
  (`releases.<channel>.json` or `update-manifest.json`), checks it with the public key in the Daemon `appsettings.json`,
  then creates the GitHub Release `<product>/v<v>`: draft, payloads, feeds and signatures last, then published. A failure
  deletes the draft and its tag. Only a stable `daemon` is marked latest.
- Assets: `daemon` = `RevitDevTool-Setup.exe`, `RevitDevTool-<v>-*full.nupkg`, `RevitDevTool-<v>-*delta.nupkg` (when an
  earlier release of the channel exists), `releases.<channel>.json` + `.sig`, `assets.<channel>.json`.
  `addin` = `RevitDevTool-<v>-<year>.zip` per year, `RevitDevTool.bundle.zip`, `update-manifest.json` + `.sig`.
- `channels` runs one at a time (concurrency group `channels`). It checks that each URL it names answers before it
  uploads `channels.json`, then `channels.json.sig`.
- `verify-install` reads `channels.json`, installs the channel's Setup with `--silent`, runs `--select "Revit 2025"
  --select "AutoCAD 2025"` when the channel names an add-in, checks `installation.json` against the channel's add-in
  version and the bundle files, and checks that `--stdio` exits 0 within 60 s.
- Rerun after a failure: run `release` again. A published release is kept and skipped; a leftover draft is replaced.
  A bad `channels.json` is fixed by rerunning the `channels` job of the last good release.

## nettest

- `publish nettest` pushes `output/nettest/RevitDevTool.TestAdapter.<v>.nupkg` with `--skip-duplicate`, then
  creates the GitHub Release `nettest/v<v>` with `gh` unless it exists. It is never marked latest;
  a prerelease version sets `--prerelease`.
- Rerun after a failure: run `release` again with the same product. A version the registry holds is skipped,
  an existing release is kept.

## Order across products

Provider first: `daemon`, then `addin`, then `nettest` and `pytest`. The first `nettest` publish waits for the first
`addin` release.

## Owner setup on GitHub

- Environments `daemon`, `addin`, `nettest`, `pytest`, each with a required reviewer and deployments limited to
  `develop` and `release/*`.
- Secret `UPDATE_SIGNING_KEY` (ECDSA P-256 private key, PEM) in environments `daemon` and `addin`; its public key is
  `Update:PublicKey` in `source/DevTools.Daemon/appsettings.json`.
- Variable `NUGET_USER` (the nuget.org account name).
- nuget.org trusted publishing policy: repository `trgiangv/RevitDevTool`, workflow `release.yml`, environment `nettest`.
- Label `bug` (the `verify-install` issue uses it).
- Tag rulesets for `daemon/*`, `addin/*`, `nettest/*`, `pytest/*` that block updates and deletions but let the
  `release` workflow create tags. The first `channels` job creates the `channels` release and tag.

## Not yet written

Hotfix steps from `release/<product>/X.Y`.
