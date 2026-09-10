/**
 * The striker, the keeper, the referee and the ball.
 *
 * Built from primitives and animated procedurally. The spec allows placeholder
 * animation for the slice, and there is a real advantage to starting here: a
 * capsule that moves correctly is more convincing than a detailed model that
 * moves wrongly, and the motion is what has to be right first. Replacing these
 * with rigged GLBs later changes this file and nothing else — every other
 * system talks to `setPose`, not to geometry.
 */

import * as THREE from 'three';

import { FIELD, type Vec3 } from '../game/BallPhysics';
import type { DiveDirection } from '../game/Goalkeeper';

/**
 * A jointed figure: torso, head, two arms, two legs.
 *
 * ## Proportions are load-bearing
 *
 * The first version had a 0.54 m leg whose top overlapped the torso and whose
 * foot stopped 28 cm above the turf, so every figure read as a capsule
 * hovering over the pitch. Nothing about the animation could fix that. The
 * numbers below build a 1.82 m person: hips at 0.92, shoulders at 1.52, eyes
 * just under 1.7 — and the feet reach the ground.
 *
 * ## Limbs rotate about joints, not about their middles
 *
 * A capsule rotated about its own centre scissors through the torso and swings
 * its shoulder end backwards. Each limb therefore lives in a pivot `Group`
 * placed at the joint, with the mesh hung half its length below. Rotating the
 * pivot swings the limb from the shoulder or hip, which is the only way a
 * stride or a dive reads as one.
 */
const HIP_Y = 0.92;
const SHOULDER_Y = 1.52;

class Figure {
  readonly group = new THREE.Group();
  readonly #torso: THREE.Mesh;
  readonly #head: THREE.Mesh;
  readonly #arms: [THREE.Group, THREE.Group];
  readonly #legs: [THREE.Group, THREE.Group];
  readonly #disposables: Array<{ dispose(): void }> = [];

