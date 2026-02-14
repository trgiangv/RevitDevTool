/**
 * Mock data generator for browser development.
 *
 * Generates realistic BIM element data for testing the dashboard
 * without needing Revit connection.
 */

import type { DashboardPayload, ElementRow, HeavyFamily, ModelInfo, WarningItem } from "@/types"

// Sample data pools
const CATEGORIES = [
  "Walls", "Floors", "Roofs", "Ceilings", "Doors", "Windows",
  "Structural Columns", "Structural Framing", "Furniture", "Plumbing Fixtures",
  "Mechanical Equipment", "Electrical Fixtures", "Casework", "Stairs", "Railings",
]

const FAMILIES: Record<string, string[]> = {
  "Walls": ["Basic Wall", "Curtain Wall", "Stacked Wall", "Interior Wall"],
  "Floors": ["Floor", "Structural Floor", "Composite Floor"],
  "Roofs": ["Basic Roof", "Sloped Glazing", "Metal Roof"],
  "Ceilings": ["Compound Ceiling", "Basic Ceiling", "Acoustic Tile"],
  "Doors": ["Single-Flush", "Double-Flush", "Sliding", "Bifold", "Revolving"],
  "Windows": ["Fixed", "Casement", "Awning", "Double Hung", "Sliding"],
  "Structural Columns": ["Concrete-Round", "Concrete-Rectangular", "Steel-Wide Flange"],
  "Structural Framing": ["W-Wide Flange", "HSS-Hollow Structural", "Concrete Beam"],
  "Furniture": ["Desk", "Chair", "Table", "Sofa", "Bookshelf", "Cabinet"],
  "Plumbing Fixtures": ["Toilet", "Sink", "Urinal", "Shower", "Bathtub"],
  "Mechanical Equipment": ["Air Handler", "Chiller", "Boiler", "Fan Coil Unit"],
  "Electrical Fixtures": ["Light Fixture", "Receptacle", "Switch", "Panel"],
  "Casework": ["Base Cabinet", "Upper Cabinet", "Tall Cabinet", "Countertop"],
  "Stairs": ["Assembled Stair", "Cast-In-Place Stair", "Precast Stair"],
  "Railings": ["Handrail", "Guard Rail", "Glass Railing"],
}

const TYPES: Record<string, string[]> = {
  "Basic Wall": ["Generic - 200mm", "Generic - 300mm", "Brick on CMU", "Exterior - Brick"],
  "Curtain Wall": ["Curtain Wall 1", "Storefront"],
  "Floor": ["Generic 150mm", "Generic 200mm", "Concrete 250mm"],
  "Single-Flush": ["0915 x 2134mm", "0864 x 2134mm", "0762 x 2134mm"],
  "Fixed": ["0915 x 1220mm", "1200 x 1500mm", "600 x 900mm"],
  "Desk": ["1500 x 750mm", "1800 x 900mm", "1200 x 600mm"],
  "Chair": ["Office Chair", "Task Chair", "Conference Chair"],
  "Light Fixture": ["2x4 Troffer", "2x2 Troffer", "Downlight", "Pendant"],
}

const LEVELS = [
  "Level B2", "Level B1", "Level 00", "Level 01", "Level 02",
  "Level 03", "Level 04", "Level 05", "Level 06", "Roof",
]

const PHASES = ["Existing", "New Construction", "Demolition"]
const WORKSETS = ["Shared Levels and Grids", "Workset1", "Architecture", "Structure", "MEP"]

