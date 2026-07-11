# Tool Error Codes & Recovery

Standard error codes returned in write-tool `failures[]` arrays. All write tools use partial-success reporting: `success_count` + `failures[]`.

## Error Envelope

```json
{
  "elementId": 12345,
  "code": "constraint_violation",
  "message": "Human-readable description",
  "recoverable": true,
  "suggestedAction": "release workset"
}
```

---

## Error Code Reference

### `constraint_violation`

**Meaning:** Revit rejected the change due to a model constraint (join, offset, hosted relationship, etc.).

**Example:** Moving a door breaks its host wall join.

**Recovery:**
1. Check element relationships with `revit_read_parameters`
2. Narrow scope to unaffected elements
3. Use `undo_changes` if partial corruption occurred
4. For complex constraints, use `execute_csharp_code`

---

### `element_borrowed`

**Meaning:** Element is borrowed by another user in a workshared model.

**Example:** Cannot write Mark on a door borrowed by another engineer.

**Recovery:**
1. Check `revit://model/worksets` for ownership
2. Skip borrowed elements; continue with owned elements
3. Ask engineer to release workset or sync
4. `suggestedAction`: **release workset**

---

### `element_pinned`

**Meaning:** Element is pinned and cannot be moved or modified.

**Example:** Cannot move a pinned grid or column.

**Recovery:**
1. Skip pinned elements in batch
2. Ask engineer to unpin if modification is intended
3. `suggestedAction`: **unpin element**

---

### `group_member`

**Meaning:** Element is part of a group; individual modification is restricted.

**Example:** Cannot change parameters on a grouped furniture instance.

**Recovery:**
1. Identify group membership via `revit_read_parameters`
2. Modify the group or ungroup first
3. `suggestedAction`: **ungroup or edit group**

---

### `param_readonly`

**Meaning:** Parameter is read-only (built-in computed, formula-driven, or locked).

**Example:** Cannot set "Area" on a room (computed value).

**Recovery:**
1. Use `revit_list_category_parameters` to find writable params
2. Remove readonly params from `updates` array
3. Check if a different param achieves the goal

---

### `type_mismatch`

**Meaning:** Value type incompatible with parameter storage type.

**Example:** Passing string "100" to a numeric param, or wrong element type for `revit_swap_type`.

**Recovery:**
1. Check param storage type via `revit_read_parameters`
2. Convert value to correct type (string, double, int, ElementId)
3. Verify type compatibility for `revit_swap_type`

---

### `not_found`

**Meaning:** Element, type, level, view, or parameter does not exist.

**Example:** Element ID 99999 not in document; level "Level 99" missing.

**Recovery:**
1. Re-query with `revit_find_elements` or live resources
2. Verify IDs from `revit://model/types`, `revit://model/levels`
3. Check for deleted or synced-away elements

---

## Recovery Workflow

```
Write tool returns failures[]
├─ All recoverable + specific elements?
│  └─ Retry with corrected scope (skip failed IDs)
├─ Worksharing conflict?
│  └─ Check revit://model/worksets, skip borrowed
├─ >50% failure rate?
│  └─ undo_changes → re-plan with revit_read_parameters sample
└─ Unknown error?
   └─ undo_changes → revit_get_status → escalate to execute_csharp_code
```

## Integration with `undo_changes`

Each write tool runs in a single transaction named `MCP: {tool_name}`. The built-in `undo_changes` tool reverts the last MCP transaction.

**Best practice:** On significant partial failure, call `undo_changes` before retrying with a corrected plan.

## Retry Guidance

| Code | Recoverable | Retry strategy |
|------|-------------|----------------|
| `constraint_violation` | Usually yes | Modify approach or skip element |
| `element_borrowed` | Yes | Skip until released |
| `element_pinned` | Yes | Skip or request unpin |
| `group_member` | Yes | Ungroup first |
| `param_readonly` | Yes | Remove from updates |
| `type_mismatch` | Yes | Fix value type |
| `not_found` | Yes | Refresh IDs from live resources |
