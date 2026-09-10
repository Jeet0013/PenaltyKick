/**
 * The AI, which must feel human rather than psychic.
 *
 * ## The rule that shapes everything here
 *
 * The AI never sees the striker's target. It sees what a person on the goal
 * line sees: what this opponent has *tended* to do, and how they are standing.
 * The spec is explicit — cap prediction, keep a reaction delay, and never
 * change the ball or the result to manufacture difficulty.
 *
 * That last point is worth stating as a prohibition rather than a preference.
 * A game that quietly nudges a shot wide to make Hard feel hard is a game that
 * has stopped being about skill, and players can tell — not from any single
 * kick, but from the shape of a hundred of them.
 *
 * ## How difficulty is expressed
 *
 * Only three dials, all of them honest:
 *
 * - **Prediction cap** — how often the read is even allowed to be right.
 * - **Reaction delay** — how late the dive is committed.
 * - **Aim discipline** — how close its own shots land to what it intended.
 *
 * Nothing else changes. Physics, reach and rules are identical at every level.
 */

import { FIELD, type KickInput, Stance } from './BallPhysics';
import { ALL_DIVES, type DiveDirection, DiveDirection as Dive } from './Goalkeeper';
import type { Rng } from './rng';

export const Difficulty = {
  Easy: 'EASY',
  Normal: 'NORMAL',
  Hard: 'HARD',
} as const;
export type Difficulty = (typeof Difficulty)[keyof typeof Difficulty];

/** A keeper's personality. Cosmetic in effect but real in behaviour. */
export const KeeperTrait = {
  Aggressive: 'AGGRESSIVE',
  Patient: 'PATIENT',
  CornerHunter: 'CORNER_HUNTER',
  CentreGuardian: 'CENTRE_GUARDIAN',
} as const;
export type KeeperTrait = (typeof KeeperTrait)[keyof typeof KeeperTrait];

interface Profile {
  /** Ceiling on how often the tendency read may be followed. Never 1. */
  readonly predictionCap: number;
  /** Seconds after the whistle before it will commit. */
  readonly reactionDelay: number;
  /** Metres of scatter on its own shots. */
  readonly aimScatter: number;
}

const PROFILES: Record<Difficulty, Profile> = {
  // Visible, early dives and forgiving aim, as the spec asks.
  [Difficulty.Easy]: { predictionCap: 0.3, reactionDelay: 0.05, aimScatter: 0.85 },
  [Difficulty.Normal]: { predictionCap: 0.48, reactionDelay: 0.14, aimScatter: 0.5 },
  /*
   * 0.65, not 1. The cap is the honest way to make Hard hard: it reads better,
   * never perfectly. An AI that always guesses right is not difficult, it is
   * broken, and it teaches the player that aiming does not matter.
   */
  [Difficulty.Hard]: { predictionCap: 0.65, reactionDelay: 0.22, aimScatter: 0.28 },
};

const TRAITS: Record<KeeperTrait, { readonly delayScale: number; readonly cornerBias: number }> = {
  [KeeperTrait.Aggressive]: { delayScale: 0.55, cornerBias: 0.15 },
  [KeeperTrait.Patient]: { delayScale: 1.45, cornerBias: 0 },
  [KeeperTrait.CornerHunter]: { delayScale: 0.9, cornerBias: 0.45 },
  [KeeperTrait.CentreGuardian]: { delayScale: 1.1, cornerBias: -0.4 },
};

/**
 * What the AI has noticed.
 *
 * Broad tendencies only — three horizontal buckets and two vertical ones. Not
 * exact coordinates, because a keeper watching a person kick does not learn
 * coordinates, they learn "this one likes their right".
 */
export class TendencyMemory {
  readonly #horizontal = [0, 0, 0]; // left, centre, right
  readonly #vertical = [0, 0]; // low, high
  #total = 0;

  record(targetX: number, targetY: number): void {
    const h = targetX < -1.1 ? 0 : targetX > 1.1 ? 2 : 1;
    const v = targetY >= 1.15 ? 1 : 0;
    this.#horizontal[h] = (this.#horizontal[h] ?? 0) + 1;
    this.#vertical[v] = (this.#vertical[v] ?? 0) + 1;
    this.#total += 1;
  }

