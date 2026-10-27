import {INTRO_TIME, LOOP_SECONDS, NARROW_QUERY, ORBIT, STATIC_TIME} from '../config';
import {BEATS} from '../data/signals';
import {homePose, hostPose, stackPose, distanceLimits} from '../controls/poses';
import {InertialOrbit} from '../controls/InertialOrbit';
import {PointerGestures} from '../controls/PointerGestures';
import {selectedHostIndex, selectedStackIndex} from '../state/selection';
import type {Selection} from '../types';
import {SceneAnimator, type AnimationInput} from './animator';
import {applyPalette} from './materials';
import {OverlayProjector, type OverlayRegistry} from './overlay';
import {createPalette} from './palette';
import {createWorld, type World} from './world';

export interface SceneCallbacks {
  onBeat(index: number): void;
  onTapHost(hostIndex: number): void;
  onHoverHost(hostIndex: number): void;
  onReset(): void;
  onHostKey(hostIndex: number): void;
}

export interface SceneOptions {
  readonly canvas: HTMLCanvasElement;
  readonly stage: HTMLElement;
  readonly overlay: OverlayRegistry;
  readonly reducedMotion: boolean;
  readonly dark: boolean;
  readonly callbacks: SceneCallbacks;
}

export class SceneController {
  private readonly orbit: InertialOrbit;
  private readonly animator: SceneAnimator;
  private readonly projector: OverlayProjector;
  private readonly gestures: PointerGestures;
  private readonly resizeObserver: ResizeObserver;
  private readonly visibilityObserver: IntersectionObserver;
  private readonly input: AnimationInput = {selectedStack: -1, hoveredStack: -1, focusedHost: -1, hoveredHost: -1};
  private frameId = 0;
  private lastFrame = performance.now();
  private loopTime = 0;
  private clock = INTRO_TIME;
  private beat = -1;
  private width = 1;
  private height = 1;
  private intersecting = true;
  private selection: Selection = {kind: 'idle'};

  private constructor(
    private readonly world: World,
    private readonly options: SceneOptions,
  ) {
    const {canvas, stage, reducedMotion, callbacks} = options;
    this.orbit = new InertialOrbit(world.camera, reducedMotion, () => this.renderStatic());
    this.animator = new SceneAnimator(world, reducedMotion);
    this.projector = new OverlayProjector(options.overlay, world.camera);
    this.gestures = new PointerGestures(canvas, stage, this.orbit, {
      onTap: (event) => callbacks.onTapHost(this.pickHost(event)),
      onHover: (event) => this.hoverHost(this.pickHost(event)),
      onHoverEnd: () => this.hoverHost(-1),
      onReset: callbacks.onReset,
      onHostKey: callbacks.onHostKey,
    }, () => this.height);
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(stage);
    this.visibilityObserver = new IntersectionObserver(([entry]) => {
      this.intersecting = Boolean(entry?.isIntersecting);
    }, {threshold: 0.05});
    this.visibilityObserver.observe(stage);
    this.setDark(options.dark);
    this.resize();
    this.orbit.placeAt(homePose(world.viewRadius, world.camera.aspect));
    if (reducedMotion) this.renderStatic();
    else this.frameId = requestAnimationFrame(this.frame);
  }

  static create(options: SceneOptions): SceneController | null {
    const world = createWorld(options.canvas);
    return world ? new SceneController(world, options) : null;
  }

  setDark(dark: boolean): void {
    applyPalette(this.world.materials, createPalette(dark));
    this.renderStatic();
  }

  setHoveredStack(index: number): void {
    this.input.hoveredStack = index;
    this.renderStatic();
  }

  setSelection(selection: Selection): void {
    const previous = this.selection;
    this.selection = selection;
    this.input.selectedStack = selectedStackIndex(selection);
    this.input.focusedHost = selectedHostIndex(selection);
    if (previous.kind === selection.kind && selection.kind === 'idle') return;
    const radius = this.world.viewRadius;
    const viewport = {radius, aspect: this.world.camera.aspect, narrow: window.matchMedia(NARROW_QUERY).matches};
    if (selection.kind === 'idle') {
      this.orbit.userControlled = false;
      this.orbit.flyTo(homePose(radius, this.world.camera.aspect), ORBIT.homeFlightSeconds);
    } else if (selection.kind === 'stack') {
      this.orbit.userControlled = true;
      this.orbit.flyTo(stackPose(selection.index, viewport), ORBIT.stackFlightSeconds);
    } else {
      this.orbit.userControlled = true;
      this.orbit.flyTo(hostPose(selection.index, radius), ORBIT.hostFlightSeconds);
    }
    this.renderStatic();
  }

  resetView(): void {
    this.orbit.userControlled = false;
    this.orbit.flyTo(homePose(this.world.viewRadius, this.world.camera.aspect), ORBIT.homeFlightSeconds);
    this.renderStatic();
  }

  dispose(): void {
    cancelAnimationFrame(this.frameId);
    this.resizeObserver.disconnect();
    this.visibilityObserver.disconnect();
    this.gestures.dispose();
    this.world.dispose();
  }

  private pickHost(event: PointerEvent): number {
    return this.world.picker.pick(event.clientX, event.clientY, this.options.canvas.getBoundingClientRect(), this.world.camera);
  }

  private hoverHost(index: number): void {
    if (index === this.input.hoveredHost) return;
    this.input.hoveredHost = index;
    this.options.canvas.style.cursor = index >= 0 ? 'pointer' : '';
    this.options.callbacks.onHoverHost(index);
    this.renderStatic();
  }

  private resize(): void {
    const bounds = this.options.stage.getBoundingClientRect();
    this.width = Math.max(1, Math.floor(bounds.width));
    this.height = Math.max(1, Math.floor(bounds.height));
    this.world.resize(this.width, this.height);
    const limits = distanceLimits(this.world.viewRadius);
    this.orbit.setLimits(limits.min, limits.max);
    if (!this.orbit.userControlled) this.orbit.placeAt(homePose(this.world.viewRadius, this.world.camera.aspect));
    this.renderStatic();
  }

  private readonly frame = (now: number): void => {
    this.frameId = requestAnimationFrame(this.frame);
    const dt = Math.min(ORBIT.maxFrameSeconds, (now - this.lastFrame) / 1000);
    this.lastFrame = now;
    if (!this.intersecting || document.hidden) return;
    this.loopTime = (this.loopTime + dt) % LOOP_SECONDS;
    this.clock += dt;
    this.world.materials.shared.time.value = this.clock;
    const autoAllowed = this.selection.kind === 'idle';
    this.orbit.update(dt, now, autoAllowed, this.clock, this.gestures.isDragging);
    this.draw(this.loopTime, dt);
  };

  private renderStatic(): void {
    if (!this.options.reducedMotion) return;
    this.world.materials.shared.time.value = INTRO_TIME;
    this.draw(STATIC_TIME, 0);
  }

  private draw(t: number, dt: number): void {
    this.animator.update(t, dt, this.input);
    this.world.camera.updateMatrixWorld(true);
    this.updateBeat(t);
    this.projector.update(t, this.clock, {width: this.width, height: this.height}, this.animator.frame);
    this.world.render();
  }

  private updateBeat(t: number): void {
    let index = 0;
    BEATS.forEach((beat, i) => {
      if (t >= beat.start) index = i;
    });
    if (index === this.beat) return;
    this.beat = index;
    this.options.callbacks.onBeat(index);
  }
}
