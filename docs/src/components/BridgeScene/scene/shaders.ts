import {GATE_PROGRESS, LAYOUT} from '../config';

export interface ShaderCounts {
  readonly streams: number;
  readonly hosts: number;
  readonly lanes: number;
}

const f = (value: number, digits = 2): string => value.toFixed(digits);

export const POINT_FRAGMENT = `
varying vec3 vCol;
varying float vA;
void main() {
  float d = length(gl_PointCoord - vec2(0.5));
  if (d > 0.5) discard;
  float a = smoothstep(0.5, 0.0, d);
  gl_FragColor = vec4(vCol, a * vA);
}`;

export function streamVertex({streams, hosts}: ShaderCounts): string {
  return `
uniform float uTime; uniform float uPx; uniform float uK; uniform float uSize;
uniform vec3 uCol[${streams}]; uniform float uY[${streams}]; uniform float uZ[${streams}];
uniform float uDest[${streams}]; uniform float uSrc[${streams}]; uniform float uFan[${streams}];
uniform float uHostY[${hosts}];
attribute vec3 aMeta;
varying vec3 vCol; varying float vA;
void main() {
  int s = int(aMeta.x + 0.5);
  vec3 r = position;
  float u = fract(uTime * (0.10 + 0.035 * aMeta.z) + aMeta.y);
  float x = mix(${f(LAYOUT.streamStartX)}, ${f(LAYOUT.hostX0)}, u);
  float g = smoothstep(0.0, ${f(GATE_PROGRESS, 3)}, u);
  float h = smoothstep(${f(GATE_PROGRESS, 3)}, 1.0, u);
  int hi = int(floor(fract(aMeta.y * 37.0 + aMeta.z * 5.0) * ${f(hosts, 1)}));
  float dest = mix(uDest[s], uHostY[hi], uFan[s]);
  float y = mix(mix(uY[s] + r.y * 0.16, r.y * 0.5, g), dest + r.y * 0.5, h);
  float z = mix(mix(uZ[s] + r.z * 0.16, r.z * 0.5, g), r.z * 0.6, h);
  y += sin(uTime * 1.7 + aMeta.y * 40.0) * 0.035 * (1.0 - h);
  float boost = exp(-pow((x - (${f(LAYOUT.gateX)})) * 1.4, 2.0));
  float v = 0.5 + 0.5 * r.x;
  vCol = uCol[s] * (1.0 + 0.5 * boost);
  vA = smoothstep(0.0, 0.07, u) * (1.0 - smoothstep(0.9, 1.0, u)) * uSrc[s] * (0.5 + 0.5 * v) * (1.0 + 0.8 * boost);
  vec4 mv = modelViewMatrix * vec4(x, y, z, 1.0);
  gl_PointSize = uSize * uPx * uK / -mv.z * (0.6 + 0.4 * v) * (1.0 + 0.7 * boost);
  gl_Position = projectionMatrix * mv;
}`;
}

export function hostVertex({hosts}: ShaderCounts): string {
  return `
uniform float uTime; uniform float uGlowRel; uniform float uBase; uniform float uPx; uniform float uK; uniform float uSize;
uniform float uHostY[${hosts}]; uniform float uArrive[${hosts}]; uniform float uFlash[${hosts}];
uniform vec3 uCol; uniform vec3 uHi;
attribute vec3 aMeta;
varying vec3 vCol; varying float vA;
void main() {
  int h = int(aMeta.x + 0.5);
  vec3 p = position;
  p.y += sin(uTime * 0.6 + aMeta.y * 30.0) * 0.01;
  float arr = uArrive[h];
  float band = exp(-pow((p.y - uHostY[h] - uGlowRel) * 2.6, 2.0)) * arr;
  float tw = 0.5 + 0.5 * sin(uTime * 2.4 + aMeta.y * 60.0);
  float lit = clamp(arr * 0.6 + band + uFlash[h] * 0.6, 0.0, 1.0);
  vCol = mix(uCol, uHi, lit);
  vA = uBase + arr * 0.3 * (0.5 + 0.5 * tw) + band * 0.55 + uFlash[h] * 0.35 * tw;
  vec4 mv = modelViewMatrix * vec4(p, 1.0);
  gl_PointSize = uSize * uPx * uK / -mv.z * (aMeta.z > 0.5 ? 1.15 : 1.0) * (1.0 + 0.6 * band);
  gl_Position = projectionMatrix * mv;
}`;
}

export const RING_VERTEX = `
uniform float uTime; uniform float uPulse; uniform float uPx; uniform float uK; uniform float uSize; uniform vec3 uCol;
attribute vec3 aMeta;
varying vec3 vCol; varying float vA;
void main() {
  bool dust = aMeta.z > 0.5;
  float a = aMeta.x + uTime * (dust ? -0.15 : 0.25);
  float R = dust ? sqrt(aMeta.y) * 1.7 : 1.85 + (aMeta.y - 0.5) * 0.12 + uPulse * 0.08 * sin(aMeta.x * 6.0 + uTime * 4.0);
  vec3 p = vec3(${f(LAYOUT.gateX)} + position.x * 0.05, cos(a) * R, sin(a) * R);
  vCol = uCol * (1.0 + 0.4 * uPulse);
  vA = (dust ? 0.08 : 0.5) * (0.7 + 0.3 * sin(uTime * 1.5 + aMeta.x * 3.0)) + uPulse * (dust ? 0.14 : 0.4);
  vec4 mv = modelViewMatrix * vec4(p, 1.0);
  gl_PointSize = uSize * uPx * uK / -mv.z * (dust ? 0.8 : 1.15);
  gl_Position = projectionMatrix * mv;
}`;

export function laneVertex({lanes}: ShaderCounts): string {
  return `
uniform float uTime; uniform float uPx; uniform float uK; uniform float uSize;
uniform float uAct[${lanes}]; uniform float uY[${lanes}]; uniform float uDir[${lanes}]; uniform vec3 uCol[${lanes}];
attribute vec3 aMeta;
varying vec3 vCol; varying float vA;
void main() {
  int l = int(aMeta.x + 0.5);
  vec3 r = position;
  float u = fract(uTime * (0.35 + 0.15 * aMeta.z) + aMeta.y);
  float d = uDir[l];
  float x = d > 0.0 ? mix(${f(LAYOUT.laneX0)}, ${f(LAYOUT.laneX1)}, u) : mix(${f(LAYOUT.laneX1)}, ${f(LAYOUT.laneX0)}, u);
  float head = d > 0.0 ? u : 1.0 - u;
  float y = uY[l] + r.y * 0.14;
  float z = r.z * 0.3;
  vCol = uCol[l] * (1.0 + 0.5 * head);
  vA = uAct[l] * pow(head, 1.4) * (1.0 - smoothstep(0.93, 1.0, head));
  vec4 mv = modelViewMatrix * vec4(x, y, z, 1.0);
  gl_PointSize = uSize * uPx * uK / -mv.z * (0.7 + 0.5 * head);
  gl_Position = projectionMatrix * mv;
}`;
}
