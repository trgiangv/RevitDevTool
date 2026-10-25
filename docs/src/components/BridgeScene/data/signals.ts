import {LOOP_SECONDS, PREVIEW} from '../config';
import type {Beat, Flash, LaneDefinition, SignalKind, SignalPill} from '../types';
import {HOSTS} from './hosts';

export const BEATS: readonly Beat[] = [
  {start: 0, text: 'Your code runs inside live Revit, AutoCAD, Civil 3D and Plant 3D sessions'},
  {start: 6.6, text: 'Tests and logs stream back from the same hosts'},
  {start: 9.8, text: 'AI assistants connect over MCP, here to AutoCAD'},
  {start: 12.6, text: 'Geometry preview, on Revit today'},
];

const PILL_SECONDS = 2.4;
const SLOT_SECONDS = 0.8;
const FIRST_SLOT = 3.2;
const PILL_OFFSET_Y = 0.15;
const FLASH_WIDTH = 0.35;
const TAGS: Readonly<Record<SignalKind, string>> = {
  inf: 'INF',
  dbg: 'DBG',
  wrn: 'WRN',
  err: 'ERR',
  ok: '✓',
  fail: '✗',
  cancel: 'CANCEL',
  ai: 'AI',
};

type LogEntry = readonly [SignalKind, string];
type TimeWindow = readonly [number, number];

function overlaps(start: number, end: number, window: TimeWindow | undefined): boolean {
  return window !== undefined && start < window[1] && end > window[0];
}

function hostLogs(hostIndex: number, entries: readonly LogEntry[], quiet?: TimeWindow): SignalPill[] {
  const pills: SignalPill[] = [];
  entries.forEach(([kind, text], i) => {
    const start = FIRST_SLOT + (i * HOSTS.length + hostIndex) * SLOT_SECONDS;
    const end = start + PILL_SECONDS;
    if (end > LOOP_SECONDS || overlaps(start, end, quiet)) return;
    pills.push({
      kind,
      tag: TAGS[kind],
      text,
      start,
      duration: PILL_SECONDS,
      hostIndex,
      yOffset: PILL_OFFSET_Y,
      outbound: true,
    });
  });
  return pills;
}

const REVIT_LOGS: readonly LogEntry[] = [
  ['inf', 'Collected 14 elements'], ['wrn', 'Family not loaded'], ['cancel', 'Export cancelled'], ['dbg', 'Done 0.4 s'],
];

const AUTOCAD_LOGS: readonly LogEntry[] = [
  ['inf', 'Polyline appended'], ['wrn', 'Unit mismatch'], ['dbg', 'Regen'], ['err', 'Entity not found'],
];

const CIVIL_LOGS: readonly LogEntry[] = [
  ['dbg', 'Profile cached'], ['ok', '12 passed'], ['fail', '6 failed'], ['inf', 'Done 0.8 s'],
];

const PLANT_LOGS: readonly LogEntry[] = [
  ['inf', 'Pipes: 128'], ['wrn', 'Parameter not set'], ['err', 'Element skipped'], ['cancel', 'Run cancelled'],
];

const AI_WINDOW: TimeWindow = [9.7, 12.9];
const PREVIEW_WINDOW: TimeWindow = [PREVIEW.start - 0.3, LOOP_SECONDS];

const AI_PILLS: readonly SignalPill[] = [
  {kind: 'ai', tag: 'AI', text: 'request', start: 9.9, duration: 1.4, hostIndex: 1, yOffset: 0, outbound: false},
  {kind: 'ai', tag: 'MCP', text: 'reply', start: 11.3, duration: 1.5, hostIndex: 1, yOffset: 0, outbound: true},
];

export const SIGNAL_PILLS: readonly SignalPill[] = [
  ...hostLogs(0, REVIT_LOGS, PREVIEW_WINDOW),
  ...hostLogs(1, AUTOCAD_LOGS, AI_WINDOW),
  ...hostLogs(2, CIVIL_LOGS),
  ...hostLogs(3, PLANT_LOGS),
  ...AI_PILLS,
];

export const LANES: readonly LaneDefinition[] = [
  {hostIndex: 0, direction: 1, tone: 'gate', activity: [3.2, 3.6, 12.2, 12.7]},
  {hostIndex: 1, direction: -1, tone: 'ai', activity: [9.8, 10.1, 11.1, 11.4]},
  {hostIndex: 1, direction: 1, tone: 'ai', activity: [11.3, 11.6, 12.4, 12.8]},
  {hostIndex: 2, direction: 1, tone: 'ok', activity: [4.2, 4.6, 14.6, 15.2]},
  {hostIndex: 3, direction: 1, tone: 'gate', activity: [3.6, 4, 14.4, 15]},
];

export const HOST_FLASHES: readonly (readonly Flash[])[] = HOSTS.map((_, hostIndex) =>
  SIGNAL_PILLS.filter((pill) => pill.hostIndex === hostIndex && pill.outbound).map((pill) => ({at: pill.start, width: FLASH_WIDTH})),
);

export const PREVIEW_HOST_INDEX = 0;
export const PREVIEW_PILL = {tag: 'Preview', text: 'Revit only'} as const;
