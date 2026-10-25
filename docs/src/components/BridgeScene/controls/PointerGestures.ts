import {ORBIT} from '../config';
import {HOSTS} from '../data/hosts';
import type {InertialOrbit} from './InertialOrbit';

export interface GestureHandlers {
  onTap(event: PointerEvent): void;
  onHover(event: PointerEvent): void;
  onHoverEnd(): void;
  onReset(): void;
  onHostKey(hostIndex: number): void;
}

interface Point {
  x: number;
  y: number;
}

interface Press extends Point {
  readonly id: number;
  readonly time: number;
  readonly wasFlying: boolean;
  moved: number;
}

interface Gesture extends Point {
  readonly mode: 'mouse' | 'pair';
  readonly id: number;
  spread: number;
}

const MAIN_BUTTON = 0;

export class PointerGestures {
  private readonly pointers = new Map<number, Point>();
  private press: Press | null = null;
  private gesture: Gesture | null = null;

  constructor(
    private readonly element: HTMLElement,
    private readonly keyTarget: HTMLElement,
    private readonly orbit: InertialOrbit,
    private readonly handlers: GestureHandlers,
    private readonly viewportHeight: () => number,
  ) {
    element.addEventListener('pointerdown', this.onPointerDown);
    element.addEventListener('pointermove', this.onPointerMove);
    element.addEventListener('pointerup', this.onPointerEnd);
    element.addEventListener('pointercancel', this.onPointerEnd);
    element.addEventListener('pointerleave', this.onPointerLeave);
    element.addEventListener('wheel', this.onWheel, {passive: false});
    keyTarget.addEventListener('keydown', this.onKeyDown);
  }

  get isDragging(): boolean {
    return this.gesture !== null;
  }

  dispose(): void {
    const {element} = this;
    element.removeEventListener('pointerdown', this.onPointerDown);
    element.removeEventListener('pointermove', this.onPointerMove);
    element.removeEventListener('pointerup', this.onPointerEnd);
    element.removeEventListener('pointercancel', this.onPointerEnd);
    element.removeEventListener('pointerleave', this.onPointerLeave);
    element.removeEventListener('wheel', this.onWheel);
    this.keyTarget.removeEventListener('keydown', this.onKeyDown);
  }

  private capture(pointerId: number): boolean {
    try {
      this.element.setPointerCapture(pointerId);
      return true;
    } catch {
      return false;
    }
  }

  private pairCenter(): (Point & {spread: number}) | null {
    const [a, b] = [...this.pointers.values()];
    if (!a || !b) return null;
    return {x: (a.x + b.x) / 2, y: (a.y + b.y) / 2, spread: Math.hypot(a.x - b.x, a.y - b.y)};
  }

  private beginGesture(mode: Gesture['mode'], id: number, at: Point, spread: number): void {
    this.orbit.beginDrag();
    this.gesture = {mode, id, x: at.x, y: at.y, spread};
    this.handlers.onHoverEnd();
  }

  private readonly onPointerDown = (event: PointerEvent): void => {
    const wasFlying = this.orbit.isFlying;
    this.pointers.set(event.pointerId, {x: event.clientX, y: event.clientY});
    this.press = this.pointers.size === 1
      ? {id: event.pointerId, x: event.clientX, y: event.clientY, time: performance.now(), moved: 0, wasFlying}
      : null;
    if (event.pointerType === 'mouse') {
      if (event.button !== MAIN_BUTTON || !this.capture(event.pointerId)) return;
      this.beginGesture('mouse', event.pointerId, {x: event.clientX, y: event.clientY}, 0);
      return;
    }
    if (event.pointerType === 'touch' && this.pointers.size === 2) {
      this.pointers.forEach((_, id) => this.capture(id));
      const center = this.pairCenter();
      if (center) this.beginGesture('pair', -1, center, center.spread);
    }
  };

  private readonly onPointerMove = (event: PointerEvent): void => {
    const tracked = this.pointers.get(event.pointerId);
    if (tracked) {
      tracked.x = event.clientX;
      tracked.y = event.clientY;
    }
    if (this.press?.id === event.pointerId) {
      this.press.moved = Math.max(this.press.moved, Math.hypot(event.clientX - this.press.x, event.clientY - this.press.y));
    }
    if (this.gesture) {
      this.dragGesture(event);
      return;
    }
    if (event.pointerType === 'mouse' && !this.orbit.isFlying) this.handlers.onHover(event);
  };

  private dragGesture(event: PointerEvent): void {
    const gesture = this.gesture;
    if (!gesture) return;
    let center: Point & {spread: number};
    if (gesture.mode === 'mouse') {
      if (event.pointerId !== gesture.id) return;
      center = {x: event.clientX, y: event.clientY, spread: 0};
    } else {
      const pair = this.pairCenter();
      if (!pair) return;
      center = pair;
    }
    const height = this.viewportHeight();
    this.orbit.dragBy(
      (-(center.x - gesture.x) / height) * ORBIT.yawGain,
      ((center.y - gesture.y) / height) * ORBIT.pitchGain,
      performance.now(),
    );
    if (gesture.mode === 'pair' && gesture.spread > 0 && center.spread > 0) this.orbit.pinchBy(gesture.spread / center.spread);
    gesture.x = center.x;
    gesture.y = center.y;
    gesture.spread = center.spread;
  }

  private readonly onPointerEnd = (event: PointerEvent): void => {
    this.pointers.delete(event.pointerId);
    const gesture = this.gesture;
    const ends = gesture && (gesture.mode === 'mouse' ? event.pointerId === gesture.id : this.pointers.size < 2);
    if (gesture && ends) {
      this.gesture = null;
      this.orbit.endDrag(performance.now());
    }
    const press = this.press;
    if (press?.id !== event.pointerId) return;
    this.press = null;
    const isTap = event.type === 'pointerup'
      && !press.wasFlying
      && press.moved < ORBIT.tapMaxMovePx
      && performance.now() - press.time < ORBIT.tapMaxMs;
    if (isTap) this.handlers.onTap(event);
  };

  private readonly onPointerLeave = (): void => {
    if (!this.gesture) this.handlers.onHoverEnd();
  };

  private readonly onWheel = (event: WheelEvent): void => {
    if (!(event.ctrlKey || event.metaKey)) return;
    event.preventDefault();
    this.orbit.zoomBy(Math.exp(event.deltaY * ORBIT.wheelZoomRate));
  };

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    const handled = this.handleKey(event.key);
    if (handled) event.preventDefault();
  };

  private handleKey(key: string): boolean {
    switch (key) {
      case 'ArrowLeft': this.orbit.nudge(-1, 0); return true;
      case 'ArrowRight': this.orbit.nudge(1, 0); return true;
      case 'ArrowUp': this.orbit.nudge(0, 1); return true;
      case 'ArrowDown': this.orbit.nudge(0, -1); return true;
      case '+':
      case '=': this.orbit.zoomBy(ORBIT.keyZoomIn); return true;
      case '-':
      case '_': this.orbit.zoomBy(ORBIT.keyZoomOut); return true;
      case 'Home': this.handlers.onReset(); return true;
      default: break;
    }
    const hostIndex = Number(key) - 1;
    if (Number.isInteger(hostIndex) && hostIndex >= 0 && hostIndex < HOSTS.length) {
      this.handlers.onHostKey(hostIndex);
      return true;
    }
    return false;
  }
}
