using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RevitMcpToolSet.Prompts;

[McpServerPromptType]
[Description("Structured workflow prompts for Revit MCP toolset agents.")]
public static class ToolsetPrompts
{
    [McpServerPrompt(Name = "revit_toolset_workflow", Title = "Revit Toolset Workflow")]
    [Description("Returns a multi-step plan with specific toolset tool calls, resource reads, and verification steps.")]
    public static string GetWorkflow(
        [Description("The task to accomplish")] string task,
        [Description("Domain hint: query, mep, documentation, export, visualization")] string? domain = null)
    {
        var resolvedDomain = ResolveDomain(task, domain);
        var steps = resolvedDomain switch
        {
            "mep" => BuildMepWorkflow(task),
            "documentation" => BuildDocumentationWorkflow(task),
            "export" => BuildExportWorkflow(task),
            "visualization" => BuildVisualizationWorkflow(task),
            _ => BuildQueryWorkflow(task),
        };

        return $$"""
            ## Workflow for: {{task}}

            **Domain:** {{resolvedDomain}}
            **Principle:** One write tool = one transaction named `MCP: {tool_name}` for targeted undo.

            ### Pre-flight
            1. `revit_get_status` — confirm active document, worksharing state, and units.
            2. Batch prefetch (one `invoke_dynamic` with `reads[]`): `revit://toolset/capabilities`, `revit://model/context`, `revit://model/selection`.
            3. Read `revit://toolset/patterns/{{resolvedDomain}}` for domain-specific FilterSpec and chaining examples.
            4. If scope is unclear, use `search_dynamic(kinds=["resource","resource_template"])` then batch-read selection/context templates.

            ### Steps
            {{steps}}

            ### Verification
            1. Re-query affected elements with `revit_find_elements` or `revit_read_parameters`.
            2. Read `revit://model/warnings` if writes were performed.
            3. Optional: `revit://view/screenshot` for visual confirmation.
            4. On unexpected state: invoke `revit_undo_recovery` prompt, then `undo_changes(count=1)`.

            ### Performance notes
            - Paginate `revit_find_elements` with `offset` when results exceed 500.
            - After find: batch-read `revit://element/{elementId}` for top 5 IDs instead of `revit_read_parameters` when only summary fields are needed.
            - Sample with `revit_read_parameters` on a subset before batch writes.
            - Read `revit://toolset/capabilities` before attempting exotic operations.
            - Bulk delete (&gt;50 IDs): structured warning JSON — use `dryRun=true` first to preview scope before deleting.
            """;
    }

