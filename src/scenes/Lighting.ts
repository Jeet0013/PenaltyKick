/**
 * Floodlights, plus a room baked into an environment map.
 *
 * The environment is the part that is easy to skip and expensive to omit.
 * Physical materials with a clearcoat — the goal frame, the wet turf, the
 * boots — reflect their surroundings. Give them three point lights and nothing
 * else and they reflect three points: every polished surface gets three small
 * highlights and is otherwise dead. Baked once at load, free per frame.
 */

import * as THREE from 'three';

import { FIELD } from '../game/BallPhysics';
import { QUALITY, type Quality } from './quality';

export class Lighting {
  readonly group = new THREE.Group();
  readonly #key: THREE.DirectionalLight;
  readonly #rim: THREE.DirectionalLight;
  #environment: THREE.Texture | null = null;

  constructor(quality: Quality) {
    const preset = QUALITY[quality];
    this.group.name = 'Lighting';

    /*
     * Four floodlight towers would be authentic and would cost four shadow
     * maps. One key light standing in for the bank that matters, plus a cool
     * rim from behind the goal, reads the same and costs one.
     */
    /*
     * Warm white, not the blue-white it started as.
     *
     * Stadium floods are metal-halide and read slightly warm on grass. A
     * 0xdff2ff key at 2.6 pushed every green channel past the red and left the
     * pitch cyan — the base colour was never the problem, the light was.
     */
    this.#key = new THREE.DirectionalLight(0xfff2dc, 2.15);
    this.#key.position.set(9, 17, 6);
    this.#key.target.position.set(0, 0, -FIELD.spotToGoal);
    this.#key.castShadow = preset.shadows;

    if (preset.shadows) {
      const shadow = this.#key.shadow;
      shadow.mapSize.set(preset.shadowMapSize, preset.shadowMapSize);
      // Fitted to the penalty area rather than the whole pitch. An oversized
      // frustum spreads texels over grass nobody is looking at and leaves the
      // ball with a blocky shadow.
      const extent = 14;
      shadow.camera.left = -extent;
      shadow.camera.right = extent;
      shadow.camera.top = extent;
      shadow.camera.bottom = -extent;
      shadow.camera.near = 4;
      shadow.camera.far = 46;
      shadow.bias = -0.0004;
      shadow.normalBias = 0.03;
      shadow.radius = 2.5;
      shadow.camera.updateProjectionMatrix();
    }

    // Cyan from behind the goal: separates the frame and the keeper from the
    // dark stands, which is what stops the silhouette getting lost.
    /*
     * 0.28, down from 0.85 via 0.5.
     *
     * A directional light's specular lobe on a big flat plane is a pool, not a
     * point, and at 0.5 the rim's pool covered the whole middle of the pitch in
     * cyan — the single thing that made it read as water. It only ever needed
     * to catch the goal frame and the keeper's shoulders.
     */
    this.#rim = new THREE.DirectionalLight(0x2ad4f5, 0.28);
    // High as well as behind. From 5 m up its specular pool landed in the
    // middle of the penalty area; from 15 m it lands beyond the goal, where
    // there is nothing to tint.
    this.#rim.position.set(-6, 15, -FIELD.spotToGoal - 12);

    // Cool fill, but much less of it: the blue ambient was tinting the shadow
    // side of everything, which is half of why the pitch went cyan.
    const ambient = new THREE.AmbientLight(0x1c2a38, preset.environment ? 0.18 : 0.7);

    this.group.add(this.#key, this.#key.target, this.#rim, ambient);
  }

  /**
   * Bake the surroundings.
   *
   * Authored in code rather than fetched: an .hdr would be a second request
   * that the single-file build cannot inline, and a hand-built room lets the
   * bright panel sit where the key light already is, so reflections and
   * shading agree about where the light is coming from.
   */
  applyEnvironment(renderer: THREE.WebGLRenderer, scene: THREE.Scene, quality: Quality): void {
    if (!QUALITY[quality].environment) return;

    const pmrem = new THREE.PMREMGenerator(renderer);
    pmrem.compileEquirectangularShader();

    const room = new THREE.Scene();

    const shell = new THREE.Mesh(
      new THREE.BoxGeometry(60, 34, 60),
      // Neutral dark rather than dark blue: the shell is the ambient term for
      // every material in the scene, and a blue one tints all of them.
      new THREE.MeshStandardMaterial({ color: 0x10141c, side: THREE.BackSide, roughness: 1 }),
    );
    room.add(shell);

    // Emissive values above 1 on purpose: a tone-mapped renderer wants
    // headroom, and a light clamped to 1 reads as a grey card, not a light.
    const panel = (w: number, h: number, hex: number, gain: number, pos: THREE.Vector3, rot: THREE.Euler) => {
      const material = new THREE.MeshBasicMaterial({ side: THREE.DoubleSide });
      material.color.setHex(hex).multiplyScalar(gain);
      const mesh = new THREE.Mesh(new THREE.PlaneGeometry(w, h), material);
      mesh.position.copy(pos);
      mesh.rotation.copy(rot);
      room.add(mesh);
    };

    // The floodlight bank, where the key light is.
    panel(26, 26, 0xfff2dc, 5.0, new THREE.Vector3(6, 16.6, 4), new THREE.Euler(Math.PI / 2, 0, 0));
    // Cyan wash from behind the goal, matching the rim.
    panel(18, 5, 0x2ad4f5, 1.0, new THREE.Vector3(0, 5, -28), new THREE.Euler(0, 0, 0));
    // Warm city bounce from the opposite side.
    panel(30, 6, 0xff9a5c, 1.4, new THREE.Vector3(0, 3, 28), new THREE.Euler(0, Math.PI, 0));

    const target = pmrem.fromScene(room, 0.035);
    scene.environment = target.texture;
    scene.environmentIntensity = 0.24;
    this.#environment = target.texture;

    room.traverse((node) => {
      if (node instanceof THREE.Mesh) {
        node.geometry.dispose();
        const m: THREE.Material | THREE.Material[] = node.material;
        if (Array.isArray(m)) m.forEach((x) => x.dispose());
        else m.dispose();
      }
    });
    pmrem.dispose();
  }

  dispose(): void {
    this.#key.shadow.map?.dispose();
    this.#key.dispose();
    this.#rim.dispose();
    this.#environment?.dispose();
  }
}
