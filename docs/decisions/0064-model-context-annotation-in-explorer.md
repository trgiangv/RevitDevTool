# 0064 Model Context: Annotation Belongs in Explorer

Date: 2026-11-26

## Status

**Accepted** (2026-11-26). Extends [0062](0062-model-context-panel.md) and
[0063](0063-model-context-ux-and-agent-foundation.md). The
[plan](../plans/completed/2026-11-26-models-context-annotation.md) passed its
live runs on the deployed add-in, Revit 2026: HVAC L2 0 fail and L3 28/28
(run `20261126-202825`), panel = oracle on サンプル意匠 and Golden Nugget
Architektur in a plan, section, legend and sheet, and a snapshot of the
heaviest sample in about 105 ms with no call over 50 ms.

## Context

1. Explorer lists only categories whose `CategoryType` is Model
   (`ElementReader.ModelCategoryIds`). The rule follows Revit's internal type,
   not what the user works with. Detail Items, Filled Regions and Detail Lines
   are 2D but count, because Revit files them as Model. Tags, Text Notes,
   Dimensions, Grids, Levels, Title Blocks and Viewports are left out.
2. Isolate already hides annotation categories, so the panel acts on things
   it never lists.
3. Measured on `Snowdon Towers Sample HVAC.rvt`, Revit 2026, 2026-11-26
   (full numbers in the plan):
   - Plan view L3: 123 of 1,048 elements are annotation (Duct Tags 73,
     Space Tags 17, Grids 15). Explorer shows none of them.
   - Cover Sheet: 10 of 23 are annotation (Title Block, Viewports, Text Notes).
   - Drafting view: 7 of 30.
   - Every tag, text note, title block and viewport is view-owned. Grids,
     Levels, Scope Boxes, Elevation markers and most Reference Planes are not.
4. Revit has no lighter way to select annotation by type in a view: Find and
   Replace cannot select text, and there is no "select all revision clouds".
   Ideate sells a separate product, Ideate Annotate, whose Browse tab lists
   annotation per project, view or sheet. The need is real.
5. `Category.IsVisibleInUI` separates what Visibility/Graphics shows from what
   it does not. On HVAC the hidden set is settings data (Phases, Revisions,
   Load Classifications, HVAC Load Schedules, Constraints, Color Fill Schema).
   Some categories Revit marks Internal are still visible and user-placed
   (Detail Groups). Some visible annotation is sketch scaffolding
   (Automatic Sketch Dimensions).

## Decision

1. **The rule is what the user can find in Visibility/Graphics.** Explorer
   lists Model and Annotation categories with `IsVisibleInUI`, plus Detail
   Groups. It never lists Automatic Sketch Dimensions, Section Boxes, Views,
   Schedules (the view element; Schedule Graphics on a sheet stay), Sheets and
   Project Information (views and settings, not drawn elements), Work Plane
   Grid, Shared Site, or any category hidden from the UI. A built-in
   subcategory follows the same test, and the existing subcategory rules
   (Center line, Site, Lines, Topography) stay. Detail Groups are not in the
   document's category list, so they are added by id.
2. **The scope follows the element, not its category.** An element that
   belongs to one view (an owner view: tags, text, dimensions, title blocks,
   viewports, detail items, detail lines, filled regions, raster images,
   detail groups) counts in Active view only, and only in its own view. An
   element with no owner view counts in both scopes, whatever its category
   (walls, ducts, grids, levels, reference planes, scope boxes, elevation
   markers). Whole model therefore drops the view-owned Detail Items and
   Detail Lines it lists today. The snapshot reads every element once,
   view-owned included, without opening any view (the owner view is stored
   on the element); scopes cut by owner view.
3. **Current model only, for now.** Link annotation is never drawn in a host
   view, and linked datums follow the link's display settings, so links keep
   listing Model categories, elements with no owner view only (Snowdon
   Architectural would add 1,046 annotation elements, 921 of them Reference
   Planes). Linked datums are a follow-up.
4. **Same tree, same actions.** Annotation uses Category › Family › Type ›
   Instance and the existing Select, Hide, Isolate, Halftone and Color. Color
   paints lines only; an annotation category that a parameter filter cannot
   take keeps the existing *not filterable* handling. No annotation-only
   controls.
5. **Out of scope.** Review checks (blank tags, zero-length dimensions,
   overlaps), tagging, and tag-to-host relations in the UI. Tag-to-host for
   the MCP tool is a follow-up under 0063 item 6.

## Alternatives Considered

1. **Keep Model only.** Drafting views, legends and sheets stay almost empty,
   and Isolate keeps acting on categories the user cannot see in the panel.
2. **Every category with elements.** Lists Phases, Revisions and load
   settings, which the user cannot place in a view.
3. **View-owned elements in Whole model too.** Thousands of tags and detail
   items from every view, none of which the user can act on from the active
   view.
4. **Split by category type** (annotation in Active view only, model
   categories in both). Detail Items are Model in Revit but belong to one
   view, and Grids are Annotation but model-wide; the category type does not
   say where an element lives.
5. **A separate annotation browser.** Two trees for one job; 0063 item 5 says
   one engine and one UI per job.

## Consequences

Positive:

- Drafting views, legends and sheets have content in Explorer.
- Agents get the annotation of the active view (counts, types, owners)
  through the same snapshot.
- One plain rule replaces the type check: if Visibility/Graphics shows it,
  Explorer can list it.

Tradeoffs:

- Whole model no longer equals "what a schedule lists": it adds Grids, Levels
  and the other model-wide annotation, and drops view-owned Detail Items,
  which a Detail Items schedule lists. The product doc must say so.
- An active view with many tags makes a bigger snapshot; per view, not per
  model, so the cost is bounded by the view.
- The two-scope rule (view-owned vs model-wide) is one more thing the user
  can notice; the count difference between scopes must read naturally. It is
  one rule for every category, so it reads the same everywhere.

## Follow-Up

- [Annotation plan](../plans/completed/2026-11-26-models-context-annotation.md).
- Linked datums (Grids, Levels) once By Linked View display is handled.
- Project-wide questions on view-owned elements ("every cloud of Revision 9
  and the view it sits in", "every text note containing X"). Revision clouds
  are view-owned (サンプル意匠: 5 clouds of Rev 9 on 2 floor and 3 ceiling
  plans; Snowdon Architectural: 2 clouds on sheets), so Decision 2 keeps them
  out of Whole model. The snapshot already holds them; the MCP tool answers
  first. A Whole model listing grouped by owner view is a separate decision,
  for every view-owned category at once, not an exception for clouds.
- Tag-to-host for the MCP filter tool.
