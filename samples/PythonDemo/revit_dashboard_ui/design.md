This specification treats the dashboard as a UI layer over the open Revit model. It reads elements, parameters, warnings, and views that already exist in the `.rvt` file. It does not invent a second model or run analysis outside Revit.

# Product specification: Revit Model Insight Dashboard

**Concept:** Revit UI 2.0. Turn the raw contents of the open file into something a manager can scan and act on.

**Tech stack:** React, TypeScript, Semi Design, Vite+, Apache ECharts. Icons come from `lucide-react`.

**Scope:** The open Revit document: elements, parameters, warnings, views, and sheets.

## 1. Project Pulse

The home screen. A manager should see the size and condition of the current file without opening a schedule.

### 1.1 Big-number cards

Show the few counts that describe the file:

- Total elements
- Total warnings
- Loaded families
- Views and sheets
- Categories
- Levels

The number is the content. Use a Semi `Card` with large `Typography` (48px or larger). A sparkline under the number shows how that count moved across earlier sessions of this dashboard, when session history exists.

| Card           | Lucide icon     | Why this icon                              |
| -------------- | --------------- | ------------------------------------------ |
| Total elements | `Cuboid`        | A placed 3D element, not a layout grid.    |
| Warnings       | `TriangleAlert` | The same mark Revit uses for a warning.    |
| Families       | `Component`     | A family is a reusable component.          |
| Views / sheets | `AppWindow`     | A view is a window onto the model.         |
| Categories     | `Shapes`        | Distinct kinds sitting next to each other. |
| Levels         | `Layers`        | Levels stack through the building.         |

### 1.2 Category distribution

A donut of the main categories (for example walls, floors, windows). It answers whether the model is mostly structure, architecture, or furniture.

- Hover shows the absolute count and the share.
- Click filters the rest of the dashboard to that category.
- Click the same slice again to clear the filter.

## 2. Visual inventory

Replace the tree-shaped Project Browser with a picture of what is actually placed.

### 2.1 Spatial treemap

Nested rectangles:

- Outer cell: level
- Inner cell: category
- Area: instance count

Click a "Level 1 / Walls" cell and Revit isolates that category on that level in the active view.

### 2.2 Family and type explorer

A list of families in use, with the instance count beside each name, sorted from most used to least used. The point is to spot the wrong family or a family that should not be loaded.

## 3. Model health

The Revit Warnings dialog, sorted so the urgent items are obvious.

### 3.1 Warning severity

Group each warning into one of three bands:

- Critical: duplicates, rooms that are not enclosed, elements slightly off axis.
- Moderate: tags that miss their target, failed joins.
- Info: everything else.

"Show isolated" creates a view that contains only the elements in the chosen band.

### 3.2 Heavy elements

A short ranked list of the families with the most instances. The point is to see which families dominate the file.

## 4. Smart schedule

A Revit schedule with an Excel-like grid.

### 4.1 Interactive data grid

A Semi `Table` of the parameters for the current rows.

- The header stays visible while the body scrolls.
- Grouping follows the active group field, such as level or category.
- Search matches any text cell. It is the fast path compared with Revit's Find.

### 4.2 Parameter auditor

Empty required values (`Mark`, `Type Mark`, `Comments`, and any other empty text cell) use a light danger background so a missing value is visible without reading every cell.

## 5. Interaction with Revit

### 5.1 Ghost selection

When the user selects an element in the Revit canvas, the dashboard opens that element's detail: id, level, and the parameters on the element. The user does not search for the element they already picked.

### 5.2 Bulk selection

1. Filter on the dashboard, for example every door whose width is under 900 mm.
2. The page states how many elements matched.
3. "Select in Revit" sends those ids to the Revit selection so the user can delete or change type in one step.

### 5.3 Export

"Export" opens the dashboard window's Save dialog and writes the rows currently on screen to `.xlsx`. It does not download through the browser and it does not open Revit's schedule-export dialog.

## 6. Layout

1. **Sidebar, left.** Semi `Layout.Sider` and `Nav`, collapsed to an icon rail. The collapse control is Nav's `collapseButton`.
2. **Top bar.** Semi `Layout.Header` with `Breadcrumb`: model name, page, current view. Actions: refresh the snapshot, export, and switch light or dark.
3. **Main area.** Semi `Layout.Content`. Cards use `Row` and `Col`. Charts and the schedule table sit in that area.
4. **Properties, right.** Semi `SideSheet` with `mask={false}`, so the page stays usable. It opens when an element is selected. Parameter groups use `Collapse` and `Descriptions`.

### Icons

Use one Lucide icon per action. Pick the symbol for the Revit noun, not a generic grid, shield, or list.

| Place              | Action              | Lucide icon            |
| ------------------ | ------------------- | ---------------------- |
| Sidebar            | Project Pulse       | `Gauge`                |
| Sidebar            | Inventory           | `Boxes`                |
| Sidebar            | Model Health        | `HeartPulse`           |
| Sidebar            | Schedule            | `Table2`               |
| Sidebar            | Settings            | `Settings`             |
| Sidebar            | Collapse / expand   | Nav `collapseButton`   |
| Top bar            | Refresh snapshot    | `RefreshCw`            |
| Top bar            | Export workbook     | `FileDown`             |
| Top bar            | Light theme         | `Sun`                  |
| Top bar            | Dark theme          | `Moon`                 |
| Search fields      | Find text           | `Search`               |
| Schedule           | Column filter       | `Filter`               |
| Schedule           | Clear filters       | `X`                    |
| Schedule or health | Isolate in Revit    | `Eye`                  |
| Schedule           | Hide from the grid  | `EyeOff`               |
| Properties         | Close               | SideSheet close button |
| Properties         | Copy a value        | `Copy`                 |
| Properties         | Read-only parameter | `Lock`                 |
| Failure            | Uncaught UI error   | `TriangleAlert`        |
