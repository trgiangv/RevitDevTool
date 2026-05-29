# Decision Log

Use this file for durable architecture decisions that affect agent behavior. Keep entries short.

## 2026-05-29: Repo-owned AI harness

- `AGENTS.md` is the entry contract and router.
- `docs/ai/` contains deterministic agent digests.
- `.agents/skills/*/SKILL.md` contains task-specific checklists.
- Tool-specific files should be thin adapters that point back to the repo-owned harness.

## 2026-05-29: Host-agnostic direction

- The project is no longer treated as Revit-only.
- Revit and AutoCAD are current hosts.
- Shared `DevTools.*` libraries should remain host-neutral unless a host API dependency is unavoidable.

## 2026-05-29: Architecture docs as source of truth

- Important features and architecture changes should update the matching docs.
- Module READMEs hold durable architecture.
- `docs/ai/` holds agent workflow and decision context.
- Skills hold short task checklists.

## 2026-05-29: GitNexus unavailable for current index run

- `npx gitnexus analyze` fails in `scopeResolution` even after ignoring vendor `libs/` and cleaning `.gitnexus`.
- `.gitnexusignore` excludes vendor/generated/runtime folders so future indexing should focus on repo-owned code.
- Until analyzer failure is resolved, agents should inspect source directly and not rely on GitNexus graph freshness.
