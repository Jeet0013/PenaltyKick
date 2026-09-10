/**
 * The goalkeeper: one committed dive, and whether it reaches the ball.
 *
 * ## The shape of the decision
 *
 * The keeper picks a direction and a moment. Both matter, and they trade
 * against each other:
 *
 * - **Dive early** and the body is fully extended by the time the ball
 *   arrives — maximum reach, but the striker can see it and roll the ball the
 *   other way.
 * - **Dive late** and the striker cannot read it, but the keeper is still
 *   extending when the ball crosses, so the reach is short.
 *
 * That trade is the whole skill of keeping, and it is why reach is a function
 * of *time since commitment* rather than a fixed radius per direction.
 *
 * ## Why not a physics engine here either
 *
 * A save is a question asked once, at the instant the ball crosses the goal
 * plane: is any part of the keeper within reach of that point? Simulating
 * limbs would make the answer prettier and non-deterministic. This resolves it
 * with hit volumes evaluated at the crossing moment, so a server re-running
 * the same kick and the same commit gets the same save.
 */

import { FIELD, type Vec3 } from './BallPhysics';

export const DiveDirection = {
  LeftHigh: 'LEFT_HIGH',
  LeftLow: 'LEFT_LOW',
  CentreHigh: 'CENTRE_HIGH',
  CentreLow: 'CENTRE_LOW',
  RightHigh: 'RIGHT_HIGH',
  RightLow: 'RIGHT_LOW',
} as const;
export type DiveDirection = (typeof DiveDirection)[keyof typeof DiveDirection];

export const ALL_DIVES: readonly DiveDirection[] = Object.values(DiveDirection);

/** What the keeper committed to, and when. */
export interface KeeperCommit {
  /** Null when they never committed — they stand up instead. */
  readonly direction: DiveDirection | null;
  /**
   * Seconds after the whistle that the dive was launched.
   *
   * Not seconds before impact: the keeper does not know when impact will be,
   * which is the entire problem they are solving.
   */
  readonly committedAt: number;
  /** Where along the goal line they were standing, in metres from centre. */
  readonly linePosition: number;
}

export const KEEPER = {
  /** How far a fully extended dive reaches from the keeper's standing spot. */
  fullReach: 2.45,
  /** Vertical half-extent of a diving hand's hit volume. */
  handReach: 0.62,
  /** Seconds of extension before a dive is at full stretch. */
  extendSeconds: 0.42,
  /**
   * Reach of a keeper who never committed.
   *
   * Deliberately small. A standing block must beat a shot down the middle and
   * lose to everything else, or not committing becomes the correct play and
   * the keeper stops being a decision.
   */
  standingReach: 0.78,
  standingHeight: 1.95,
  /** How far the keeper may roam from centre before the whistle. */
  lineLimit: 2.6,
} as const;

/** Which way a dive goes, as a sign. Centre is 0. */
function lateral(direction: DiveDirection): -1 | 0 | 1 {
  if (direction.startsWith('LEFT')) return -1;
  if (direction.startsWith('RIGHT')) return 1;
  return 0;
}

/** The height band a dive covers, in metres from the ground. */
function band(direction: DiveDirection): { readonly low: number; readonly high: number } {
  return direction.endsWith('HIGH')
    ? { low: 0.95, high: FIELD.goalHeight + 0.15 }
    : { low: 0, high: 1.15 };
}

/**
 * How far through its extension the dive is when the ball arrives.
 *
 * Eased rather than linear: a keeper leaves the ground quickly and slows as
 * they stretch, so a dive committed slightly late still covers most of the
 * distance. A linear ramp makes late dives uselessly short and pushes every
 * player into diving early, which flattens the whole mechanic.
 */
export function extensionAt(commit: KeeperCommit, ballArrivalTime: number): number {
  if (commit.direction === null) return 0;
  const airborne = ballArrivalTime - commit.committedAt;
  if (airborne <= 0) return 0;
  const t = Math.min(1, airborne / KEEPER.extendSeconds);
  // Ease-out: fast off the mark, slowing into full stretch.
  return 1 - (1 - t) * (1 - t);
}

export interface SaveAttempt {
  readonly saved: boolean;
  /** How close it was, in metres. Drives the near-miss camera and the crowd. */
  readonly margin: number;
  /** Where on the keeper it hit, for the deflection and the particle burst. */
  readonly contact: 'HANDS' | 'BODY' | 'LEGS' | null;
}

/**
 * Does the keeper reach it?
 *
 * @param crossing where the ball crossed the goal plane
 * @param arrivalTime seconds from the whistle to that crossing
 */
export function attemptSave(
  crossing: Vec3,
  commit: KeeperCommit,
  arrivalTime: number,
): SaveAttempt {
  const standing = commit.linePosition;

  if (commit.direction === null) {
    // Standing block: a narrow column where the keeper is.
    const dx = Math.abs(crossing.x - standing);
    const withinHeight = crossing.y <= KEEPER.standingHeight;
    const margin = dx - KEEPER.standingReach;
    return {
      saved: withinHeight && margin <= 0,
      margin,
      contact: withinHeight && margin <= 0 ? (crossing.y < 0.7 ? 'LEGS' : 'BODY') : null,
    };
  }

  const extension = extensionAt(commit, arrivalTime);
  const side = lateral(commit.direction);
  const heights = band(commit.direction);

  // Where the reaching hand is when the ball crosses.
  const handX = standing + side * KEEPER.fullReach * extension;

  // The covered span runs from the keeper's body out to the hand, because the
  // arm is in between — a shot at the keeper's chest during a dive is still a
  // save, which is why this is a span and not a point.
  const near = Math.min(standing, handX);
  const far = Math.max(standing, handX);

  const withinBand = crossing.y >= heights.low - 0.1 && crossing.y <= heights.high;

  let dx: number;
  if (crossing.x < near) dx = near - crossing.x;
  else if (crossing.x > far) dx = crossing.x - far;
  else dx = 0;

  // The hand itself has a hit volume beyond the span's end.
  const margin = dx - KEEPER.handReach * (0.45 + 0.55 * extension);
  const saved = withinBand && margin <= 0;

  let contact: SaveAttempt['contact'] = null;
  if (saved) {
    const towardHand = Math.abs(crossing.x - handX) < KEEPER.handReach;
    contact = towardHand ? 'HANDS' : crossing.y < 0.7 ? 'LEGS' : 'BODY';
  }

  return { saved, margin, contact };
}

/**
 * The dive a given shot would need to be stopped by.
 *
 * Used by the AI to reason about tendencies, and by the practice mode to show
 * what would have worked. Never used to change an outcome after the fact.
 */
export function diveThatCovers(crossing: Vec3): DiveDirection {
  const high = crossing.y >= 1.15;
  if (crossing.x < -0.9) return high ? DiveDirection.LeftHigh : DiveDirection.LeftLow;
  if (crossing.x > 0.9) return high ? DiveDirection.RightHigh : DiveDirection.RightLow;
  return high ? DiveDirection.CentreHigh : DiveDirection.CentreLow;
}
