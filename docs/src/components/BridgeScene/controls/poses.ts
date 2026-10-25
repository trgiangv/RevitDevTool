import {Vector3} from 'three';

import {CAMERA, HOST_CENTER_X, LAYOUT, ORBIT} from '../config';
import {HOSTS} from '../data/hosts';
import {STACKS} from '../data/stacks';
import {clamp} from '../scene/math';

export interface CameraPose {
  readonly yaw: number;
  readonly pitch: number;
  readonly distance: number;
  readonly target: Vector3;
}

export interface Viewport {
  readonly radius: number;
  readonly aspect: number;
  readonly narrow: boolean;
}

export function distanceLimits(radius: number): {min: number; max: number} {
  return {min: radius * ORBIT.minDistanceFactor, max: radius * ORBIT.maxDistanceFactor};
}

export function homePose(radius: number): CameraPose {
  const [x, y, z] = CAMERA.homeTarget;
  return {
    yaw: CAMERA.homeYaw,
    pitch: Math.atan2(CAMERA.homeLift, radius),
    distance: Math.hypot(radius, CAMERA.homeLift),
    target: new Vector3(x, y, z),
  };
}

export function hostPose(hostIndex: number, radius: number): CameraPose {
  return {
    yaw: CAMERA.hostFocusYaw,
    pitch: CAMERA.hostFocusPitch,
    distance: radius * CAMERA.hostFocusDistance,
    target: new Vector3(HOST_CENTER_X, HOSTS[hostIndex]?.y ?? 0, 0),
  };
}

export function stackPose(stackIndex: number, {radius, aspect, narrow}: Viewport): CameraPose {
  const laneY = STACKS[stackIndex]?.lane.y ?? 0;
  const tanHalfFov = Math.tan((CAMERA.fov / 2) * (Math.PI / 180));
  const low = Math.min(laneY, -CAMERA.stackMinSpan);
  const high = Math.max(laneY, CAMERA.stackMinSpan);
  const wantedWidth = narrow ? CAMERA.stackWidthNarrow : CAMERA.stackWidthWide;
  const fitWidth = wantedWidth / (2 * tanHalfFov * aspect);
  const fitHeight = (high - low + CAMERA.stackHeightPadding) / (2 * tanHalfFov);
  const limits = distanceLimits(radius);
  const distance = clamp(Math.max(fitWidth, fitHeight), limits.min, limits.max);
  const visibleWidth = 2 * distance * tanHalfFov * aspect;
  const sourceX = LAYOUT.streamStartX + LAYOUT.streamLabelOffsetX;
  const targetX = narrow ? (sourceX + LAYOUT.gateX) / 2 : LAYOUT.gateX - CAMERA.stackShift * visibleWidth;
  return {
    yaw: CAMERA.stackFocusYaw,
    pitch: Math.atan2(CAMERA.homeLift, radius),
    distance,
    target: new Vector3(targetX, (low + high) / 2, 0),
  };
}
