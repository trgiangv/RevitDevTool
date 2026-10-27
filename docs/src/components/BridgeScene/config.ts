export const LOOP_SECONDS = 16;
export const STATIC_TIME = 8.9;
export const INTRO_TIME = 3;

export const LAYOUT = {
  streamStartX: -8.2,
  gateX: -1.5,
  hostX0: 3.8,
  hostX1: 6.1,
  hostHalfDepth: 0.7,
  hostHalfHeight: 0.75,
  floors: 4,
  laneX0: 6.3,
  laneX1: 11.9,
  signalX0: 6.5,
  signalX1: 8.3,
  previewX: 9.3,
  streamLabelOffsetX: -0.3,
  streamLabelStagger: [0, -0.8, -0.3, -1, -0.15, -0.9, -0.4, -0.7] as readonly number[],
  badgeOffsetY: 0.42,
  moreBadgeOffsetY: 0.7,
} as const;

export const HOST_CENTER_X = (LAYOUT.hostX0 + LAYOUT.hostX1) / 2;
export const GATE_PROGRESS = (LAYOUT.gateX - LAYOUT.streamStartX) / (LAYOUT.hostX0 - LAYOUT.streamStartX);

export const PARTICLES = {
  seed: 20261025,
  perStream: 2000,
  perLane: 900,
  ring: 2100,
  ringFront: 1500,
  hostFloorFill: 450,
  hostFloorEdge: 260,
  hostColumn: 70,
  streamSize: 2.1,
  laneSize: 2.3,
} as const;

export const PREVIEW = {
  start: 12.8,
  fullIn: 13.3,
  fadeStart: 15.2,
  end: 16,
  drawStart: 12.9,
  drawDuration: 1.9,
} as const;

export const CAMERA = {
  fov: 32,
  near: 0.1,
  far: 200,
  homeYaw: 0.18,
  homeTarget: [0.2, 0, 0] as const,
  homeLift: 0.6,
  minRadius: 25,
  maxRadius: 60,
  radiusFactor: 48,
  narrowBoost: 8,
  narrowAspect: 2,
  narrowShift: 2.4,
  narrowShiftSpan: 0.3,
  hostFocusYaw: 0.3,
  hostFocusPitch: Math.PI / 2 - 1.52,
  hostFocusDistance: 0.45,
  stackFocusYaw: 0.12,
  stackWidthWide: 17.5,
  stackWidthNarrow: 14,
  stackHeightPadding: 1.6,
  stackShift: 0.08,
  stackMinSpan: 1.9,
} as const;

export const ORBIT = {
  pitchLimit: 1.396,
  pitchSoftZone: 0.28,
  friction: 2,
  follow: 30,
  zoomFollow: 10,
  yawGain: 3.6,
  pitchGain: 2.6,
  maxSpin: 14,
  keyImpulse: 0.5,
  keyImpulseLimit: 3,
  keyStepReduced: 0.12,
  velocityWindow: 0.09,
  sampleWindowMs: 250,
  autoAmplitude: 0.12,
  autoOmega: 0.25,
  autoIdleSeconds: 3,
  minDistanceFactor: 0.32,
  maxDistanceFactor: 1.25,
  wheelZoomRate: 0.0015,
  keyZoomIn: 0.9,
  keyZoomOut: 1.1,
  tapMaxMovePx: 5,
  tapMaxMs: 300,
  homeFlightSeconds: 0.9,
  hostFlightSeconds: 1.1,
  stackFlightSeconds: 1,
  maxFrameSeconds: 0.05,
} as const;

export const NARROW_QUERY = '(max-width: 759px)';
export const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';