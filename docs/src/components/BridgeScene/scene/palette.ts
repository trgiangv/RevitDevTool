import {STACKS} from '../data/stacks';

export interface ScenePalette {
  readonly dark: boolean;
  readonly streams: readonly string[];
  readonly host: string;
  readonly highlight: string;
  readonly gate: string;
  readonly ok: string;
  readonly ai: string;
  readonly edge: string;
  readonly baseAlpha: number;
  readonly pointSize: number;
}

function readColor(name: string, fallback: string): string {
  if (typeof document === 'undefined') return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return /^#[0-9a-f]{6}$/i.test(value) ? value : fallback;
}

export function createPalette(dark: boolean): ScenePalette {
  if (dark) {
    return {
      dark,
      streams: STACKS.map((stack) => stack.colors.dark),
      host: '#2f7480',
      highlight: '#a6eef7',
      gate: readColor('--rdt-accent', '#5cc7d3'),
      ok: '#6fd49a',
      ai: '#b79cff',
      edge: '#5b8cff',
      baseAlpha: 0.3,
      pointSize: 2.1,
    };
  }
  return {
    dark,
    streams: STACKS.map((stack) => stack.colors.light),
    host: '#4d7479',
    highlight: '#0a6f7a',
    gate: readColor('--rdt-accent', '#0a6f7a'),
    ok: '#1d7348',
    ai: '#6a4fd0',
    edge: '#1e51ff',
    baseAlpha: 0.4,
    pointSize: 2.6,
  };
}