  /**
   * The favoured dive, and how strongly it is favoured.
   *
   * Confidence stays low until there is something to be confident about: with
   * one kick observed, a tendency is noise. This is what stops the AI from
   * "reading" a player it has never seen.
   */
  read(): { readonly dive: DiveDirection; readonly confidence: number } {
    if (this.#total === 0) {
      return { dive: Dive.CentreLow, confidence: 0 };
    }

    const hIndex = argmax(this.#horizontal);
    const vIndex = argmax(this.#vertical);
    const high = vIndex === 1;

    const dive =
      hIndex === 0
        ? high ? Dive.LeftHigh : Dive.LeftLow
        : hIndex === 2
          ? high ? Dive.RightHigh : Dive.RightLow
          : high ? Dive.CentreHigh : Dive.CentreLow;

    const share = (this.#horizontal[hIndex] ?? 0) / this.#total;
    // Ramps in over the first few kicks rather than trusting a single sample.
    const sample = Math.min(1, this.#total / 4);
    return { dive, confidence: share * sample };
  }
}

function argmax(values: readonly number[]): number {
  let best = 0;
  for (let i = 1; i < values.length; i += 1) {
    if ((values[i] ?? 0) > (values[best] ?? 0)) best = i;
  }
  return best;
}

export interface AiKeeperDecision {
  readonly direction: DiveDirection;
  readonly committedAt: number;
  readonly linePosition: number;
}

/**
 * Choose a dive.
 *
 * The read is *allowed* to be used only `predictionCap` of the time, and even
 * then only as far as the tendency is actually established. The rest of the
 * time it guesses, weighted by trait.
 */
export function decideDive(
  memory: TendencyMemory,
  difficulty: Difficulty,
  trait: KeeperTrait,
  rng: Rng,
): AiKeeperDecision {
  const profile = PROFILES[difficulty];
  const traits = TRAITS[trait];
  const { dive, confidence } = memory.read();

  const useRead = rng.chance(Math.min(profile.predictionCap, confidence));
  let direction = useRead ? dive : rng.pick(ALL_DIVES);

  // Trait bias: a corner hunter would rather be wrong at a post than right in
  // the middle, and a centre guardian the reverse.
  if (!useRead && traits.cornerBias !== 0) {
    const isCentre = direction.startsWith('CENTRE');
    if (traits.cornerBias > 0 && isCentre && rng.chance(traits.cornerBias)) {
      direction = rng.pick(ALL_DIVES.filter((d) => !d.startsWith('CENTRE')));
    } else if (traits.cornerBias < 0 && !isCentre && rng.chance(-traits.cornerBias)) {
      direction = rng.pick(ALL_DIVES.filter((d) => d.startsWith('CENTRE')));
    }
  }

  // A little jitter so the same difficulty is not the same metronome.
  const committedAt = Math.max(
    0,
    profile.reactionDelay * traits.delayScale + (rng.next() - 0.5) * 0.06,
  );

  return { direction, committedAt, linePosition: (rng.next() - 0.5) * 0.5 };
}

/**
 * Choose a shot.
 *
 * Scatter is applied to the *aim*, before the ball is launched — never to the
 * result afterwards. That distinction is the whole difference between an AI
 * that misses and an AI that is made to miss.
 */
export function decideShot(difficulty: Difficulty, rng: Rng): KickInput {
  const profile = PROFILES[difficulty];

  // Aim for a corner most of the time, as a competent player would.
  const goForCorner = rng.chance(0.72);
  const side = rng.chance(0.5) ? -1 : 1;
  const intendedX = goForCorner ? side * (FIELD.goalWidth / 2 - 0.85) : side * 0.5;
  const intendedY = rng.chance(0.35) ? 1.7 : 0.55;

  const stance = rng.pick([Stance.Driven, Stance.Placed, Stance.Finesse, Stance.Chip]);

  return {
    targetX: intendedX + (rng.next() - 0.5) * 2 * profile.aimScatter,
    targetY: Math.max(0.25, intendedY + (rng.next() - 0.5) * profile.aimScatter),
    power: 62 + rng.next() * 24,
    curl: (rng.next() - 0.5) * 1.4,
    perfect: rng.chance(difficulty === Difficulty.Hard ? 0.45 : 0.2),
    stance,
  };
}

/** Exposed for tests and for a future difficulty screen. */
export function profileFor(difficulty: Difficulty): Profile {
  return PROFILES[difficulty];
}
