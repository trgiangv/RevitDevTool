"""Toolset workflow prompts."""

from shared.mcp_registry import McpRegistry


def register_toolset_prompts(mcp: McpRegistry) -> None:
    """Register workflow guidance prompts for MCP clients."""

    @mcp.prompt("acad_toolset_workflow")
    async def acad_toolset_workflow(task: str) -> str:
        """Generate a short multi-step AutoCAD tool sequence."""
        lowered = task.lower()
        if any(key in lowered for key in ("draw", "line", "circle", "create")):
            steps = (
                "1. `acad_get_status`\n"
                "2. `acad_list_layers` if a target layer is required\n"
                "3. `acad_draw_line` or `acad_draw_circle`\n"
                "4. `acad_find_entities` to verify the new handle"
            )
        elif any(key in lowered for key in ("erase", "delete", "remove")):
            steps = (
                "1. `acad_find_entities` or `acad_get_selection` to collect handles\n"
                "2. `acad_highlight_entities` so the engineer can see the set\n"
                "3. `acad_erase_entities`\n"
                "4. On failure: `navigate_history(direction=\"back\")`"
            )
        else:
            steps = (
                "1. `acad_get_status`\n"
                "2. Read `acad://drawing/context` and `acad://drawing/selection`\n"
                "3. `acad_find_entities` with type_name / layer filters\n"
                "4. `acad_highlight_entities` or a draw/erase tool as needed"
            )
        return (
            "## Workflow for: {}\n\n"
            "### Pre-flight\n"
            "1. Confirm AutoCAD/Civil host via `list_host_instances`\n"
            "2. Read `acad://toolset/capabilities`\n"
            "3. `acad_get_status`\n\n"
            "### Steps\n{}\n\n"
            "### Verification\n"
            "1. `acad_find_entities` or `acad://drawing/selection`\n"
            "2. Built-in `view_screenshot` when a visual check is needed\n"
        ).format(task, steps)