    [McpServerPrompt(Name = "revit_batch_operation", Title = "Revit Batch Operation")]
    [Description("Returns a find → read → write/transform sequence for batch operations on element sets.")]
    public static string GetBatchOperation(
        [Description("Operation type: write_parameters, swap_type, move, rotate, delete, clone_parameters, color_override")] string operation,
        [Description("FilterSpec JSON or human-readable selection criteria")] string criteria,
        [Description("Updates to apply: parameter map, transform spec, or target type — omit for delete/query-only")] string? updates = null)
    {
        var op = operation.Trim().ToLowerInvariant();
        var writeTool = op switch
        {
            "write_parameters" or "write" or "parameters" => "revit_write_parameters",
            "swap_type" or "swap" or "type" => "revit_swap_type",
            "move" or "move_elements" => "revit_move_elements",
            "rotate" or "rotate_elements" => "revit_rotate_elements",
            "delete" or "delete_elements" => "revit_delete_elements",
            "clone_parameters" or "clone" => "revit_clone_parameters",
            "color_override" or "color" or "override_colors" => "revit_override_colors",
            _ => $"revit_{op}",
        };

        return $"""
            ## Batch Operation Plan

            **Operation:** {operation}
            **Criteria:** {criteria}
            **Updates:** {updates ?? "(none — query or delete only)"}

            ### Phase 1 — Discover scope
            1. `revit_get_status` — verify document is open and workshared if applicable.
            2. `revit_find_elements` with FilterSpec:
               ```
               {criteria}
               ```
               - Start with `max_results: 50` to validate criteria.
               - Paginate with `offset` until all targets are collected.
            3. Record `total_count` and element IDs from response.

            ### Phase 2 — Sample and validate
            1. `revit_read_parameters` on first 5–10 element IDs — confirm current values and writable params.
            2. If workshared, read `revit://model/worksets` — check borrowed/pinned elements.
            3. For parameter writes: `revit_list_category_parameters` to confirm parameter names and types.

            ### Phase 3 — Execute in batches
            1. Chunk element IDs into batches of 50–100.
            2. Per batch, call `{writeTool}`:
               ```
               {updates ?? "/* operation-specific payload */"}
               ```
            3. Inspect each response for `success_count` and `failures[]`.
            4. On partial failure: do NOT retry failed IDs blindly — read `revit://toolset/errors` for recovery codes.

            ### Phase 4 — Verify and recover
            1. `revit_read_parameters` on a sample of updated IDs.
            2. `revit_find_elements` with same FilterSpec — confirm expected state.
            3. If corruption or wrong scope: `undo_changes(count=N)` where N = number of write tool calls made.
            4. Invoke `revit_undo_recovery` prompt with `failed_tool="{writeTool}"` for structured recovery.

            ### Safety rules
            - Never operate on whole model without explicit FilterSpec scope.
            - One `{writeTool}` call = one undo transaction.
            - Stop and consult engineer if `element_borrowed` or `element_pinned` failures exceed 10%.
            """;
    }

    [McpServerPrompt(Name = "revit_coordination_check", Title = "Revit Coordination Check")]
    [Description("Returns a spatial conflict detection workflow with structured output schema.")]
    public static string GetCoordinationCheck(
        [Description("Categories to check, e.g. Ducts, Pipes, Structural Framing")] string[] categories,
        [Description("Clash tolerance in feet (Revit internal units). Default 0.0 for hard clash.")] double? tolerance = null,
        [Description("Optional rules: ignore_same_system, ignore_insulation, level_filter, etc.")] string? rules = null)
    {
        var categoryList = categories.Length > 0
            ? string.Join(", ", categories)
            : "(none specified — agent must clarify)";
        var tol = tolerance ?? 0.0;
        var ruleText = string.IsNullOrWhiteSpace(rules) ? "default (hard clash, no same-system exemption)" : rules;

        return $$"""
            ## Coordination Check Workflow

            **Categories:** {{categoryList}}
            **Tolerance:** {{tol}} ft
            **Rules:** {{ruleText}}

            ### Output schema (agent must produce)
            ```json
            {
              "summary": { "total_clashes": 0, "by_category_pair": {} },
              "clashes": [
                {
                  "element_a": { "id": 0, "category": "", "name": "" },
                  "element_b": { "id": 0, "category": "", "name": "" },
                  "overlap_volume_cuft": 0.0,
                  "location": [0, 0, 0],
                  "severity": "hard | clearance",
                  "notes": ""
                }
              ]
            }
            ```

            ### Phase 1 — Scope elements
            1. `revit_get_status` — confirm 3D view availability or create one.
            2. For each category in [{{categoryList}}]:
               - `revit_find_elements` with `{ "type": "category", "names": ["<category>"] }`
               - Collect element IDs and bounding boxes.
            3. Read `revit://model/levels` to constrain by level if rules specify.
            4. Read `revit://model/links` — decide whether to include linked models.

            ### Phase 2 — Spatial pre-filter
            1. `revit_find_elements` with `bounding_box` filters to narrow candidate pairs per zone.
            2. Use tolerance {{tol}} ft: expand bboxes by tolerance for clearance checks.
            3. Exclude pairs matching rules: {{ruleText}}

            ### Phase 3 — Clash detection (god tool)
            The toolset has no native clash engine. Use `execute_csharp_code` with:
            - `FilteredElementCollector` + `ElementIntersectsElementFilter` or solid intersection.
            - Compare solids from `element.get_Geometry(Options)` with `{{tol}}` ft clearance logic.
            - Return clashes matching the output schema above.

            ### Phase 4 — Visualize and report
            1. `revit_color_by_parameter` or `revit_override_colors` — highlight clashing element IDs (red).
            2. `revit_highlight_elements` — zoom to clash locations in UI.
            3. `revit_export_to_excel` — export clash table for engineer review.
            4. Optional: `revit://view/screenshot` for visual record.

            ### Phase 5 — Cleanup
            1. `revit_clear_overrides` on affected views when review is complete.
            2. Document unresolved clashes for coordination meeting.
            """;
    }

