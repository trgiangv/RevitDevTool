# Documentation Map

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
| End-user guides | [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki) |

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