// Warning templates
const WARNING_TEMPLATES: Array<{ description: string; severity: WarningItem["severity"]; category: string }> = [
  { description: "Elements are slightly off axis and may cause inaccuracies", severity: "critical", category: "Geometry" },
  { description: "Room is not enclosed. Room area may not be accurate", severity: "critical", category: "Rooms" },
  { description: "Duplicate instances found in the same location", severity: "critical", category: "Duplicates" },
  { description: "There are identical instances in the same place", severity: "critical", category: "Duplicates" },
  { description: "One element is completely inside another", severity: "critical", category: "Geometry" },
  { description: "Tag is not attached to any element", severity: "moderate", category: "Annotations" },
  { description: "Elements have duplicate Mark values", severity: "moderate", category: "Parameters" },
  { description: "Highlighted walls are attached to, but miss, the target", severity: "moderate", category: "Joins" },
  { description: "Wall join is not clean. Use 'Edit Profile' to fix", severity: "moderate", category: "Joins" },
  { description: "Multiple rooms are not placed in the model", severity: "moderate", category: "Rooms" },
  { description: "Room tag is outside of its room boundary", severity: "info", category: "Annotations" },
  { description: "Level is slightly off axis", severity: "info", category: "Geometry" },
  { description: "Analytical model and physical model are inconsistent", severity: "info", category: "Structure" },
  { description: "This floor/roof has a slope but the slope arrow was not applied", severity: "info", category: "Geometry" },
]

// Helper functions
function randomFrom<T>(arr: T[]): T {
  return arr[Math.floor(Math.random() * arr.length)]
}

function randomInt(min: number, max: number): number {
  return Math.floor(Math.random() * (max - min + 1)) + min
}

function generateElementId(): number {
  return randomInt(100000, 9999999)
}

function generateUniqueId(): string {
  return `${crypto.randomUUID?.() ?? Math.random().toString(36).slice(2)}`
}

// Generate a single element row
function generateElement(id: number): ElementRow {
  const category = randomFrom(CATEGORIES)
  const familyOptions = FAMILIES[category] ?? ["Generic"]
  const family = randomFrom(familyOptions)
  const typeOptions = TYPES[family] ?? [`${family} - Standard`]
  const type = randomFrom(typeOptions)

  return {
    element_id: id,
    unique_id: generateUniqueId(),
    name: `${family} - ${type}`,
    class_name: category.replace(/\s+/g, ""),
    category,
    family,
    type,
    level: randomFrom(LEVELS),
    phase: randomFrom(PHASES),
    workset: randomFrom(WORKSETS),
    is_view_specific: Math.random() < 0.1,
    is_pinned: Math.random() < 0.15,
    has_material_quantities: Math.random() < 0.7,
  }
}

// Generate multiple elements with realistic distribution
function generateElements(count: number): ElementRow[] {
  const elements: ElementRow[] = []
  const usedIds = new Set<number>()

  for (let i = 0; i < count; i++) {
    let id: number
    do {
      id = generateElementId()
    } while (usedIds.has(id))
    usedIds.add(id)
    elements.push(generateElement(id))
  }

  return elements
}

// Build filter options from elements
function buildFilterOptions(elements: ElementRow[]): Record<string, string[]> {
  const options: Record<string, Set<string>> = {
    category: new Set(),
    family: new Set(),
    type: new Set(),
    level: new Set(),
    phase: new Set(),
    workset: new Set(),
  }

  for (const el of elements) {
    options.category.add(el.category)
    options.family.add(el.family)
    options.type.add(el.type)
    options.level.add(el.level)
    options.phase.add(el.phase)
    options.workset.add(el.workset)
  }

  return Object.fromEntries(
    Object.entries(options).map(([k, v]) => [k, Array.from(v).sort()]),
  )
}

// Build chart data
function buildChartData(elements: ElementRow[]): Record<string, Record<string, unknown>[]> {
  const countBy = (key: keyof ElementRow) => {
    const counts = new Map<string, number>()
    for (const el of elements) {
      const val = String(el[key])
      counts.set(val, (counts.get(val) ?? 0) + 1)
    }
    return Array.from(counts.entries())
      .map(([k, v]) => ({ [key]: k, count: v }))
      .sort((a, b) => b.count - a.count)
  }

  return {
    category_counts: countBy("category"),
    level_counts: countBy("level"),
    family_counts: countBy("family").slice(0, 50),
    workset_counts: countBy("workset"),
    phase_counts: countBy("phase"),
    quality: [
      { metric: "missing_category", count: elements.filter(e => e.category === "<No Category>").length },
      { metric: "missing_family", count: elements.filter(e => e.family === "<No Family>").length },
      { metric: "missing_level", count: elements.filter(e => e.level === "<No Level>").length },
    ],
  }
}

