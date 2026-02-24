/**
 * Deterministic color palette for group color assignment.
 * 12 visually distinct colors that work on both light and dark backgrounds.
 */

const PALETTE: [number, number, number][] = [
  [66, 133, 244],   // blue
  [234, 67, 53],    // red
  [52, 168, 83],    // green
  [251, 188, 4],    // yellow
  [171, 71, 188],   // purple
  [255, 112, 67],   // orange
  [0, 172, 193],    // cyan
  [124, 179, 66],   // lime
  [233, 30, 99],    // pink
  [63, 81, 181],    // indigo
  [255, 167, 38],   // amber
  [0, 150, 136],    // teal
]

export interface ColorGroup {
  label: string
  ids: number[]
  color: [number, number, number]
}

/**
 * Assign deterministic colors to a map of group-label -> element-ids.
 * Colors cycle through PALETTE when there are more groups than colors.
 */
export function assignGroupColors(
  groups: Map<string, number[]>,
): ColorGroup[] {
  return Array.from(groups.entries()).map(([label, ids], i) => ({
    label,
    ids,
    color: PALETTE[i % PALETTE.length],
  }))
}

/** Get a CSS rgba string for a palette color at reduced opacity. */
export function colorToRgba(color: [number, number, number], alpha = 1): string {
  return `rgba(${color[0]}, ${color[1]}, ${color[2]}, ${alpha})`
}

export { PALETTE }
