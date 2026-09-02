"""Toolset workflow prompts — ported from C# ToolsetPrompts."""

from shared.mcp_registry import McpRegistry


def _resolve_domain(task: str, domain: str | None) -> str:
    if domain:
        return domain.strip().lower()
    t = task.lower()
    if any(k in t for k in ("duct", "pipe", "mep", "conduit", "hvac")):
        return "mep"
    if any(
        k in t for k in ("sheet", "view", "schedule", "documentation", "titleblock")
    ):
        return "documentation"
    if any(k in t for k in ("export", "pdf", "excel", "image", "spreadsheet")):
        return "export"
    if any(k in t for k in ("color", "highlight", "override", "visual", "tag")):
        return "visualization"
    return "query"


def register_toolset_prompts(mcp: McpRegistry) -> None:
    """Register workflow guidance prompts for MCP clients."""

    @mcp.prompt("revit_toolset_workflow")
    async def revit_toolset_workflow(task: str, domain: str | None = None) -> str:
        """Generate optimal multi-step tool call sequence."""
        resolved = _resolve_domain(task, domain)
        steps = {
            "mep": '1. `revit_list_types(kind="mep_system")` → 2. read `revit://toolset/patterns/mep` → 3. place segments → 4. `revit_list_mep_systems`',
            "documentation": "1. `revit_list_views` → 2. `revit_create_view` → 3. `revit_create_sheet` → 4. `revit_place_on_sheet` → 5. `revit_export_pdf`",
            "export": "1. `revit_get_status` → 2. read `revit://toolset/patterns/export` → 3. `revit_find_elements` → 4. export tool",
            "visualization": "1. `revit_find_elements` → 2. `revit_color_by_parameter` → 3. `revit_highlight_elements` → 4. `revit://view/screenshot`",
        }.get(
            resolved,
            "1. `revit_get_model_summary` → 2. `revit_find_elements` → 3. `revit_read_parameters`",
        )
        return (
            f"## Workflow for: {task}\n\n**Domain:** {resolved}\n\n### Pre-flight\n"
            "1. `revit_get_status`\n"
            "2. Batch `invoke_dynamic(reads=[capabilities, model/context, model/selection])`\n"
            f"3. Read `revit://toolset/patterns/{resolved}`\n\n### Steps\n{steps}\n\n"
            "### Verification\n1. Re-query with `revit_find_elements`\n2. On failure: `revit_undo_recovery` + `undo_changes`"
        )

    @mcp.prompt("revit_batch_operation")
    async def revit_batch_operation(
        operation: str, criteria: str, updates: str | None = None
    ) -> str:
        """Generate batch param/transform execution plan."""
        op = operation.strip().lower()
        write_tool = {
            "write_parameters": "revit_write_parameters",
            "swap_type": "revit_swap_type",
            "move": "revit_move_elements",
            "rotate": "revit_rotate_elements",
            "delete": "revit_delete_elements",
            "clone_parameters": "revit_clone_parameters",
        }.get(op, f"revit_{op}")
        return (
            "## Batch Operation Plan\n\n**Operation:** {}\n**Criteria:** {}\n**Updates:** {}\n\n"
            "### Phase 1 — Discover\n1. `revit_find_elements` with FilterSpec (max_results: 50)\n\n"
            "### Phase 2 — Sample\n1. Batch-read `revit://element/{{elementId}}` for 5 IDs, or `revit_read_parameters` when full params needed\n\n"
            "### Phase 3 — Execute\n1. Chunk IDs (50–100)\n2. Call `{}`\n\n"
            "### Phase 4 — Verify\n1. `revit_read_parameters` sample\n2. On failure: `undo_changes`"
        ).format(operation, criteria, updates or "(none)", write_tool)

    @mcp.prompt("revit_coordination_check")
    async def revit_coordination_check(
        categories: list[str],
        tolerance: float | None = None,
        rules: str | None = None,
    ) -> str:
        """Clash/interference check workflow with structured output schema."""
        tol = tolerance if tolerance is not None else 0.0
        rules_note = f"\n**Rules:** {rules}\n" if rules else ""
        return (
            "## Coordination Check\n\n**Categories:** {}\n**Tolerance:** {} ft{}\n\n"
            "### Phase 1 — Scope\n`revit_find_elements` per category with bbox filters\n\n"
            "### Phase 2 — Clash (god tool)\nUse `execute_csharp_code` with ElementIntersectsElementFilter\n\n"
            "### Phase 3 — Visualize\n`revit_override_colors` + `revit_highlight_elements`\n\n"
            "### Phase 4 — Report\n`revit_export_to_excel`"
        ).format(", ".join(categories), tol, rules_note)

    @mcp.prompt("revit_undo_recovery")
    async def revit_undo_recovery(failed_tool: str, error_context: str) -> str:
        """Recovery plan after tool failure."""
        return (
            f"## Undo Recovery\n\n**Failed tool:** {failed_tool}\n**Context:** {error_context}\n\n"
            "1. Parse `failures[]` codes — read `revit://toolset/errors`\n"
            "2. `undo_changes(count=1)` per write tool call\n"
            "3. `revit_find_elements` + `revit_read_parameters` to verify state\n"
            "4. Retry with smaller batches (≤50)"
        )

    @mcp.prompt("revit_worksharing_guide")
    async def revit_worksharing_guide(operation: str) -> str:
        """Sync/relinquish/borrow etiquette for concurrent use."""
        return (
            f"## Worksharing Guide: {operation}\n\n"
            "1. Read `revit://model/worksets` before writes\n"
            "2. `revit_sync_with_central` at task boundaries\n"
            "3. `revit_sync_with_central(relinquishAll=true)` when done\n"
            "4. Never sync mid-batch operation"
        )

    @mcp.prompt("revit_god_tool_decision")
    async def revit_god_tool_decision(task: str) -> str:
        """Decision tree: toolset tool vs execute_csharp_code."""
        t = task.lower()
        non_goals = (
            "wall",
            "floor",
            "roof",
            "stair",
            "beam",
            "curtain",
            "ifc",
            "rebar",
        )
        if any(ng in t for ng in non_goals):
            verdict = "Use god tool — task in non-goals list"
        elif "clash" in t or "interference" in t:
            verdict = "Hybrid: execute_csharp_code for detection, toolset for highlight/export"
        else:
            verdict = "Use toolset — read `revit://toolset/capabilities` to confirm"
        return (
            f"## God Tool Decision: {task}\n\n**Recommendation:** {verdict}\n\n"
            "1. Check `revit://toolset/capabilities` for matching `revit_*` tool\n"
            "2. If non-goal domain → `execute_csharp_code`\n"
            "3. If custom geometry algorithm → god tool + toolset chaining"
        )