    [McpServerPrompt(Name = "revit_undo_recovery", Title = "Revit Undo Recovery")]
    [Description("Returns steps to diagnose failure, undo transactions, and verify model state.")]
    public static string GetUndoRecovery(
        [Description("Name of the tool that failed, e.g. revit_write_parameters")] string failedTool,
        [Description("Error message, failure codes, or partial-success response")] string errorContext)
    {
        return $$"""
            ## Undo Recovery Plan

            **Failed tool:** {{failedTool}}
            **Error context:** {{errorContext}}

            ### Step 1 — Diagnose
            1. Parse `failures[]` for error codes: `constraint_violation`, `element_borrowed`, `element_pinned`, `group_member`, `param_readonly`, `type_mismatch`, `not_found`.
            2. Read `revit://toolset/errors` for code-specific recovery guidance.
            3. Check `suggestedAction` in each failure — e.g. `release workset`, `unpin element`, `use undo_changes`.
            4. `revit_get_status` — confirm document is still open and not in a corrupted state.

            ### Step 2 — Assess undo depth
            - Each toolset write tool commits exactly one transaction named `MCP: {tool_name}`.
            - Count how many `{{failedTool}}` calls succeeded before the failure.
            - If unsure, start with `undo_changes(count=1)` and inspect.

            ### Step 3 — Undo
            1. Call `undo_changes(count=1)` — returns undone transaction names.
            2. Verify the undone name matches `MCP: {{failedTool}}`.
            3. Repeat `undo_changes` until all bad writes are reverted (max 5 per cycle unless engineer approves).
            4. Do NOT use `revit_sync_with_central` during recovery.

            ### Step 4 — Verify state
            1. `revit_find_elements` — re-query affected element IDs from error context.
            2. `revit_read_parameters` — confirm values match pre-change state.
            3. Read `revit://model/warnings` — check for new warnings introduced.
            4. Optional: `revit://view/screenshot` for visual check.

            ### Step 5 — Retry or escalate
            | Error code | Action |
            |------------|--------|
            | `element_borrowed` | Ask engineer to release workset; read `revit_worksharing_guide` prompt |
            | `element_pinned` | Exclude pinned IDs from batch; retry on unpinned subset |
            | `param_readonly` | Remove readonly params from update payload |
            | `constraint_violation` | Reduce batch size; fix driving constraints manually |
            | `group_member` | Ungroup or exclude group members from scope |

            ### Step 6 — Prevent recurrence
            - Sample with `revit_read_parameters` before batch writes.
            - Use smaller batches (≤50 elements).
            - Invoke `revit_batch_operation` prompt to plan safer execution.
            """;
    }