// Build KPIs
function buildKpis(elements: ElementRow[], warnings: WarningItem[], modelInfo: ModelInfo): Record<string, number> {
  return {
    total_elements: elements.length,
    total_warnings: warnings.length,
    unique_categories: new Set(elements.map(e => e.category)).size,
    unique_families: new Set(elements.map(e => e.family)).size,
    unique_types: new Set(elements.map(e => e.type)).size,
    unique_levels: new Set(elements.map(e => e.level)).size,
    total_views: modelInfo.total_views,
    total_sheets: modelInfo.total_sheets,
    pinned_elements: elements.filter(e => e.is_pinned).length,
    view_specific_elements: elements.filter(e => e.is_view_specific).length,
  }
}

// Generate mock warnings
function generateWarnings(elements: ElementRow[]): WarningItem[] {
  const warningCount = randomInt(15, 60)
  const warnings: WarningItem[] = []

  for (let i = 0; i < warningCount; i++) {
    const template = randomFrom(WARNING_TEMPLATES)
    const affectedCount = randomInt(1, 4)
    const affectedElements = Array.from({ length: affectedCount }, () => randomFrom(elements).element_id)

    warnings.push({
      id: 1000 + i,
      description: template.description,
      severity: template.severity,
      element_ids: affectedElements,
      category: template.category,
    })
  }

  return warnings
}

// Generate mock heavy families
function generateHeavyFamilies(elements: ElementRow[]): HeavyFamily[] {
  const familyCounts = new Map<string, { category: string; count: number; types: Set<string> }>()

  for (const el of elements) {
    const existing = familyCounts.get(el.family)
    if (existing) {
      existing.count++
      existing.types.add(el.type)
    } else {
      familyCounts.set(el.family, { category: el.category, count: 1, types: new Set([el.type]) })
    }
  }

  return Array.from(familyCounts.entries())
    .map(([name, data]) => ({
      family_name: name,
      category: data.category,
      instance_count: data.count,
      type_count: data.types.size,
      estimated_complexity: randomInt(200, 5000),
    }))
    .sort((a, b) => b.estimated_complexity - a.estimated_complexity)
    .slice(0, 10)
}

// Generate mock model info
function generateModelInfo(): ModelInfo {
  return {
    file_name: "Office_Building_2025.rvt",
    file_path: "C:\\Projects\\Office Building\\Office_Building_2025.rvt",
    current_view: "3D View: {3D}",
    total_views: randomInt(80, 250),
    total_sheets: randomInt(20, 80),
  }
}

/**
 * Generate a complete mock dashboard payload.
 * @param elementCount Number of elements to generate (default: 2500)
 */
export function generateMockPayload(elementCount = 2500): DashboardPayload {
  const elements = generateElements(elementCount)
  const filterOptions = buildFilterOptions(elements)
  const modelInfo = generateModelInfo()
  const warnings = generateWarnings(elements)
  const heavyFamilies = generateHeavyFamilies(elements)

  return {
    schema_version: "1.1.0",
    generated_at_utc: new Date().toISOString(),
    model_info: modelInfo,
    kpis: buildKpis(elements, warnings, modelInfo),
    filter_options: filterOptions,
    filterable_columns: ["category", "family", "type", "level", "phase", "workset"],
    chart_configs: [
      {
        type: "bar",
        title: "Elements by Category",
        data_key: "category_counts",
        label_field: "category",
        value_field: "count",
        max_items: 15,
        click_filter_field: "category",
      },
      {
        type: "bar",
        title: "Elements by Level",
        data_key: "level_counts",
        label_field: "level",
        value_field: "count",
        max_items: 15,
        click_filter_field: "level",
      },
    ],
    charts: buildChartData(elements),
    rows: elements,
    columns: [
      "element_id", "unique_id", "name", "class_name", "category",
      "family", "type", "level", "phase", "workset",
      "is_view_specific", "is_pinned", "has_material_quantities",
    ],
    warnings,
    heavy_families: heavyFamilies,
  }
}

/**
 * Check if running in browser (not in Revit WebView2)
 */
export function isBrowserMode(): boolean {
  return !window.chrome?.webview && !window.__BIM_DASHBOARD_INITIAL_DATA
}
