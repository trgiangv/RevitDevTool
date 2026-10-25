import {MarkerType, type Edge, type Node} from '@xyflow/react';

export type Participant = {id: string; title: string; subtitle?: string};
export type Message = {from: string; to: string; text: string; detail?: string; back?: boolean};
export type Note = {note: string; over: string | string[]; detail?: string};
export type Step = Message | Note;

export type LaneData = {title: string; subtitle?: string; height: number; rows: number; active: boolean};
export type NoteData = {text: string; width: number};
export type ColumnOf = (id: string) => number;

export const laneWidth = 190;
export const laneGap = 320;
export const headerHeight = 70;
export const rowHeight = 66;
export const noteHeight = 34;
const bottomPad = 16;
const noteInset = 30;

export const palette = {
  light: {accent: '#0a6f7a', ink: '#11201f', surface: '#ecf1f0'},
  dark: {accent: '#5cc7d3', ink: '#e4eeed', surface: '#162020'},
};

export type Palette = typeof palette.light;

export function isNote(step: Step): step is Note {
  return 'note' in step;
}

export function rowCenter(index: number): number {
  return headerHeight + index * rowHeight + rowHeight / 2;
}

export function diagramSize(participantCount: number, stepCount: number): {width: number; height: number} {
  return {
    width: (participantCount - 1) * laneGap + laneWidth,
    height: headerHeight + stepCount * rowHeight + bottomPad,
  };
}

export function describeStep(step: Step | undefined): string {
  if (!step) return '';
  return step.detail ?? (isNote(step) ? step.note : step.text);
}

interface GraphInput {
  readonly participants: readonly Participant[];
  readonly steps: readonly Step[];
  readonly revealed: number;
  readonly column: ColumnOf;
}

interface NodeInput extends GraphInput {
  readonly height: number;
  readonly activeLane: number;
}

interface EdgeInput extends GraphInput {
  readonly currentIndex: number;
  readonly colors: Palette;
}

function laneNode(participant: Participant, index: number, input: NodeInput): Node {
  return {
    id: participant.id,
    type: 'lane',
    position: {x: index * laneGap, y: 0},
    data: {
      title: participant.title,
      subtitle: participant.subtitle,
      height: input.height,
      rows: input.steps.length,
      active: index === input.activeLane,
    },
    draggable: false,
    selectable: false,
    style: {width: laneWidth},
  };
}

function noteNode(step: Note, index: number, column: ColumnOf): Node {
  const columns = (Array.isArray(step.over) ? step.over : [step.over]).map(column);
  const first = Math.min(...columns);
  const last = Math.max(...columns);
  return {
    id: `note-${index}`,
    type: 'note',
    position: {x: first * laneGap + laneWidth / 2 - noteInset, y: rowCenter(index) - noteHeight / 2},
    data: {text: step.note, width: (last - first) * laneGap + 2 * noteInset},
    draggable: false,
    selectable: false,
    zIndex: 5,
  };
}

export function buildNodes(input: NodeInput): Node[] {
  const lanes = input.participants.map((participant, index) => laneNode(participant, index, input));
  const notes = input.steps.flatMap((step, index) => (isNote(step) && index < input.revealed ? [noteNode(step, index, input.column)] : []));
  return [...lanes, ...notes];
}

function messageEdge(step: Message, index: number, input: EdgeInput): Edge {
  const {colors, currentIndex, participants, column} = input;
  const isCurrent = index === currentIndex;
  return {
    id: `step-${index}`,
    source: participants[column(step.from)].id,
    target: participants[column(step.to)].id,
    sourceHandle: `s${index}`,
    targetHandle: `t${index}`,
    type: 'straight',
    animated: isCurrent,
    label: step.text,
    labelStyle: {fill: colors.ink, fontWeight: 600, fontSize: 14},
    labelBgStyle: {fill: colors.surface, fillOpacity: 1},
    labelBgPadding: [8, 4] as [number, number],
    labelBgBorderRadius: 4,
    style: {stroke: colors.accent, strokeWidth: isCurrent ? 2.6 : 2, strokeDasharray: step.back ? '7 6' : undefined},
    markerEnd: {type: MarkerType.ArrowClosed, color: colors.accent, width: 18, height: 18},
  };
}

export function buildEdges(input: EdgeInput): Edge[] {
  return input.steps.flatMap((step, index) => (!isNote(step) && index < input.revealed ? [messageEdge(step, index, input)] : []));
}