    [McpServerPrompt(Name = "revit_worksharing_guide", Title = "Revit Worksharing Guide")]
    [Description("Returns best practices for a specific worksharing operation.")]
    public static string GetWorksharingGuide(
        [Description("Operation: sync, relinquish, borrow, open, save, close")] string operation)
    {
        var op = operation.Trim().ToLowerInvariant();
        var guide = op switch
        {
            "sync" or "synchronize" or "sync_with_central" => """
                ### Sync with Central
                1. **Before sync:** `revit_save_document` — ensure local changes are saved.
                2. Read `revit://model/worksets` — note borrowed elements and editable worksets.
                3. Finish or pause in-progress write batches — partial tool calls should not span sync.
                4. `revit_sync_with_central(comment="agent: <task summary>", saveLocalBefore=true)`.
                5. **After sync:** `revit_get_status` — confirm latest central version.
                6. Re-read `revit://model/worksets` — borrowed elements may have changed ownership.

                **Etiquette:**
                - Sync at natural breakpoints, not mid-batch.
                - Use descriptive comments so engineers can trace agent activity.
                - Do not sync after every single tool call — batch related changes first.
                - If sync fails due to conflicts, read `revit://model/warnings` and resolve before retry.
                """,

            "relinquish" or "relinquish_all" or "release" => """
                ### Relinquish Worksets and Borrowed Elements
                1. Read `revit://model/worksets` — list worksets you own or have borrowed elements in.
                2. Complete or undo pending writes (`undo_changes` if needed).
                3. `revit_sync_with_central(relinquishAll=true, comment="agent: relinquish after <task>")`.
                4. Verify: `revit://model/worksets` — no borrowed elements remain under your user.

                **Etiquette:**
                - Relinquish when done with a work area so teammates can edit.
                - Never relinquish while a batch operation is in progress.
                - Relinquish before end-of-session, not only at sync.
                - Warn engineer if relinquishing worksets with uncommitted design intent.
                """,

            "borrow" or "checkout" or "edit" => """
                ### Borrowing Elements
                1. Read `revit://model/worksets` — identify workset owners and borrowed status.
                2. `revit_find_elements` — target only elements in editable worksets.
                3. Attempt write via toolset tool — Revit auto-borrows on first edit if permitted.
                4. On `element_borrowed` failure: element is owned by another user.

                **Etiquette:**
                - Do not force-borrow elements another engineer is actively editing.
                - Prefer editing elements in worksets assigned to your team.
                - Borrow the minimum scope needed for the task.
                - Release (relinquish) promptly after edits are complete.
                - Coordinate with engineer before borrowing from shared/system worksets.
                """,

            "open" => """
                ### Opening a Workshared Model
                1. If the host is not running: `launch_host` with `filePath` (host inferred from extension).
                2. If the host is already running: `invoke_dynamic` on `open_document` with the central path.
                3. Choose appropriate worksets — do not load all worksets unless needed.
                4. `revit_get_status` — confirm worksharing enabled and local path.
                5. Read `revit://model/worksets` — understand ownership before any writes.

                **Etiquette:**
                - Open only required worksets to reduce contention.
                - Create local file in team-standard location.
                - Sync soon after open to get latest changes.
                """,

            "save" => """
                ### Saving in Workshared Context
                1. `revit_save_document` — saves local file only (not sync).
                2. Saving does NOT share changes with the team — sync is required.
                3. Save before long batch operations as a safety checkpoint.

                **Etiquette:**
                - Save locally at milestones; sync at collaboration breakpoints.
                - Do not confuse save with sync — teammates won't see local saves.
                """,

            "close" => """
                ### Closing a Workshared Model
                1. Complete or undo pending agent writes.
                2. `revit_sync_with_central(relinquishAll=true)` — share changes and release ownership.
                3. `revit_save_document` if sync did not save local.
                4. `revit_close_document(save=false)` — after sync saved.

                **Etiquette:**
                - Never close without syncing if writes were made.
                - Always relinquish on close to avoid locking elements.
                - Warn engineer if unsynced changes must be discarded.
                """,

            _ => """
                ### General Worksharing Rules
                1. Read `revit://model/worksets` before any write operation.
                2. Batch edits within one workset before syncing.
                3. Use `revit_sync_with_central` at task boundaries, not per element.
                4. Relinquish when done — `revit_sync_with_central(relinquishAll=true)`.
                5. On ownership errors, invoke this prompt with operation `borrow` or `relinquish`.

                Supported operations: sync, relinquish, borrow, open, save, close.
                """,
        };

        return $"""
            ## Worksharing Guide: {operation}

            {guide}

            ### Toolset tools involved
            - `revit_get_status` — worksharing flag and document path
            - `revit_sync_with_central` — sync, optional relinquish
            - `revit_save_document` / `revit_close_document` — local persistence
            - `revit://model/worksets` — live ownership and editability
            """;
    }

