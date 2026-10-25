export const TAU = Math.PI * 2;

export const clamp = (value: number, min: number, max: number): number => Math.min(max, Math.max(min, value));
export const clamp01 = (value: number): number => clamp(value, 0, 1);
export const lerp = (from: number, to: number, amount: number): number => from + (to - from) * amount;

export const easeInOut = (p: number): number => (p < 0.5 ? 2 * p * p : 1 - Math.pow(-2 * p + 2, 2) / 2);

export function trapezoid(t: number, riseStart: number, riseEnd: number, fallStart: number, fallEnd: number): number {
  const rise = clamp01((t - riseStart) / Math.max(1e-4, riseEnd - riseStart));
  const fall = clamp01((t - fallStart) / Math.max(1e-4, fallEnd - fallStart));
  return rise * (1 - fall);
}

export const bump = (t: number, center: number, width: number): number => Math.exp(-Math.pow((t - center) / width, 2));

export const wrapAngle = (angle: number): number => angle - TAU * Math.round(angle / TAU);

export function createRandom(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = Math.trunc(state + 0x6d2b79f5);
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function smoothTo(current: number, target: number, rate: number): number {
  return current + (target - current) * rate;
}

export function hexToRgb(hex: string): [number, number, number] {
  const value = Number.parseInt(hex.slice(1), 16);
  return [((value >> 16) & 255) / 255, ((value >> 8) & 255) / 255, (value & 255) / 255];
}