  constructor(kit: THREE.ColorRepresentation, accent: THREE.ColorRepresentation) {
    const skin = new THREE.MeshStandardMaterial({ color: 0x8d5a3b, roughness: 0.72 });
    const cloth = new THREE.MeshPhysicalMaterial({
      color: kit,
      roughness: 0.68,
      sheen: 0.4,
      sheenRoughness: 0.6,
      sheenColor: new THREE.Color(0xffffff),
    });
    const trim = new THREE.MeshStandardMaterial({
      color: accent,
      emissive: accent,
      emissiveIntensity: 0.55,
      roughness: 0.5,
    });
    const boot = new THREE.MeshStandardMaterial({ color: 0x11141b, roughness: 0.45, metalness: 0.3 });
    this.#disposables.push(skin, cloth, trim, boot);

    // Torso spans hip to shoulder, plus a neck: 0.92 to 1.62.
    const torsoGeo = new THREE.CapsuleGeometry(0.155, 0.4, 6, 12);
    const headGeo = new THREE.SphereGeometry(0.115, 16, 12);
    const armGeo = new THREE.CapsuleGeometry(0.052, 0.53, 4, 8);
    const legGeo = new THREE.CapsuleGeometry(0.072, 0.775, 4, 8);
    const bootGeo = new THREE.BoxGeometry(0.11, 0.06, 0.24);
    this.#disposables.push(torsoGeo, headGeo, armGeo, legGeo, bootGeo);

    this.#torso = new THREE.Mesh(torsoGeo, cloth);
    this.#torso.position.y = 1.27;
    this.#torso.castShadow = true;

    this.#head = new THREE.Mesh(headGeo, skin);
    this.#head.position.y = 1.7;
    this.#head.castShadow = true;

    // Arms: pivot at the shoulder, capsule hung 0.317 below it (half of the
    // 0.634 total length), so the hand ends around 0.89 — mid-thigh, which is
    // where a hanging hand actually sits.
    this.#arms = [new THREE.Group(), new THREE.Group()];
    for (const [i, pivot] of this.#arms.entries()) {
      pivot.position.set(i === 0 ? -0.2 : 0.2, SHOULDER_Y, 0);
      const mesh = new THREE.Mesh(armGeo, trim);
      mesh.position.y = -0.317;
      mesh.castShadow = true;
      pivot.add(mesh);
      this.group.add(pivot);
    }

    // Legs: pivot at the hip, capsule hung 0.46 below, so the sole lands on 0.
    this.#legs = [new THREE.Group(), new THREE.Group()];
    for (const [i, pivot] of this.#legs.entries()) {
      pivot.position.set(i === 0 ? -0.093 : 0.093, HIP_Y, 0);
      const mesh = new THREE.Mesh(legGeo, cloth);
      mesh.position.y = -0.46;
      mesh.castShadow = true;
      const shoe = new THREE.Mesh(bootGeo, boot);
      shoe.position.set(0, -0.89, 0.05);
      shoe.castShadow = true;
      pivot.add(mesh, shoe);
      this.group.add(pivot);
    }

    this.group.add(this.#torso, this.#head);
  }

  /** Idle breathing, so a waiting figure is not a statue. */
  breathe(t: number): void {
    this.#torso.scale.y = 1 + Math.sin(t * 1.8) * 0.012;
    this.#head.position.y = 1.7 + Math.sin(t * 1.8) * 0.006;
  }

  /**
   * Swing the limbs. Angles in radians; the caller decides what a pose means.
   *
   * Arms rotate about Z (out to the sides, which is what a keeper does) and
   * legs about X (fore and aft, which is what a stride does). Mirrored on the
   * right so that a positive angle means the same thing on both sides.
   */
  setLimbs(leftArm: number, rightArm: number, leftLeg: number, rightLeg: number): void {
    this.#arms[0].rotation.z = leftArm;
    this.#arms[1].rotation.z = -rightArm;
    this.#legs[0].rotation.x = leftLeg;
    this.#legs[1].rotation.x = rightLeg;
  }

  setLean(pitch: number, roll: number): void {
    this.group.rotation.x = pitch;
    this.group.rotation.z = roll;
  }

  dispose(): void {
    for (const d of this.#disposables) d.dispose();
    this.#disposables.length = 0;
  }
}

export class Striker {
  readonly figure: Figure;

  constructor(kit: THREE.ColorRepresentation, accent: THREE.ColorRepresentation) {
    this.figure = new Figure(kit, accent);
    /*
     * The run-up mark, chosen against the lens rather than against the pitch.
     *
     * At 30 degrees the camera cannot hold both a goal worth aiming at and a
     * striker standing next to it — anyone close enough to see is close enough
     * to block. So he waits just outside the left edge and runs into frame,
     * which is what a broadcast penalty looks like anyway.
     */
    this.figure.group.position.set(-1.6, 0, 5.4);
    this.figure.group.rotation.y = Math.PI;
  }

  /** @param runUp 0 at the mark, 1 at contact. */
  setRunUp(runUp: number): void {
    const g = this.figure.group;
    // Approaches the ball along a slight diagonal, as a right-footed kicker
    // does — straight-on run-ups look like a machine.
    g.position.set(-1.6 + runUp * 1.95, 0, 5.4 - runUp * 4.95);

    const stride = Math.sin(runUp * Math.PI * 3.2);
    this.figure.setLimbs(stride * 0.5, -stride * 0.5, stride * 0.9, -stride * 0.9);
    this.figure.setLean(runUp * 0.12, 0);
  }

  /** The strike itself: plant, swing, follow through. */
  setStrike(progress: number): void {
    // A quick leg swing that overshoots and settles — the follow-through is
    // what sells contact more than the contact frame does.
    const swing = Math.sin(Math.min(1, progress) * Math.PI) * 1.5;
    this.figure.setLimbs(0.6, -0.3, -swing, swing * 0.3);
    this.figure.setLean(0.18, -0.1);
  }

  idle(t: number): void {
    this.figure.breathe(t);
    this.figure.setLimbs(0.08, 0.08, 0, 0);
    this.figure.setLean(0, 0);
  }

  dispose(): void {
    this.figure.dispose();
  }
}

export class Keeper {
  readonly figure: Figure;

  constructor(kit: THREE.ColorRepresentation, accent: THREE.ColorRepresentation) {
    this.figure = new Figure(kit, accent);
    this.figure.group.position.set(0, 0, -FIELD.spotToGoal + 0.35);
  }

  /** Shuffling along the line before the whistle. */
  setLine(x: number, t: number): void {
    this.figure.group.position.x = x;
    this.figure.breathe(t);
    // Arms out, weight low: the ready stance is most of what makes a keeper
    // look like a keeper rather than a person standing in a goal.
    this.figure.setLimbs(1.15, 1.15, 0.1, -0.1);
  }

  /**
   * The dive.
   *
   * @param direction which way, or null for a standing block
   * @param extension 0-1, from the keeper's own reach model — so what is drawn
   *   and what is judged come from the same number
   */
  setDive(direction: DiveDirection | null, extension: number, x: number): void {
    const g = this.figure.group;

    if (direction === null) {
      g.position.set(x, 0, -FIELD.spotToGoal + 0.35);
      g.rotation.z = 0;
      this.figure.setLimbs(1.4, 1.4, 0.1, -0.1);
      return;
    }

    const side = direction.startsWith('LEFT') ? -1 : direction.startsWith('RIGHT') ? 1 : 0;
    const high = direction.endsWith('HIGH');

    g.position.set(
      x + side * 2.45 * extension,
      (high ? 1.15 : 0.22) * extension,
      -FIELD.spotToGoal + 0.35,
    );
    // Rotating into the dive is what makes it read as a dive rather than as a
    // figure sliding sideways.
    g.rotation.z = -side * extension * (Math.PI / 2.2);
    this.figure.setLimbs(1.6, 1.6, high ? 0.5 : -0.2, high ? -0.5 : 0.2);
  }

  dispose(): void {
    this.figure.dispose();
  }
}

/** The referee: present for one job, which is the whistle. */
export class Referee {
  readonly group = new THREE.Group();
  readonly #figure: Figure;
  #signal = 0;

  constructor() {
    this.#figure = new Figure(0x14161c, 0xffd23f);
    // Out by the edge of the box. At -5.4 he clipped the left edge of the
    // 30-degree lens and read as an object stuck to the camera.
    this.#figure.group.position.set(-8.2, 0, -5.6);
    this.#figure.group.rotation.y = Math.PI * 0.72;
    this.group.add(this.#figure.group);
  }

  /** @param raised 0-1: arm down to arm up, which is the visual whistle cue. */
  setSignal(raised: number, t: number): void {
    this.#signal = raised;
    this.#figure.breathe(t);
    // One arm straight up. The spec requires a visual cue as well as a sound,
    // for players who cannot hear the whistle.
    this.#figure.setLimbs(0.1, raised * Math.PI * 0.92, 0, 0);
  }

  get signal(): number {
    return this.#signal;
  }

  dispose(): void {
    this.#figure.dispose();
  }
}

/** The ball. */
export class Ball {
  readonly mesh: THREE.Mesh;
  readonly #material: THREE.MeshPhysicalMaterial;
  readonly #geometry: THREE.SphereGeometry;

  constructor() {
    this.#geometry = new THREE.SphereGeometry(FIELD.ballRadius, 32, 24);
    this.#material = new THREE.MeshPhysicalMaterial({
      color: 0xf2f6ff,
      roughness: 0.34,
      clearcoat: 0.7,
      clearcoatRoughness: 0.2,
      emissive: 0x1b3a52,
      emissiveIntensity: 0.25,
    });
    this.mesh = new THREE.Mesh(this.#geometry, this.#material);
    this.mesh.castShadow = true;
    this.reset();
  }

  setPosition(p: Vec3): void {
    this.mesh.position.set(p.x, p.y, p.z);
  }

  /** Rolling rotation, so the ball is visibly spinning rather than sliding. */
  spin(velocity: Vec3, delta: number): void {
    this.mesh.rotation.x -= (velocity.z / FIELD.ballRadius) * delta * 0.35;
    this.mesh.rotation.z += (velocity.x / FIELD.ballRadius) * delta * 0.35;
  }

  reset(): void {
    this.mesh.position.set(0, FIELD.ballRadius, 0);
    this.mesh.rotation.set(0, 0, 0);
  }

  dispose(): void {
    this.#geometry.dispose();
    this.#material.dispose();
  }
}
