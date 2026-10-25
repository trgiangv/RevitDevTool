import {BufferGeometry, LineSegments, PerspectiveCamera, Points, Scene, WebGLRenderer, type Material} from 'three';

import {CAMERA, PARTICLES} from '../config';
import {HOSTS} from '../data/hosts';
import {PREVIEW_HOST_INDEX} from '../data/signals';
import {clamp, createRandom} from './math';
import {Disposables} from './disposables';
import {
  buildHostGeometry,
  buildLaneGeometry,
  buildPreviewWireframe,
  buildRingGeometry,
  buildStreamGeometry,
} from './geometry';
import {HostPicker} from './hitTest';
import {createMaterials, type SceneMaterials} from './materials';

export interface World {
  readonly renderer: WebGLRenderer;
  readonly scene: Scene;
  readonly camera: PerspectiveCamera;
  readonly materials: SceneMaterials;
  readonly previewLines: LineSegments;
  readonly previewVertexCount: number;
  readonly picker: HostPicker;
  viewRadius: number;
  resize(width: number, height: number): void;
  render(): void;
  dispose(): void;
}

export function computeViewRadius(aspect: number): number {
  return clamp(CAMERA.radiusFactor / aspect, CAMERA.minRadius, CAMERA.maxRadius);
}

function addPoints(scene: Scene, geometry: BufferGeometry, material: Material, order: number): Points {
  const points = new Points(geometry, material);
  points.frustumCulled = false;
  points.renderOrder = order;
  scene.add(points);
  return points;
}

function createRenderer(canvas: HTMLCanvasElement): WebGLRenderer | null {
  try {
    const renderer = new WebGLRenderer({canvas, antialias: true, alpha: true});
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    return renderer;
  } catch {
    return null;
  }
}

export function createWorld(canvas: HTMLCanvasElement): World | null {
  const renderer = createRenderer(canvas);
  if (!renderer) return null;

  const disposables = new Disposables();
  const scene = new Scene();
  const camera = new PerspectiveCamera(CAMERA.fov, 1, CAMERA.near, CAMERA.far);
  const materials = createMaterials(disposables);
  const random = createRandom(PARTICLES.seed);

  const hostGeometry = disposables.add(buildHostGeometry(random));
  const ringGeometry = disposables.add(buildRingGeometry(random));
  const streamGeometry = disposables.add(buildStreamGeometry(random));
  const laneGeometry = disposables.add(buildLaneGeometry(random));
  const previewGeometry = disposables.add(buildPreviewWireframe(HOSTS[PREVIEW_HOST_INDEX]?.y ?? 0));

  addPoints(scene, hostGeometry, materials.host, 1);
  addPoints(scene, ringGeometry, materials.ring, 2);
  addPoints(scene, streamGeometry, materials.stream, 3);
  addPoints(scene, laneGeometry, materials.lane, 4);
  const previewLines = new LineSegments(previewGeometry, materials.preview);
  previewLines.frustumCulled = false;
  previewLines.renderOrder = 5;
  scene.add(previewLines);

  const world: World = {
    renderer,
    scene,
    camera,
    materials,
    previewLines,
    previewVertexCount: previewGeometry.getAttribute('position')?.count ?? 0,
    picker: new HostPicker(scene, disposables),
    viewRadius: CAMERA.minRadius,
    resize(width, height) {
      renderer.setSize(width, height, false);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
      world.viewRadius = computeViewRadius(camera.aspect);
      materials.shared.pixelRatio.value = renderer.getPixelRatio();
      materials.shared.scale.value = 22 * Math.sqrt(world.viewRadius / 22);
    },
    render() {
      renderer.render(scene, camera);
    },
    dispose() {
      disposables.disposeAll();
      renderer.dispose();
    },
  };
  return world;
}
