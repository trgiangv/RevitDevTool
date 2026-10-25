import {
  AdditiveBlending,
  LineBasicMaterial,
  NormalBlending,
  ShaderMaterial,
  Vector3,
  type IUniform,
} from 'three';

import {PARTICLES} from '../config';
import {HOSTS} from '../data/hosts';
import {LANES} from '../data/signals';
import {STACKS} from '../data/stacks';
import {hexToRgb} from './math';
import type {ScenePalette} from './palette';
import {hostVertex, laneVertex, POINT_FRAGMENT, RING_VERTEX, streamVertex, type ShaderCounts} from './shaders';
import type {Disposables} from './disposables';

type Uniforms = Record<string, IUniform<unknown>>;

export interface SceneMaterials {
  readonly stream: ShaderMaterial;
  readonly host: ShaderMaterial;
  readonly ring: ShaderMaterial;
  readonly lane: ShaderMaterial;
  readonly preview: LineBasicMaterial;
  readonly shared: { time: IUniform<number>; pixelRatio: IUniform<number>; scale: IUniform<number> };
}

const COUNTS: ShaderCounts = {streams: STACKS.length, hosts: HOSTS.length, lanes: LANES.length};
const zeros = (length: number): number[] => new Array<number>(length).fill(0);
const vectors = (length: number): Vector3[] => Array.from({length}, () => new Vector3());

export const toVector = (hex: string): Vector3 => new Vector3(...hexToRgb(hex));

function pointMaterial(vertexShader: string, shared: SceneMaterials['shared'], uniforms: Uniforms): ShaderMaterial {
  return new ShaderMaterial({
    vertexShader,
    fragmentShader: POINT_FRAGMENT,
    transparent: true,
    depthTest: false,
    depthWrite: false,
    uniforms: {uTime: shared.time, uPx: shared.pixelRatio, uK: shared.scale, ...uniforms},
  });
}

export function createMaterials(disposables: Disposables): SceneMaterials {
  const shared = {time: {value: 0}, pixelRatio: {value: 1}, scale: {value: 22}};
  const stream = pointMaterial(streamVertex(COUNTS), shared, {
    uSize: {value: PARTICLES.streamSize},
    uCol: {value: vectors(STACKS.length)},
    uY: {value: STACKS.map((stack) => stack.lane.y)},
    uZ: {value: STACKS.map((stack) => stack.lane.z)},
    uDest: {value: STACKS.map((stack) => HOSTS[stack.hostIndex]?.y ?? 0)},
    uSrc: {value: STACKS.map(() => 1)},
    uFan: {value: zeros(STACKS.length)},
    uHostY: {value: HOSTS.map((host) => host.y)},
  });
  const host = pointMaterial(hostVertex(COUNTS), shared, {
    uSize: {value: PARTICLES.streamSize},
    uGlowRel: {value: 0},
    uBase: {value: 0.3},
    uHostY: {value: HOSTS.map((item) => item.y)},
    uArrive: {value: HOSTS.map(() => 0.2)},
    uFlash: {value: zeros(HOSTS.length)},
    uCol: {value: new Vector3()},
    uHi: {value: new Vector3()},
  });
  const ring = pointMaterial(RING_VERTEX, shared, {
    uSize: {value: PARTICLES.streamSize},
    uPulse: {value: 0},
    uCol: {value: new Vector3()},
  });
  const lane = pointMaterial(laneVertex(COUNTS), shared, {
    uSize: {value: PARTICLES.laneSize},
    uAct: {value: zeros(LANES.length)},
    uY: {value: LANES.map((item) => HOSTS[item.hostIndex]?.y ?? 0)},
    uDir: {value: LANES.map((item) => item.direction)},
    uCol: {value: vectors(LANES.length)},
  });
  const preview = new LineBasicMaterial({transparent: true, depthTest: false, depthWrite: false, opacity: 0});
  [stream, host, ring, lane, preview].forEach((material) => disposables.add(material));
  return {stream, host, ring, lane, preview, shared};
}

export function uniformValue<T>(material: ShaderMaterial, name: string): T {
  const uniform = material.uniforms[name];
  if (!uniform) throw new Error(`Missing shader uniform: ${name}`);
  return uniform.value as T;
}

export function setUniform(material: ShaderMaterial, name: string, value: number): void {
  const uniform = material.uniforms[name];
  if (!uniform) throw new Error(`Missing shader uniform: ${name}`);
  uniform.value = value;
}

function setUniformVector(material: ShaderMaterial, name: string, hex: string): void {
  uniformValue<Vector3>(material, name).copy(toVector(hex));
}

function setUniformVectors(material: ShaderMaterial, name: string, hexes: readonly string[]): void {
  const values = uniformValue<Vector3[]>(material, name);
  hexes.forEach((hex, index) => values[index]?.copy(toVector(hex)));
}

export function applyPalette(materials: SceneMaterials, palette: ScenePalette): void {
  const tones = {gate: palette.gate, ai: palette.ai, ok: palette.ok};
  setUniformVectors(materials.stream, 'uCol', palette.streams);
  setUniformVector(materials.host, 'uCol', palette.host);
  setUniformVector(materials.host, 'uHi', palette.highlight);
  setUniformVector(materials.ring, 'uCol', palette.gate);
  setUniformVectors(materials.lane, 'uCol', LANES.map((item) => tones[item.tone]));
  setUniform(materials.host, 'uBase', palette.baseAlpha);
  [materials.stream, materials.host, materials.ring, materials.lane].forEach((material) => {
    setUniform(material, 'uSize', palette.pointSize);
    material.blending = palette.dark ? AdditiveBlending : NormalBlending;
    material.needsUpdate = true;
  });
  materials.preview.color.set(palette.edge);
}