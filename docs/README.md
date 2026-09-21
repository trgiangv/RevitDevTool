# Documentation Map

This folder contains both the repository's engineering documentation and the
public Docusaurus documentation site.

## Public documentation site

The Docusaurus project lives in this folder, following the same single-repository
layout used by TUnit:

| Path | Role |
|------|------|
| [docs/](docs/) | Public, user-facing Markdown consumed by Docusaurus |
| [docusaurus.config.ts](docusaurus.config.ts) | Site configuration |
| [sidebars.ts](sidebars.ts) | Public navigation |
| [src/](src/) | Site theme and custom UI |
| [static/](static/) | Site assets |

From this directory:

```bash
bun install
bun run start
bun run build
```

`bun run build` writes the generated site to `docs/build/`. Do not edit that
generated directory. The public Markdown is intentionally separate from the
engineering source-of-truth below; link to product and architecture documents
when deeper detail is needed.

### GitHub Pages

CI workflow [`.github/workflows/Docs.yml`](../.github/workflows/Docs.yml) builds
on PRs and deploys to GitHub Pages on push to `develop` / `main` (and
`workflow_dispatch`).

One-time repo setting: **Settings → Pages → Source = GitHub Actions**.
Site URL: https://trgiangv.github.io/RevitDevTool/

Start here when locating repository truth. Retrieve only what the task needs.

## Harness Core

| Path | Role |
|------|------|
| [WORKFLOW.md](WORKFLOW.md) | Request shape, judgment, validation, completion |
| [product/](product/README.md) | Current product behavior contracts |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Structural index → `architecture/` |
| [plans/](plans/README.md) | Durable multi-session working memory |
| [decisions/](decisions/README.md) | Lasting product and architecture choices |
| [templates/](templates/) | Exec-plan and decision templates |

## Domain Layers

| Path | Role |
|------|------|
| [architecture/](ARCHITECTURE.md) | Deep module design (Execution, MCP, PyTest, Testing, Logging, Visualization, …) |
| [agents/](agents/index.md) | Agent task router and operational digests |
| [static/](static/) | Icon and installer assets (not behavioral truth) |

## Quick Links

| I want to… | Read |
|------------|------|
| Choose how to work on a request | [WORKFLOW.md](WORKFLOW.md) |
| Know current platform behavior | [product/overview.md](product/overview.md) |
| Route an agent task | [agents/index.md](agents/index.md) |
| Understand a module deeply | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Resume complex work | [plans/active/](plans/active/) |
| Inherit a lasting choice | [decisions/](decisions/README.md) |
| End-user guides | [Docusaurus site](https://trgiangv.github.io/RevitDevTool/) |

## Update Rule

- Behavior change → `product/<domain>.md`
- Module structure / design change → `architecture/<Module>/`
- Agent workflow / verify traps → `agents/`
- Lasting policy → `decisions/`
- Multi-session work → `plans/active/`
- Bounded fix with no contract change → code + proof only

Do not duplicate the same fact across layers; link instead.

## Related

- Root contract for agents: [AGENTS.md](../AGENTS.md)
- User-facing README: [../README.md](../README.md)
