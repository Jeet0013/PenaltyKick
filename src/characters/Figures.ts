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

/** A jointed figure: torso, head, two arms, two legs. */
class Figure {
  readonly group = new THREE.Group();
  readonly #torso: THREE.Mesh;
  readonly #head: THREE.Mesh;
  readonly #arms: [THREE.Mesh, THREE.Mesh];
  readonly #legs: [THREE.Mesh, THREE.Mesh];
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
    this.#disposables.push(skin, cloth, trim);

    const torsoGeo = new THREE.CapsuleGeometry(0.19, 0.44, 6, 12);
    const headGeo = new THREE.SphereGeometry(0.13, 16, 12);
    const limbGeo = new THREE.CapsuleGeometry(0.062, 0.42, 4, 8);
    this.#disposables.push(torsoGeo, headGeo, limbGeo);

    this.#torso = new THREE.Mesh(torsoGeo, cloth);
    this.#torso.position.y = 1.12;
    this.#torso.castShadow = true;

    this.#head = new THREE.Mesh(headGeo, skin);
    this.#head.position.y = 1.52;
    this.#head.castShadow = true;

    this.#arms = [new THREE.Mesh(limbGeo, trim), new THREE.Mesh(limbGeo, trim)];
    this.#legs = [new THREE.Mesh(limbGeo, cloth), new THREE.Mesh(limbGeo, cloth)];

    for (const [i, arm] of this.#arms.entries()) {
      arm.position.set(i === 0 ? -0.26 : 0.26, 1.16, 0);
      arm.castShadow = true;
      this.group.add(arm);
    }
    for (const [i, leg] of this.#legs.entries()) {
      leg.position.set(i === 0 ? -0.1 : 0.1, 0.55, 0);
      leg.castShadow = true;
      this.group.add(leg);
    }

    this.group.add(this.#torso, this.#head);
  }

  /** Idle breathing, so a waiting figure is not a statue. */
  breathe(t: number): void {
    this.#torso.scale.y = 1 + Math.sin(t * 1.8) * 0.012;
    this.#head.position.y = 1.52 + Math.sin(t * 1.8) * 0.006;
  }

  /** Swing the limbs. Angles in radians; the caller decides what a pose means. */
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
    this.figure.group.position.set(0.35, 0, 2.2);
    this.figure.group.rotation.y = Math.PI;
  }

  /** @param runUp 0 at the mark, 1 at contact. */
  setRunUp(runUp: number): void {
    const g = this.figure.group;
    // Approaches the ball along a slight diagonal, as a right-footed kicker
    // does — straight-on run-ups look like a machine.
    g.position.set(0.35 - runUp * 0.35, 0, 2.2 - runUp * 1.9);

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
    this.#figure.group.position.set(-5.4, 0, -3.2);
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
