# 0051 Update Integrity and Trust

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Part of the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).

## Context

An updater that downloads and loads code into Revit and AutoCAD without user
action is a code-execution channel. Today release assets are unsigned, and the
only trust is HTTPS to GitHub plus the GitHub account that publishes releases.
`PublishGithubModule` runs in CI with `GITHUB_TOKEN`. A stolen token or a
compromised account could publish a release that every `auto` client applies.

## Decision

- **Hashes are mandatory.** Every year payload has `sha256` and `size` in the
  year manifest (0046). Velopack checks the hashes in its own feed for Daemon
  packages. A mismatch discards the download and reports a failed update.
- **Both feeds are signed.** CI signs `update-manifest.json` and
  `releases.<channel>.json` with an **ECDSA P-256** key held as a repository
  secret and uploads `.sig` files. The custom `IUpdateSource` (0053) and the
  year engine verify them with `System.Security.Cryptography.ECDsa`, so no extra
  package is needed. The public key is compiled into `DevTools.Update`. An
  unsigned or badly signed feed is ignored. Velopack hashes then bind each
  package to the signed feed.
- **Key rotation:** the manifest may name a next public key, signed by the
  current key. Clients accept the next key from that release on.
- **Downgrade protection:** the engine applies only versions greater than the
  installed one, except an explicit user rollback (0049) and the crash-on-start
  rollback (0052), which uses a signed feed of the previous tag.
- **Host allowlist:** downloads follow redirects only to `github.com` and GitHub
  release-asset hosts, over HTTPS.
- **Zip safety:** year extraction rejects absolute paths and `..` entries and
  stays inside the staging folder.
- **Authenticode** signing of `RevitDevTool-Setup.exe` and the Daemon is
  desirable for SmartScreen but is not required by this decision. When a
  certificate exists, `vpk pack --signParams` (or Azure Trusted Signing) does it.

## Alternatives Considered

1. **HTTPS and hashes only.** Protects transport, not a malicious release from a
   compromised publisher.
2. **Authenticode on every DLL, verified before apply.** Strong, but needs a paid
   certificate and signing every assembly in every year.
3. **Sigstore/cosign.** Good provenance, but needs network checks at verify time
   and a larger client.
4. **Ed25519.** Smaller keys and signatures, but needs a package such as NSec on
   the client; ECDSA P-256 is in the BCL.

## Consequences

Positive:

- Publishing a release that clients apply needs both GitHub access and the signing key.
- Offline installs (0047 `--source`) get the same checks.

Tradeoffs:

- A lost signing key needs a `Setup.exe` reinstall on every machine to recover.
- One more CI secret and one more release step.

## Follow-Up

- Generate the key pair; store the private key as a repository secret.
- Add a signing step after `GenerateUpdateManifestModule` and `PackDaemonModule`.
- Decide on Authenticode separately when a certificate is available.
