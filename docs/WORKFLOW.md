# Repository Workflow

Canonical default workflow. Repository product behavior,
architecture, plans, decisions, code, tests, and runtime signals are the system
of record. Optimize for reliable agent execution with minimal human attention
and process overhead.

## Repository Map

- `AGENTS.md`: small entry map and authority boundary.
- `README.md` and `docs/product/`: current product behavior.
- `docs/ARCHITECTURE.md` and `docs/architecture/`: structural module truth.
- `docs/decisions/`: lasting decisions.
- `docs/agents/`: agent task router and operational digests.
- `docs/plans/active/`: complex work currently in progress.
- `docs/plans/completed/`: completed execution history worth retaining.
- Project code, tests, CI, hooks, and runtime signals: executable truth.
- `docs/agents/verification.md` and `docs/agents/build-matrix.md`: verify/build
  commands for this repository.

Use `docs/README.md` for the map; prefer targeted search.

## Select The Work Shape

Answer three questions independently; do not let one risk label decide them.

### Does The Work Need Durable Memory?

Use an ephemeral plan for bounded, single-session work.

Create or update one execution plan in `docs/plans/active/` when work:

- is likely to span sessions;
- coordinates multiple agents or contributors;
- has meaningful dependencies or an important sequence;
- requires an explicit recovery procedure; or
- would be unsafe or expensive to resume from the final diff alone.

Use `docs/templates/exec-plan.md`. Keep progress and task-local decisions in the
same file. Do not create parallel story, design, validation, and trace documents
for the same work unless one has independent long-term value.

### Does The Work Need Human Judgment?

Before editing, identify repository authority for each new externally
observable policy. If materially different choices remain open, stop before
edits and request the smallest necessary decision. Configurable defaults are
not authority.

For example, `Add rate limiting` without a quota, trusted key, enforcement
topology, or response contract must stop. `Enforce the documented 20 requests
per minute per authenticated tenant` may proceed.

Also pause when:

- product intent remains ambiguous;
- the action is irreversible or difficult to recover;
- validation, security, or compatibility requirements would be weakened; or
- the requested work does not authorize the necessary action.

### What Proves The Behavior?

Choose proof from the affected behavior:

- focused tests for local rules;
- compile-hook feedback for .NET API/shared edits;
- integration tests for persistence and service boundaries;
- host pytest (`uv run pytest` in `RevitDevTool.PyTest`) for bridge/host API;
- recovery rehearsal for migrations and destructive operations; and
- runtime measurements for reliability or performance claims.

Harness rows, proof flags, trace tiers, context scores, and entropy scores do not
prove product behavior by themselves.

## Task Flows

### Read-Only Request

For an answer, explanation, review, diagnosis, plan, or status report:

1. Read `AGENTS.md` and only the material needed for the response.
2. Use read-only inspection commands when useful.
3. Do not edit files or mutate Harness state.
4. Stop when concrete repository evidence supports the answer.

Discovery never grants authority to fix what it finds.

### Bounded Change

1. Restate the observable outcome.
2. Read the relevant product or architecture material, `docs/agents/` digest,
   affected code, adjacent patterns, and existing tests.
3. Make the smallest coherent change that satisfies the outcome.
4. Run focused proof plus repository-required checks (prefer compile hooks).
5. If behavior or boundaries changed, update the matching doc layer only.
6. Report the outcome, important changed surfaces, proof, and known limitations.

No bootstrap, intake, story, matrix, trace, scoring, audit, or proposal command
is required.

### Durable Planned Change

1. Create or resume one plan in `docs/plans/active/`.
2. Record outcome, context, approach, risks, recovery, progress, decisions, and
   validation in that file.
3. Implement in coherent, independently verifiable groups.
4. Update progress and decisions as reality changes.
5. Run the plan's focused and repository-wide proof.
6. Promote lasting product or architecture decisions into `docs/decisions/`.
7. Update `docs/product/` and/or `docs/architecture/<Module>/` when contracts or
   design changed.
8. Record the final result and move the plan to `docs/plans/completed/`.

The plan is working memory, not a prediction frozen at intake. Update it when
evidence changes the approach.

## Documentation Layers

| Change type | Update |
|-------------|--------|
| Observable behavior | `docs/product/<domain>.md` |
| Module design / structure | `docs/architecture/<Module>/` |
| Agent workflow / traps / verify | `docs/agents/` |
| Lasting policy | `docs/decisions/` |
| Multi-session work | `docs/plans/active/` |
| Bounded fix, no contract change | code + proof only |

Do not duplicate the same truth across layers; link instead.

## Completion Standard

A change is complete only when:

- the requested outcome exists or the blocker is explicit;
- relevant product and design truth remains current;
- behavior-appropriate proof has passed, or missing proof is disclosed without
  overstating completion;
- durable plan progress and result are current when a plan was required; and
- the final report separates verified facts, limitations, and unattempted work.

Git history, pull-request discussion, test artifacts, screenshots, videos,
logs, metrics, and plan progress are preferred evidence because they arise from
the work. Manual descriptions may add context but do not replace observed proof.

## Compatibility Control Plane

The optional repository-harness SQLite CLI remains unsupported for ordinary
RevitDevTool work. Use it only when explicitly requested.
