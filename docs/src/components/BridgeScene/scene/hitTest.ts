import {BoxGeometry, Mesh, MeshBasicMaterial, Raycaster, Vector2, type PerspectiveCamera, type Scene} from 'three';

import {HOST_CENTER_X, LAYOUT} from '../config';
import {HOSTS} from '../data/hosts';
import type {Disposables} from './disposables';

interface HostUserData {
  hostIndex: number;
}

export class HostPicker {
  private readonly raycaster = new Raycaster();
  private readonly pointer = new Vector2();
  private readonly meshes: Mesh[];

  constructor(scene: Scene, disposables: Disposables) {
    const material = disposables.add(new MeshBasicMaterial({visible: false}));
    const geometry = disposables.add(
      new BoxGeometry(
        LAYOUT.hostX1 - LAYOUT.hostX0 + 0.6,
        2 * LAYOUT.hostHalfHeight + 0.5,
        2 * LAYOUT.hostHalfDepth + 0.6,
      ),
    );
    this.meshes = HOSTS.map((host, hostIndex) => {
      const mesh = new Mesh(geometry, material);
      mesh.position.set(HOST_CENTER_X, host.y, 0);
      mesh.userData = {hostIndex} satisfies HostUserData;
      scene.add(mesh);
      mesh.updateMatrixWorld(true);
      return mesh;
    });
  }

  pick(clientX: number, clientY: number, bounds: DOMRect, camera: PerspectiveCamera): number {
    this.pointer.set(((clientX - bounds.left) / bounds.width) * 2 - 1, -(((clientY - bounds.top) / bounds.height) * 2 - 1));
    this.raycaster.setFromCamera(this.pointer, camera);
    const hit = this.raycaster.intersectObjects(this.meshes, false)[0];
    return hit ? (hit.object.userData as HostUserData).hostIndex : -1;
  }
}
