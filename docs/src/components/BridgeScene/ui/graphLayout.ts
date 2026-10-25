import type {StackDefinition} from '../types';

export const GRAPH = {
  viewBox: '-30 0 380 240',
  centerX: 160,
  centerY: 120,
  radiusX: 84,
  radiusY: 78,
  coreRadius: 26,
  nodeRadius: 6,
  iconSize: 28,
  labelGap: 11,
  labelRise: 12,
  labelDrop: 18,
  sideThreshold: 0.35,
} as const;

export interface GraphNode {
  readonly x: number;
  readonly y: number;
  readonly cos: number;
  readonly sin: number;
}

export interface GraphRelation {
  readonly a: number;
  readonly b: number;
  readonly path: string;
}

export interface GraphLayout {
  readonly nodes: readonly GraphNode[];
  readonly relations: readonly GraphRelation[];
  readonly neighbours: readonly ReadonlySet<number>[];
}

export type LabelAnchor = 'start' | 'middle' | 'end';

export interface LabelPlacement {
  readonly anchor: LabelAnchor;
  readonly dx: number;
  readonly dy: number;
}

const fixed = (value: number): string => value.toFixed(1);

export function computeGraphLayout(stack: StackDefinition): GraphLayout {
  const {centerX, centerY, radiusX, radiusY} = GRAPH;
  const count = stack.docs.length;
  const nodes = stack.docs.map((_, k) => {
    const angle = -Math.PI / 2 + (k * 2 * Math.PI) / count;
    return {x: centerX + Math.cos(angle) * radiusX, y: centerY + Math.sin(angle) * radiusY, cos: Math.cos(angle), sin: Math.sin(angle)};
  });
  const relations = stack.related.map(([a, b]) => {
    const from = nodes[a];
    const to = nodes[b];
    if (!from || !to) return {a, b, path: ''};
    const midX = (from.x + to.x) / 2;
    const midY = (from.y + to.y) / 2;
    const controlX = midX + (centerX - midX) * 0.5;
    const controlY = midY + (centerY - midY) * 0.5;
    return {a, b, path: `M${fixed(from.x)},${fixed(from.y)} Q${fixed(controlX)},${fixed(controlY)} ${fixed(to.x)},${fixed(to.y)}`};
  });
  const neighbours = stack.docs.map((_, k) => {
    const set = new Set<number>();
    stack.related.forEach(([a, b]) => {
      if (a === k) set.add(b);
      if (b === k) set.add(a);
    });
    return set;
  });
  return {nodes, relations, neighbours};
}

export function placeLabel(node: GraphNode): LabelPlacement {
  const {sideThreshold, labelGap, labelRise, labelDrop} = GRAPH;
  if (node.cos > sideThreshold) return {anchor: 'start', dx: labelGap, dy: 4};
  if (node.cos < -sideThreshold) return {anchor: 'end', dx: -labelGap, dy: 4};
  return {anchor: 'middle', dx: 0, dy: node.sin < 0 ? -labelRise : labelDrop};
}