    [McpServerPrompt(Name = "revit_god_tool_decision", Title = "God Tool Decision")]
    [Description("Returns a recommendation with reasoning for whether to use a toolset tool or the god tool.")]
    public static string GetGodToolDecision(
        [Description("The task to evaluate")] string task)
    {
        var recommendation = EvaluateGodToolNeed(task);

        return $"""
            ## God Tool Decision: {task}

            **Recommendation:** {recommendation.Verdict}
            **Preferred approach:** {recommendation.Approach}

            ### Reasoning
            {recommendation.Reasoning}

            ### Decision tree
            1. Read `revit://toolset/capabilities` — is there a matching `revit_*` tool?
               - **Yes, exact match** → use toolset tool (structured params, single-transaction undo).
               - **Yes, but needs composition** → chain toolset tools; invoke `revit_toolset_workflow` prompt.
               - **No match** → proceed to step 2.
            2. Is the task in the **non-goals** list?
               - Wall/floor/roof/ceiling/stair creation, structural framing, rebar, in-place families,
                 curtain walls, IFC import/export, energy analysis, link attach/reload, detail items/dimensions
               - **Yes** → use `execute_csharp_code`.
            3. Does the task need **custom geometry algorithms** (clash detection, complex intersections, bespoke filters)?
               - **Yes** → `execute_csharp_code` for algorithm; toolset for pre/post (find, color, export).
            4. Is it a **one-off** script unlikely to repeat?
               - **Yes** → `execute_csharp_code`.
               - **No** → prefer toolset for safety and undo granularity.

            ### Examples

            | Task | Use |
            |------|-----|
            | Find all doors on Level 2 with empty Mark | `revit_find_elements` + `revit_read_parameters` |
            | Batch-set Fire Rating on 200 walls | `revit_write_parameters` via `revit_batch_operation` |
            | Place a duct between two points | `revit_place_duct` |
            | Create sheet package with views | `revit_create_sheet` + `revit_place_on_sheet` + `revit_export_pdf` |
            | Detect duct-pipe clashes in a zone | `execute_csharp_code` (clash) + `revit_color_by_parameter` (highlight) |
            | Create a curved curtain wall | `execute_csharp_code` (non-goal) |
            | Custom scheduling logic across 5 parameters | `execute_csharp_code` |
            | Export room data to Excel | `revit_find_elements` + `revit_export_to_excel` |

            ### If using god tool
            1. Read `revit://api-cheatsheet` for API patterns.
            2. Read `revit://model/context` for current model state.
            3. Wrap all mutations in a single `Transaction` named descriptively.
            4. Return structured JSON (element IDs, counts, errors) for downstream toolset chaining.
            5. Prefer toolset `undo_changes` after god-tool writes — god tool commits normal Revit transactions.
            """;
    }

    private static string ResolveDomain(string task, string? domain)
    {
        if (!string.IsNullOrWhiteSpace(domain))
            return domain!.Trim().ToLowerInvariant();

        var t = task.ToLowerInvariant();
        if (t.Contains("duct") || t.Contains("pipe") || t.Contains("mep") || t.Contains("conduit") || t.Contains("hvac"))
            return "mep";
        if (t.Contains("sheet") || t.Contains("view") || t.Contains("schedule") || t.Contains("documentation") || t.Contains("titleblock"))
            return "documentation";
        if (t.Contains("export") || t.Contains("pdf") || t.Contains("excel") || t.Contains("image") || t.Contains("spreadsheet"))
            return "export";
        if (t.Contains("color") || t.Contains("highlight") || t.Contains("override") || t.Contains("visual") || t.Contains("tag"))
            return "visualization";
        return "query";
    }

    private static string BuildQueryWorkflow(string task) => $"""
        1. `revit_get_model_summary` — category counts and project overview.
        2. `revit_find_elements` — build FilterSpec for: {task}
           - Read `revit://toolset/patterns/query` for FilterSpec examples.
        3. `revit_read_parameters` — extract fields needed for analysis.
        4. Optional: `revit_export_to_excel` — deliver results to engineer.
        """;

