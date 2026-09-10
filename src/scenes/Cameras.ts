/**
 * Camera work.
 *
 * The spec is specific and the numbers matter: 3.5 m behind the ball, 1.6 m
 * above, aimed at goal centre; ease forward on the run-up; follow the ball for
 * 0.8-1.2 s after impact; shake only on a heavy strike and never while aiming.
 *
 * That last clause is the one worth defending. Shake during aiming does not
 * read as power, it reads as an input problem — the player is trying to place
 * a reticle and the world is moving under it.
 */

import * as THREE from 'three';

import { FIELD, type Vec3 } from '../game/BallPhysics';

export const CameraShot = {
  /** Behind the striker, the default. */
  Striker: 'STRIKER',
  /** Behind the goal looking back — what the keeper sees. */
  Keeper: 'KEEPER',
  /** Follows the ball after the strike. */
  Follow: 'FOLLOW',
  /** Wide and high, for the result. */
  Result: 'RESULT',
} as const;
export type CameraShot = (typeof CameraShot)[keyof typeof CameraShot];

const GOAL_CENTRE = new THREE.Vector3(0, FIELD.goalHeight / 2, -FIELD.spotToGoal);

export class Cameras {
  readonly camera: THREE.PerspectiveCamera;
  #shot: CameraShot = CameraShot.Striker;
  #shake = 0;
  readonly #position = new THREE.Vector3();
  readonly #target = new THREE.Vector3();
  readonly #desired = new THREE.Vector3();
  readonly #lookAt = new THREE.Vector3();

  constructor(aspect: number) {
    this.camera = new THREE.PerspectiveCamera(52, aspect, 0.1, 400);
    this.#position.set(0, 1.6, 3.5);
    this.#target.copy(GOAL_CENTRE);
    this.camera.position.copy(this.#position);
    this.camera.lookAt(this.#target);
  }

  setShot(shot: CameraShot): void {
    this.#shot = shot;
  }

  get shot(): CameraShot {
    return this.#shot;
  }

  /**
   * Shake, in metres of displacement.
   *
   * Only ever called on impact. Decays on its own so nothing has to remember
   * to switch it off — a shake that has to be cancelled is a shake that
   * eventually is not.
   */
  impulse(strength: number): void {
    this.#shake = Math.min(0.35, this.#shake + strength);
  }

  resize(aspect: number): void {
    this.camera.aspect = aspect;
    this.camera.updateProjectionMatrix();
  }

  /**
   * @param ball where the ball is, when one is in flight
   * @param runUp 0-1 through the striker's approach, for the ease-in
   */
  update(delta: number, ball: Vec3 | null, runUp = 0): void {
    switch (this.#shot) {
      case CameraShot.Striker:
        // Eases forward during the run-up: 3.5 m back settling toward 2.9.
        this.#desired.set(0, 1.6 - runUp * 0.12, 3.5 - runUp * 0.6);
        this.#lookAt.copy(GOAL_CENTRE);
        break;

      case CameraShot.Keeper:
        // Behind the goal looking back at the striker, so the keeper never
        // sees the reticle — the spec requires that separation.
        this.#desired.set(0, 2.3, -FIELD.spotToGoal - 5.5);
        this.#lookAt.set(0, 1.1, 0);
        break;

      case CameraShot.Follow:
        if (ball) {
          // Trails the ball rather than riding it: a camera locked to a fast
          // object makes the object look stationary and the world look wrong.
          this.#desired.set(ball.x * 0.35, Math.max(1.4, ball.y + 1.0), ball.z + 4.2);
          this.#lookAt.set(ball.x * 0.6, ball.y, ball.z - 1.5);
        }
        break;

      case CameraShot.Result:
        this.#desired.set(4.5, 5.2, -FIELD.spotToGoal + 9);
        this.#lookAt.copy(GOAL_CENTRE);
        break;
    }

    // Frame-rate independent smoothing. `1 - exp(-k*dt)` rather than a fixed
    // lerp factor, so the camera settles at the same rate at 30fps and 144fps.
    const ease = 1 - Math.exp(-6.5 * delta);
    this.#position.lerp(this.#desired, ease);
    this.#target.lerp(this.#lookAt, ease);

    this.camera.position.copy(this.#position);

    if (this.#shake > 0.0005) {
      this.camera.position.x += (Math.random() - 0.5) * this.#shake;
      this.camera.position.y += (Math.random() - 0.5) * this.#shake;
      this.#shake *= Math.exp(-7 * delta);
    } else {
      this.#shake = 0;
    }

    this.camera.lookAt(this.#target);
  }
}
