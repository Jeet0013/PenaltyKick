/**
 * Ball flight, as a deterministic fixed-step simulation.
 *
 * ## Why this is not a physics engine
 *
 * A penalty is one rigid body on a known trajectory for about a second, hitting
 * at most a post, a bar, a keeper or a net. That is a small enough problem to
 * solve exactly, and solving it exactly buys the one property a general engine
 * cannot promise: **the same kick replays identically, on any machine**.
 *
 * The spec requires it, and the reason is concrete. A server has to be able to
 * re-run a client's kick and get the same answer, or a goal is whatever the
 * client says it is. Rapier is excellent and still the wrong tool here — its
 * results are not guaranteed bit-identical across CPUs and browsers, which is
 * exactly the guarantee this needs.
 *
 * `BallPhysics` is therefore an interface. If Rapier is ever wanted for
 * something richer, it goes behind this same interface and the determinism
 * requirement stays visible at the boundary.
 *
 * ## Units
 *
 * Metres, seconds, kilograms. The penalty spot is the origin, +X is right from
 * the striker, +Y is up, and −Z is toward the goal. A regulation goal is
 * 7.32 m wide and 2.44 m high, 11 m away.
 */

export interface Vec3 {
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

/** Where the flight ended, and why. */
export const Outcome = {
  InFlight: 'IN_FLIGHT',
  Goal: 'GOAL',
  /** Crossed the goal plane outside the frame. */
  Wide: 'WIDE',
  /** Hit a post or the bar. Whether it then goes in is decided afterwards. */
  Woodwork: 'WOODWORK',
  /** Stopped by the keeper's hit volumes. */
  Saved: 'SAVED',
  /** Came to rest without ever reaching the goal plane. */
  Short: 'SHORT',
} as const;
export type Outcome = (typeof Outcome)[keyof typeof Outcome];

export interface BallState {
  readonly position: Vec3;
  readonly velocity: Vec3;
  /** Radians per second about the vertical axis — the source of curl. */
  readonly spin: number;
  readonly outcome: Outcome;
  /** Seconds since the strike. */
  readonly elapsed: number;
}

/** Everything the striker's input decides. No randomness enters after this. */
export interface KickInput {
  /** Where in the goal plane they aimed, in metres from goal centre. */
  readonly targetX: number;
  readonly targetY: number;
  /** 0-100 from the power meter. */
  readonly power: number;
  /** −1 (left) to +1 (right). */
  readonly curl: number;
  /** Whether the strike landed in the 80 ms perfect-contact window. */
  readonly perfect: boolean;
  readonly stance: Stance;
}

export const Stance = {
  Driven: 'DRIVEN',
  Placed: 'PLACED',
  Finesse: 'FINESSE',
  Chip: 'CHIP',
} as const;
export type Stance = (typeof Stance)[keyof typeof Stance];

/**
 * The pitch and the goal, in metres.
 *
 * Regulation, because a goal that is not regulation-sized makes every instinct
 * a player brought with them wrong.
 */
export const FIELD = {
  goalWidth: 7.32,
  goalHeight: 2.44,
  /** Distance from the penalty spot to the goal line. */
  spotToGoal: 11,
  postRadius: 0.06,
  ballRadius: 0.11,
} as const;

export const PHYSICS = {
  /** Fixed step. Everything is integrated at exactly this rate, always. */
  timeStep: 1 / 120,
  gravity: -9.81,
  /**
   * Combined drag coefficient.
   *
   * A real ball's drag varies with speed and surface; this is a single linear
   * term because the difference over an 11 m flight is smaller than the
   * difference a player would notice, and a simpler model is one that replays
   * identically without needing to be careful about it.
   */
  drag: 0.06,
  /**
   * Magnus strength: sideways acceleration per unit of spin per unit of speed.
   *
   * Capped downstream. The spec asks for curl that is "powerful but legible",
   * and an uncapped Magnus term produces shots that bend so late no keeper
   * could read them, which stops being skill and starts being a lottery.
   */
  magnus: 0.00042,
  /** Speed in m/s at full power, before stance adjustment. */
  maxLaunchSpeed: 31,
  /** Restitution when the ball strikes a post or the bar. */
  woodworkBounce: 0.55,
} as const;

/** How each stance shapes the strike. All are available to every player. */
export const STANCES: Record<Stance, {
  readonly speed: number;
  readonly curlScale: number;
  readonly accuracy: number;
}> = {
  // Fast and flat: the keeper has least time, and the smallest curl window.
  [Stance.Driven]: { speed: 1.0, curlScale: 0.45, accuracy: 0.82 },
  // Slower and truer. The keeper gets longer to read it — that is the trade.
  [Stance.Placed]: { speed: 0.78, curlScale: 0.7, accuracy: 1.0 },
  // The most bend, and the easiest to push wide.
  [Stance.Finesse]: { speed: 0.85, curlScale: 1.0, accuracy: 0.88 },
  // High arc. Punishes an early dive; humiliating if underhit.
  [Stance.Chip]: { speed: 0.62, curlScale: 0.55, accuracy: 0.9 },
};

/**
 * Turn a kick into an initial ball state.
 *
 * Everything the player chose is consumed here, and nothing random is added.
 * Once this returns, the flight is fully determined — which is the property
 * that makes a replay a replay rather than a re-roll.
 */
export function launch(input: KickInput): BallState {
  const stance = STANCES[input.stance];

  const power = clamp(input.power, 0, 100) / 100;
  // Perfect contact is a bonus, not a gate: missing it must not feel like a
  // failed input, so it is worth a few percent rather than the whole shot.
  const speed = PHYSICS.maxLaunchSpeed * stance.speed * (0.55 + 0.45 * power) *
    (input.perfect ? 1.06 : 1);

  // Aim at the requested point in the goal plane, then let gravity and drag do
  // what they will — the player aims at a target, not at a launch angle.
  const dz = -FIELD.spotToGoal;
  const dx = input.targetX;
  const flightTime = Math.abs(dz) / Math.max(speed, 1);
  // The vertical velocity that arrives at targetY under gravity.
  const dy = input.targetY - FIELD.ballRadius;
  const vy = (dy - 0.5 * PHYSICS.gravity * flightTime * flightTime) / flightTime;

  const horizontal = Math.hypot(dx, dz);
  const scale = speed / Math.max(horizontal, 0.001);

  return {
    position: { x: 0, y: FIELD.ballRadius, z: 0 },
    velocity: {
      x: dx * scale,
      /*
       * Exactly the velocity that arrives at the aimed height — no stance
       * multiplier.
       *
       * There was one, and it broke aiming: `vy` is *solved* to reach targetY,
       * so scaling it afterwards means the ball goes somewhere else. Aiming at
       * 4.44 m put the ball across at 2.21 m, which is a reticle that lies.
       *
       * The arc still differs by stance, and now for the right reason. A chip
       * flies at 62% speed, so it is in the air far longer, so it needs far
       * more lift to reach the same point — the high arc falls out of the
       * physics instead of being pasted on top of it.
       */
      y: vy,
      z: dz * scale,
    },
    spin: clamp(input.curl, -1, 1) * stance.curlScale * 34,
    outcome: Outcome.InFlight,
    elapsed: 0,
  };
}

/**
 * Advance one fixed step.
 *
 * Never called with a variable delta. A renderer running at 144 Hz and one
 * running at 30 Hz must produce the same flight, so the caller accumulates
 * real time and calls this a whole number of times.
 */
export function step(ball: BallState): BallState {
  if (ball.outcome !== Outcome.InFlight) return ball;

  const dt = PHYSICS.timeStep;
  const v = ball.velocity;
  const speed = Math.hypot(v.x, v.y, v.z);

  // Magnus: perpendicular to travel, in the horizontal plane. Spin about the
  // vertical axis pushes the ball sideways relative to where it is going,
  // which is why a curled ball bends more the faster it is moving.
  const magnus = PHYSICS.magnus * ball.spin * speed;

  const ax = -PHYSICS.drag * v.x + magnus * (-v.z / Math.max(speed, 0.001));
  const ay = PHYSICS.gravity - PHYSICS.drag * v.y;
  const az = -PHYSICS.drag * v.z + magnus * (v.x / Math.max(speed, 0.001));

  const next: BallState = {
    ...ball,
    velocity: { x: v.x + ax * dt, y: v.y + ay * dt, z: v.z + az * dt },
    position: {
      x: ball.position.x + v.x * dt,
      y: ball.position.y + v.y * dt,
      z: ball.position.z + v.z * dt,
    },
    elapsed: ball.elapsed + dt,
  };

  return resolve(ball, next);
}

/**
 * Decide whether this step ended the flight.
 *
 * Takes both the previous and the new state because the goal line is a plane
 * the ball crosses *between* steps — testing only the new position would let a
 * fast shot tunnel straight through it. At 31 m/s a ball travels 26 cm per
 * step, which is more than the width of a post.
 */
function resolve(previous: BallState, next: BallState): BallState {
  const goalZ = -FIELD.spotToGoal;

  // Still short of the goal line.
  if (next.position.z > goalZ) {
    // Rolled to a stop without getting there.
    if (next.position.y <= FIELD.ballRadius && Math.hypot(next.velocity.x, next.velocity.z) < 0.6) {
      return { ...next, outcome: Outcome.Short };
    }
    return next;
  }

  // Crossed the plane this step — interpolate to the exact crossing point
  // rather than using the overshot position.
  const span = previous.position.z - next.position.z;
  const t = span > 0 ? (previous.position.z - goalZ) / span : 0;
  const crossX = previous.position.x + (next.position.x - previous.position.x) * t;
  const crossY = previous.position.y + (next.position.y - previous.position.y) * t;

  const halfWidth = FIELD.goalWidth / 2;
  const inner = halfWidth - FIELD.postRadius;
  const under = FIELD.goalHeight - FIELD.postRadius;

  const at = { ...next, position: { x: crossX, y: crossY, z: goalZ } };

  // The ball is a sphere, so its edge decides, not its centre.
  const edge = FIELD.ballRadius;

  if (Math.abs(crossX) <= inner - edge && crossY <= under - edge && crossY >= edge) {
    return { ...at, outcome: Outcome.Goal };
  }

  // Grazing the frame: inside the outer edge but not clear of it.
  const hitsPost = Math.abs(crossX) <= halfWidth + edge && Math.abs(crossX) >= inner - edge;
  const hitsBar = crossY <= FIELD.goalHeight + edge && crossY >= under - edge;
  if ((hitsPost || hitsBar) && crossY <= FIELD.goalHeight + edge) {
    return {
      ...at,
      outcome: Outcome.Woodwork,
      velocity: {
        x: -next.velocity.x * PHYSICS.woodworkBounce,
        y: next.velocity.y * PHYSICS.woodworkBounce,
        z: -next.velocity.z * PHYSICS.woodworkBounce,
      },
    };
  }

  return { ...at, outcome: Outcome.Wide };
}

/**
 * Run a kick to its conclusion.
 *
 * Used by the tests and by the AI when it wants to know where a shot would
 * end up. Bounded so a bug cannot hang the caller.
 */
export function simulate(input: KickInput, maxSeconds = 4): BallState {
  let ball = launch(input);
  const limit = Math.ceil(maxSeconds / PHYSICS.timeStep);
  for (let i = 0; i < limit && ball.outcome === Outcome.InFlight; i += 1) {
    ball = step(ball);
  }
  return ball;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
