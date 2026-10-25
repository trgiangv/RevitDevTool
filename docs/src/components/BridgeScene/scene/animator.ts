import {LAYOUT, PREVIEW} from '../config';
import {HOSTS} from '../data/hosts';
import {HOST_FLASHES, LANES} from '../data/signals';
import {STACKS} from '../data/stacks';
import type {Flash} from '../types';
import {bump, clamp01, easeInOut, smoothTo, trapezoid} from './math';
import {setUniform, uniformValue} from './materials';
import type {World} from './world';

export interface AnimationInput {
  selectedStack: number;
  hoveredStack: number;
  focusedHost: number;
  hoveredHost: number;
}

export interface FrameState {
  readonly env: number[];
  readonly boost: number[];
  selection: number;
  pulse: number;
}

const BASE_ARRIVAL = 0.15;
const SMOOTHING_RATE = 8;
const PREVIEW_FADE_SECONDS = PREVIEW.end - PREVIEW.fadeStart;
const HOVER_ARRIVAL = 0.6;
const PREVIEW_FLASH = 0.55;
const FOCUS_FLASH = 0.45;
const FAN_FLASH = 0.3;
const FAN_EPSILON = 0.001;

const arrivalLevel = (strength: number): number => BASE_ARRIVAL + (1 - BASE_ARRIVAL) * strength;

function flashSum(t: number, flashes: readonly Flash[]): number {
  return flashes.reduce((sum, {at, width}) => sum + bump(t, at, width), 0);
}

export class SceneAnimator {
  readonly frame: FrameState = {
    env: STACKS.map(() => 0),
    boost: STACKS.map(() => 0),
    selection: 0,
    pulse: 0,
  };

  private readonly fan = STACKS.map(() => 0);
  private readonly sources: number[];
  private readonly fanUniform: number[];
  private readonly arrive: number[];
  private readonly flash: number[];
  private readonly laneActivity: number[];

  constructor(
    private readonly world: World,
    private readonly reducedMotion: boolean,
  ) {
    const {stream, host, lane} = world.materials;
    this.sources = uniformValue<number[]>(stream, 'uSrc');
    this.fanUniform = uniformValue<number[]>(stream, 'uFan');
    this.arrive = uniformValue<number[]>(host, 'uArrive');
    this.flash = uniformValue<number[]>(host, 'uFlash');
    this.laneActivity = uniformValue<number[]>(lane, 'uAct');
  }
  update(t: number, dt: number, input: AnimationInput): void {
    const rate = this.reducedMotion ? 1 : 1 - Math.exp(-SMOOTHING_RATE * dt);
    this.updateStreams(t, rate, input);
    const fanMax = Math.max(0, ...this.fan);
    this.updateHosts(t, input, fanMax);
    this.updateLanes(t);
    this.updatePreview(t);
    this.updatePulse(fanMax);
    const half = LAYOUT.hostHalfHeight;
    setUniform(this.world.materials.host, 'uGlowRel', -half + 2 * half * ((t * 0.5) % 1));
  }

  private updateStreams(t: number, rate: number, input: AnimationInput): void {
    const {frame} = this;
    frame.selection = smoothTo(frame.selection, input.selectedStack >= 0 ? 1 : 0, rate);
    STACKS.forEach((stack, i) => {
      const [from, to] = stack.activeWindow;
      const env = trapezoid(t, from, from + 0.5, to, to + 0.7);
      frame.env[i] = env;
      const lit = i === input.selectedStack || i === input.hoveredStack ? 1 : 0;
      frame.boost[i] = smoothTo(frame.boost[i] ?? 0, lit, rate);
      this.fan[i] = smoothTo(this.fan[i] ?? 0, i === input.selectedStack ? 1 : 0, rate);
      const boost = frame.boost[i] ?? 0;
      this.sources[i] = ((0.12 + 0.88 * env) * (1 - boost) + boost) * (1 - 0.8 * frame.selection * (1 - boost));
      this.fanUniform[i] = this.fan[i] ?? 0;
    });
  }

  private updateHosts(t: number, input: AnimationInput, fanMax: number): void {
    this.updateArrivals();
    this.updateFlashes(t);
    this.applyHostInput(input);
    this.applyFan(fanMax);
  }

  private raiseArrival(host: number, level: number): void {
    this.arrive[host] = Math.max(this.arrive[host] ?? 0, level);
  }

  private brighten(host: number, amount: number): void {
    this.flash[host] = Math.min(1, (this.flash[host] ?? 0) + amount);
  }

  private updateArrivals(): void {
    this.arrive.fill(BASE_ARRIVAL);
    STACKS.forEach((stack, i) => {
      this.raiseArrival(stack.hostIndex, arrivalLevel(this.frame.env[i] ?? 0));
    });
  }

  private updateFlashes(t: number): void {
    HOSTS.forEach((_, h) => {
      this.flash[h] = Math.min(1, flashSum(t, HOST_FLASHES[h] ?? []));
    });
    this.brighten(0, PREVIEW_FLASH * trapezoid(t, PREVIEW.start, PREVIEW.fullIn, PREVIEW.fadeStart, PREVIEW.end));
  }

  private applyHostInput({hoveredHost, focusedHost}: AnimationInput): void {
    if (hoveredHost >= 0) this.raiseArrival(hoveredHost, HOVER_ARRIVAL);
    if (focusedHost < 0) return;
    this.arrive[focusedHost] = 1;
    this.brighten(focusedHost, FOCUS_FLASH);
  }

  private applyFan(fanMax: number): void {
    if (fanMax <= FAN_EPSILON) return;
    const level = arrivalLevel(fanMax);
    for (let h = 0; h < this.arrive.length; h++) {
      this.raiseArrival(h, level);
      this.brighten(h, FAN_FLASH * fanMax);
    }
  }

  private updateLanes(t: number): void {
    LANES.forEach((lane, i) => {
      this.laneActivity[i] = trapezoid(t, ...lane.activity);
    });
  }

  private updatePreview(t: number): void {
    const {previewLines, previewVertexCount, materials} = this.world;
    const progress = easeInOut(clamp01((t - PREVIEW.drawStart) / PREVIEW.drawDuration));
    previewLines.geometry.setDrawRange(0, Math.floor((previewVertexCount * progress) / 2) * 2);
    materials.preview.opacity = progress > 0 ? 1 - clamp01((t - PREVIEW.fadeStart) / PREVIEW_FADE_SECONDS) : 0;
  }

  private updatePulse(fanMax: number): void {
    const strongest = Math.max(0, ...this.frame.env);
    const brightest = Math.max(0, ...this.flash);
    this.frame.pulse = Math.min(1, Math.max(0.85 * fanMax, strongest * 0.7 + brightest * 0.3));
    setUniform(this.world.materials.ring, 'uPulse', this.frame.pulse);
  }
}