    private static string BuildMepWorkflow(string task) => $"""
        1. `revit_list_types(kind="mep_system")` — discover system types.
        2. Read `revit://toolset/patterns/mep` — placement conventions.
        3. `revit_list_mep_systems` — confirm existing systems.
        4. Place elements: `revit_place_duct` / `revit_place_pipe` / `revit_place_conduit` for: {task}
        5. `revit_insulate_duct_system` — if insulation is required.
        6. `revit_list_mep_systems` — verify connectivity post-placement.
        """;

    private static string BuildDocumentationWorkflow(string task) => $"""
        1. `revit_list_views` — inventory existing views and sheets.
        2. Read `revit://toolset/patterns/documentation` — sheet package pattern.
        3. `revit_create_view` — create sections/elevations/plans as needed for: {task}
        4. `revit_create_sheet` — create target sheets.
        5. `revit_place_on_sheet` — place views on sheets.
        6. `revit_apply_view_template` — standardize appearance.
        7. Optional: `revit_create_schedule` + `revit_place_on_sheet` for tabular data.
        8. `revit_export_pdf` — deliver documentation package.
        """;

    private static string BuildExportWorkflow(string task) => $"""
        1. `revit_get_status` — confirm document path and active view.
        2. Read `revit://toolset/patterns/export` — path conventions and options.
        3. `revit_find_elements` — collect data set for: {task}
        4. Export via appropriate tool:
           - Tabular data → `revit_export_to_excel`
           - Schedule → `revit_export_schedule`
           - Views → `revit_export_pdf` or `revit_export_image`
        5. Return file path only — do not inline large datasets.
        """;

    private static string BuildVisualizationWorkflow(string task) => $"""
        1. `revit_find_elements` — identify elements to visualize for: {task}
        2. `revit_list_category_parameters` — find the driving parameter.
        3. `revit_color_by_parameter` — apply color scheme by parameter value.
           - Or `revit_override_colors` for explicit per-element colors.
        4. `revit_activate_view` — switch to target 3D/plan view.
        5. `revit_highlight_elements` — zoom to elements in UI.
        6. `revit://view/screenshot` — capture result for engineer.
        7. Cleanup: `revit_clear_overrides` when review is done.
        """;

    private static (string Verdict, string Approach, string Reasoning) EvaluateGodToolNeed(string task)
    {
        var t = task.ToLowerInvariant();

        string[] nonGoals =
        [
            "wall", "floor", "roof", "ceiling", "stair", "beam", "column", "foundation",
            "rebar", "curtain wall", "ifc", "in-place", "detail item", "dimension",
            "filled region", "structural", "analytical", "energy analysis", "link attach",
        ];

        foreach (var ng in nonGoals)
        {
            if (t.Contains(ng))
                return (
                    "Use god tool",
                    "execute_csharp_code",
                    $"Task mentions '{ng}' which is in the toolset non-goals list. Custom code is required.");
        }

        if (t.Contains("clash") || t.Contains("interference") || t.Contains("intersect"))
            return (
                "Hybrid: god tool + toolset",
                "execute_csharp_code for detection, toolset for highlight/export",
                "Clash detection requires custom geometry logic. Use toolset for element discovery and visualization.");

        string[] toolsetSignals =
        [
            "find", "search", "list", "parameter", "export", "sheet", "view", "duct", "pipe",
            "place", "move", "rotate", "delete", "color", "schedule", "room", "tag", "sync",
        ];

        foreach (var signal in toolsetSignals)
        {
            if (t.Contains(signal))
                return (
                    "Use toolset",
                    "revit_* tools",
                    $"Task matches toolset domain signal '{signal}'. Structured tools provide safer batch ops and per-call undo.");
        }

        return (
            "Likely god tool",
            "execute_csharp_code",
            "No clear toolset signal detected. Read revit://toolset/capabilities to confirm before writing custom code.");
    }
}
