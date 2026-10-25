import {Vector3, type PerspectiveCamera} from 'three';

import {ORBIT} from '../config';
import {clamp, clamp01, easeInOut, lerp, wrapAngle} from '../scene/math';
import type {CameraPose} from './poses';

interface Flight {
  elapsed: number;
  readonly duration: number;
  readonly fromYaw: number;
  readonly fromPitch: number;
  readonly fromDistance: number;
  readonly toYaw: number;
  readonly toPitch: number;
  readonly toDistance: number;
  readonly fromTarget: Vector3;
  readonly toTarget: Vector3;
}

interface DragSample {
  readonly time: number;
  readonly yaw: number;
  readonly pitch: number;
}

export class InertialOrbit {
  private yaw = 0;
  private pitch = 0;
  private distance = 1;
  private goalYaw = 0;
  private goalPitch = 0;
  private goalDistance = 1;
  private yawVelocity = 0;
  private pitchVelocity = 0;
  private readonly target = new Vector3();
  private flight: Flight | null = null;
  private samples: DragSample[] = [];
  private minDistance = 1;
  private maxDistance = 100;
  private lastInteraction = Number.NEGATIVE_INFINITY;
  private autoActive = false;
  private autoStart = 0;
  userControlled = false;

  constructor(
    private readonly camera: PerspectiveCamera,
    private readonly reducedMotion: boolean,
    private readonly requestRedraw: () => void,
  ) {}

  get isFlying(): boolean {
    return this.flight !== null;
  }

  setLimits(min: number, max: number): void {
    this.minDistance = min;
    this.maxDistance = max;
    this.goalDistance = clamp(this.goalDistance, min, max);
    this.distance = clamp(this.distance, min, max);
  }

  placeAt(pose: CameraPose): void {
    this.yaw = this.goalYaw = pose.yaw;
    this.pitch = this.goalPitch = pose.pitch;
    this.distance = this.goalDistance = pose.distance;
    this.yawVelocity = 0;
    this.pitchVelocity = 0;
    this.target.copy(pose.target);
    this.applyCamera();
  }

  flyTo(pose: CameraPose, seconds: number): void {
    const toYaw = this.yaw + wrapAngle(pose.yaw - this.yaw);
    if (this.reducedMotion || seconds <= 0) {
      this.placeAt({...pose, yaw: toYaw});
      this.flight = null;
      this.lastInteraction = performance.now();
      this.requestRedraw();
      return;
    }
    this.flight = {
      elapsed: 0,
      duration: seconds,
      fromYaw: this.yaw,
      fromPitch: this.pitch,
      fromDistance: this.distance,
      toYaw,
      toPitch: pose.pitch,
      toDistance: pose.distance,
      fromTarget: this.target.clone(),
      toTarget: pose.target.clone(),
    };
    this.yawVelocity = 0;
    this.pitchVelocity = 0;
  }

  cancelFlight(): void {
    if (!this.flight) return;
    this.flight = null;
    this.goalYaw = this.yaw;
    this.goalPitch = this.pitch;
    this.goalDistance = this.distance;
  }

  beginDrag(): void {
    this.cancelFlight();
    this.userControlled = true;
    this.yawVelocity = 0;
    this.pitchVelocity = 0;
    this.goalYaw = this.yaw;
    this.goalPitch = this.pitch;
    this.goalDistance = this.distance;
    this.samples = [];
  }

  dragBy(deltaYaw: number, deltaPitch: number, now: number): void {
    this.goalYaw += deltaYaw;
    const appliedPitch = this.movePitchGoal(deltaPitch);
    this.samples.push({time: now, yaw: deltaYaw, pitch: appliedPitch});
    while (this.samples.length > 1 && now - (this.samples[0]?.time ?? now) > ORBIT.sampleWindowMs) this.samples.shift();
    this.syncImmediate();
  }

  endDrag(now: number): void {
    this.lastInteraction = now;
    if (this.reducedMotion) {
      this.yawVelocity = 0;
      this.pitchVelocity = 0;
      return;
    }
    let yawSum = 0;
    let pitchSum = 0;
    this.samples.forEach((sample) => {
      if (now - sample.time <= ORBIT.velocityWindow * 1000) {
        yawSum += sample.yaw;
        pitchSum += sample.pitch;
      }
    });
    this.yawVelocity = clamp(yawSum / ORBIT.velocityWindow, -ORBIT.maxSpin, ORBIT.maxSpin);
    this.pitchVelocity = clamp(pitchSum / ORBIT.velocityWindow, -ORBIT.maxSpin, ORBIT.maxSpin);
  }

  zoomBy(factor: number): void {
    this.cancelFlight();
    this.markInteraction();
    this.goalDistance = clamp(this.goalDistance * factor, this.minDistance, this.maxDistance);
    this.syncImmediate();
  }

