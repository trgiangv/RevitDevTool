import {Vector3, type PerspectiveCamera} from 'three';

import {HOST_CENTER_X, LAYOUT, PREVIEW} from '../config';
import {HOSTS} from '../data/hosts';
import {PREVIEW_HOST_INDEX, SIGNAL_PILLS} from '../data/signals';
import {STACKS} from '../data/stacks';
import type {FrameState} from './animator';
import {clamp01, easeInOut, lerp, trapezoid} from './math';

export const overlayKey = {
  stack: (index: number): string => `stack:${index}`,
  host: (index: number): string => `host:${index}`,
  pill: (index: number): string => `pill:${index}`,
  gate: 'gate',
  more: 'more',
  preview: 'preview',
} as const;

type RefCallback = (element: HTMLElement | null) => void;

export class OverlayRegistry {
  private readonly elements = new Map<string, HTMLElement>();
  private readonly callbacks = new Map<string, RefCallback>();

  ref(key: string): RefCallback {
    let callback = this.callbacks.get(key);
    if (!callback) {
      callback = (element) => {
        if (element) this.elements.set(key, element);
        else this.elements.delete(key);
      };
      this.callbacks.set(key, callback);
    }
    return callback;
  }

  get(key: string): HTMLElement | undefined {
    return this.elements.get(key);
  }
}

interface Viewport {
  readonly width: number;
  readonly height: number;
}

const CENTER = '-50%, -50%';
const LEFT_CENTER = '-100%, -50%';
const START_CENTER = '0, -50%';

export class OverlayProjector {
  private readonly point = new Vector3();

  constructor(
    private readonly registry: OverlayRegistry,
    private readonly camera: PerspectiveCamera,
  ) {}

  update(t: number, clock: number, viewport: Viewport, frame: FrameState): void {
    this.placeStacks(viewport, frame);
    this.placeHosts(viewport);
    this.placeGate(viewport, frame, clock);
    this.placePills(t, viewport);
    this.placePreview(t, viewport);
  }

  private place(key: string, x: number, y: number, z: number, viewport: Viewport, anchor: string): HTMLElement | undefined {
    const element = this.registry.get(key);
    if (!element) return undefined;
    this.point.set(x, y, z).project(this.camera);
    const sx = (this.point.x * 0.5 + 0.5) * viewport.width;
    const sy = (1 - (this.point.y * 0.5 + 0.5)) * viewport.height;
    element.style.transform = `translate(${sx.toFixed(1)}px, ${sy.toFixed(1)}px) translate(${anchor})`;
    return element;
  }

  private placeStacks(viewport: Viewport, frame: FrameState): void {
    STACKS.forEach((stack, i) => {
      const element = this.place(overlayKey.stack(i), LAYOUT.streamStartX + LAYOUT.streamLabelOffsetX + (LAYOUT.streamLabelStagger[i] ?? 0), stack.lane.y, 0, viewport, LEFT_CENTER);
      if (!element) return;
      const boost = frame.boost[i] ?? 0;
      const env = frame.env[i] ?? 0;
      const base = (0.5 + 0.5 * env) * (1 - boost) + boost;
      element.style.opacity = (base * (1 - 0.4 * frame.selection * (1 - boost))).toFixed(2);
    });
  }

  private placeHosts(viewport: Viewport): void {
    HOSTS.forEach((host, i) => {
      this.place(overlayKey.host(i), HOST_CENTER_X, host.y + LAYOUT.hostHalfHeight + LAYOUT.badgeOffsetY, 0, viewport, CENTER);
    });
    const last = HOSTS.at(-1);
    if (last) this.place(overlayKey.more, HOST_CENTER_X, last.y - LAYOUT.hostHalfHeight - LAYOUT.moreBadgeOffsetY, 0, viewport, CENTER);
  }

  private placeGate(viewport: Viewport, frame: FrameState, clock: number): void {
    const element = this.place(overlayKey.gate, LAYOUT.gateX, 0, 0, viewport, CENTER);
    if (!element) return;
    element.style.setProperty('--p', frame.pulse.toFixed(2));
    element.style.setProperty('--s', (frame.pulse * (0.6 + 0.4 * Math.sin(clock * 6))).toFixed(2));
  }

  private placePills(t: number, viewport: Viewport): void {
    SIGNAL_PILLS.forEach((pill, i) => {
      const element = this.registry.get(overlayKey.pill(i));
      if (!element) return;
      const progress = (t - pill.start) / pill.duration;
      if (progress < 0 || progress > 1) {
        element.style.opacity = '0';
        return;
      }
      const [from, to] = pill.outbound ? [LAYOUT.signalX0, LAYOUT.signalX1] : [LAYOUT.signalX1, LAYOUT.signalX0];
      const y = (HOSTS[pill.hostIndex]?.y ?? 0) + pill.yOffset;
      this.place(overlayKey.pill(i), lerp(from, to, easeInOut(clamp01(progress))), y, 0, viewport, START_CENTER);
      element.style.opacity = Math.min(1, progress / 0.12, (1 - progress) / 0.18).toFixed(2);
    });
  }

  private placePreview(t: number, viewport: Viewport): void {
    const element = this.registry.get(overlayKey.preview);
    if (!element) return;
    const visible = trapezoid(t, PREVIEW.start, PREVIEW.fullIn, PREVIEW.fadeStart, PREVIEW.end);
    element.style.opacity = visible.toFixed(2);
    if (visible <= 0) return;
    this.place(overlayKey.preview, LAYOUT.previewX, HOSTS[PREVIEW_HOST_INDEX]?.y ?? 0, 0, viewport, CENTER);
  }
}
