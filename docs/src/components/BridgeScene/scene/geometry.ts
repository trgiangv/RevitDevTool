import {BufferAttribute, BufferGeometry} from 'three';

import {HOST_CENTER_X, LAYOUT, PARTICLES} from '../config';
import {HOSTS} from '../data/hosts';
import {LANES} from '../data/signals';
import {STACKS} from '../data/stacks';
import {TAU} from './math';

type Random = () => number;
type PointWriter = (index: number, position: Float32Array, meta: Float32Array) => void;

const { hostX0, hostX1, hostHalfDepth: halfDepth, hostHalfHeight: halfHeight, floors } = LAYOUT;
const floorGap = (2 * halfHeight) / (floors - 1);

function pointGeometry(count: number, write: PointWriter): BufferGeometry {
  const position = new Float32Array(count * 3);
  const meta = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) write(i, position, meta);
  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new BufferAttribute(position, 3));
  geometry.setAttribute('aMeta', new BufferAttribute(meta, 3));
  return geometry;
}

export function buildStreamGeometry(random: Random): BufferGeometry {
  const per = PARTICLES.perStream;
  return pointGeometry(STACKS.length * per, (i, position, meta) => {
    const stream = Math.floor(i / per);
    position.set([random() * 2 - 1, random() * 2 - 1, random() * 2 - 1], i * 3);
    meta.set([stream, ((i % per) + random()) / per, random()], i * 3);
  });
}

export function buildLaneGeometry(random: Random): BufferGeometry {
  const per = PARTICLES.perLane;
  return pointGeometry(LANES.length * per, (i, position, meta) => {
    const lane = Math.floor(i / per);
    position.set([0, random() * 2 - 1, random() * 2 - 1], i * 3);
    meta.set([lane, ((i % per) + random()) / per, random()], i * 3);
  });
}

export function buildRingGeometry(random: Random): BufferGeometry {
  return pointGeometry(PARTICLES.ring, (i, position, meta) => {
    position.set([random() * 2 - 1, 0, 0], i * 3);
    meta.set([random() * TAU, random(), i < PARTICLES.ringFront ? 0 : 1], i * 3);
  });
}

function pointOnFootprint(distance: number): [number, number] {
  const width = hostX1 - hostX0;
  const depth = 2 * halfDepth;
  if (distance < width) return [hostX0 + distance, -halfDepth];
  if (distance < width + depth) return [hostX1, -halfDepth + distance - width];
  if (distance < 2 * width + depth) return [hostX1 - (distance - width - depth), halfDepth];
  return [hostX0, halfDepth - (distance - 2 * width - depth)];
}

type HostPoint = readonly [x: number, y: number, z: number, host: number, seed: number, edge: number];

const PERIMETER = 2 * (hostX1 - hostX0 + 2 * halfDepth);
const COLUMN_X = [hostX0 + 0.35, HOST_CENTER_X, hostX1 - 0.35] as const;
const COLUMN_Z = [-0.38, 0.38] as const;

function sample<T>(count: number, make: () => T): T[] {
  return Array.from({length: count}, make);
}

function jitter(random: Random, amount: number): number {
  return (random() - 0.5) * amount;
}

function floorFill(random: Random, hostIndex: number, y: number): HostPoint[] {
  return sample<HostPoint>(PARTICLES.hostFloorFill, () => [
    hostX0 + random() * (hostX1 - hostX0),
    y + jitter(random, 0.02),
    -halfDepth + random() * 2 * halfDepth,
    hostIndex,
    random(),
    0,
  ]);
}

function floorEdge(random: Random, hostIndex: number, y: number): HostPoint[] {
  return sample<HostPoint>(PARTICLES.hostFloorEdge, () => {
    const [x, z] = pointOnFootprint(random() * PERIMETER);
    return [x + jitter(random, 0.015), y + jitter(random, 0.02), z + jitter(random, 0.015), hostIndex, random(), 1];
  });
}

function columnPoints(random: Random, hostIndex: number, hostY: number, x: number, z: number): HostPoint[] {
  return sample<HostPoint>(PARTICLES.hostColumn, () => [
    x + jitter(random, 0.03),
    hostY - halfHeight + random() * 2 * halfHeight,
    z + jitter(random, 0.03),
    hostIndex,
    random(),
    1,
  ]);
}

function floorPoints(random: Random, hostIndex: number, hostY: number): HostPoint[] {
  return Array.from({length: floors}, (_, floor) => {
    const y = hostY - halfHeight + floor * floorGap;
    return [...floorFill(random, hostIndex, y), ...floorEdge(random, hostIndex, y)];
  }).flat();
}

function hostColumns(random: Random, hostIndex: number, hostY: number): HostPoint[] {
  return COLUMN_X.flatMap((x) => COLUMN_Z.flatMap((z) => columnPoints(random, hostIndex, hostY, x, z)));
}

function hostPoints(random: Random): HostPoint[] {
  return HOSTS.flatMap((host, hostIndex) => [...floorPoints(random, hostIndex, host.y), ...hostColumns(random, hostIndex, host.y)]);
}

export function buildHostGeometry(random: Random): BufferGeometry {
  const points = hostPoints(random);
  return pointGeometry(points.length, (i, position, meta) => {
    const [x, y, z, host, seed, edge] = points[i];
    position.set([x, y, z], i * 3);
    meta.set([host, seed, edge], i * 3);
  });
}

export function buildPreviewWireframe(hostY: number): BufferGeometry {
  const segments: number[] = [];
  const box = (x0: number, y0: number, z0: number, x1: number, y1: number, z1: number): void => {
    const corners = [[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1], [x0, y1, z0], [x1, y1, z0], [x1, y1, z1], [x0, y1, z1]];
    const edges = [[0, 1], [1, 2], [2, 3], [3, 0], [4, 5], [5, 6], [6, 7], [7, 4], [0, 4], [1, 5], [2, 6], [3, 7]];
    edges.forEach(([a, b]) => segments.push(...corners[a], ...corners[b]));
  };
  const bottom = hostY - halfHeight;
  box(hostX0, bottom, -halfDepth, hostX1, hostY + halfHeight, halfDepth);
  box(hostX0 + 0.5, bottom + floorGap, -halfDepth, HOST_CENTER_X + 0.3, bottom + 2 * floorGap, 0);
  for (let floor = 1; floor < floors - 1; floor++) {
    const y = bottom + floor * floorGap;
    segments.push(hostX0, y, -halfDepth, hostX1, y, -halfDepth, hostX1, y, -halfDepth, hostX1, y, halfDepth);
    segments.push(hostX1, y, halfDepth, hostX0, y, halfDepth, hostX0, y, halfDepth, hostX0, y, -halfDepth);
  }
  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new BufferAttribute(new Float32Array(segments), 3));
  return geometry;
}