  pinchBy(factor: number): void {
    this.goalDistance = clamp(this.goalDistance * factor, this.minDistance, this.maxDistance);
    this.syncImmediate();
  }

  nudge(yaw: number, pitch: number): void {
    this.cancelFlight();
    this.markInteraction();
    if (this.reducedMotion) {
      this.goalYaw += yaw * ORBIT.keyStepReduced;
      this.movePitchGoal(pitch * ORBIT.keyStepReduced * (2 / 3));
      this.syncImmediate();
      return;
    }
    const limit = ORBIT.keyImpulseLimit;
    this.yawVelocity = clamp(this.yawVelocity + yaw * ORBIT.keyImpulse, -limit, limit);
    this.pitchVelocity = clamp(this.pitchVelocity + pitch * ORBIT.keyImpulse * 0.6, -limit, limit);
  }

  markInteraction(): void {
    this.userControlled = true;
    this.lastInteraction = performance.now();
  }

  update(dt: number, now: number, autoAllowed: boolean, clock: number, dragging: boolean): void {
    if (this.flight) {
      this.stepFlight(dt, now);
      return;
    }
    if (!dragging) this.coast(dt, now, autoAllowed, clock);
    const follow = 1 - Math.exp(-ORBIT.follow * dt);
    this.yaw += (this.goalYaw - this.yaw) * follow;
    this.pitch += (this.goalPitch - this.pitch) * follow;
    this.distance += (this.goalDistance - this.distance) * (1 - Math.exp(-ORBIT.zoomFollow * dt));
    this.applyCamera();
  }

  private coast(dt: number, now: number, autoAllowed: boolean, clock: number): void {
    const decay = 1 - Math.exp(-ORBIT.friction * dt);
    const auto = !this.reducedMotion && autoAllowed && (now - this.lastInteraction) / 1000 > ORBIT.autoIdleSeconds;
    if (auto && !this.autoActive) this.autoStart = clock;
    this.autoActive = auto;
    const drift = auto ? ORBIT.autoAmplitude * ORBIT.autoOmega * Math.cos(ORBIT.autoOmega * (clock - this.autoStart)) : 0;
    this.yawVelocity += (drift - this.yawVelocity) * decay;
    this.pitchVelocity -= this.pitchVelocity * decay;
    this.goalYaw += this.yawVelocity * dt;
    this.movePitchGoal(this.pitchVelocity * dt);
  }

  private stepFlight(dt: number, now: number): void {
    const flight = this.flight;
    if (!flight) return;
    flight.elapsed += dt;
    const k = easeInOut(clamp01(flight.elapsed / flight.duration));
    this.yaw = this.goalYaw = lerp(flight.fromYaw, flight.toYaw, k);
    this.pitch = this.goalPitch = lerp(flight.fromPitch, flight.toPitch, k);
    this.distance = this.goalDistance = lerp(flight.fromDistance, flight.toDistance, k);
    this.target.lerpVectors(flight.fromTarget, flight.toTarget, k);
    if (k >= 1) {
      this.flight = null;
      this.lastInteraction = now;
    }
    this.applyCamera();
  }

  private movePitchGoal(delta: number): number {
    const applied = this.resistPitch(delta, this.goalPitch);
    this.goalPitch = clamp(this.goalPitch + applied, -ORBIT.pitchLimit, ORBIT.pitchLimit);
    if (Math.abs(this.goalPitch) >= ORBIT.pitchLimit - 1e-4 && this.goalPitch * this.pitchVelocity > 0) this.pitchVelocity = 0;
    return applied;
  }

  private resistPitch(delta: number, pitch: number): number {
    if (delta * pitch <= 0) return delta;
    const soft = 1 - ORBIT.pitchSoftZone;
    const over = clamp01((Math.abs(pitch) - ORBIT.pitchLimit * soft) / (ORBIT.pitchLimit * ORBIT.pitchSoftZone));
    return delta * (1 - over * over);
  }

  private syncImmediate(): void {
    if (!this.reducedMotion) return;
    this.yaw = this.goalYaw;
    this.pitch = this.goalPitch;
    this.distance = this.goalDistance;
    this.applyCamera();
    this.requestRedraw();
  }

  private applyCamera(): void {
    const flat = Math.cos(this.pitch);
    this.camera.position.set(
      this.target.x + Math.sin(this.yaw) * flat * this.distance,
      this.target.y + Math.sin(this.pitch) * this.distance,
      this.target.z + Math.cos(this.yaw) * flat * this.distance,
    );
    this.camera.lookAt(this.target);
    this.camera.updateMatrixWorld(true);
  }
}
