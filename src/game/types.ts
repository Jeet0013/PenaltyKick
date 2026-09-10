/**
 * The vocabulary the rules are written in.
 *
 * Deliberately free of anything visual. `ShootoutRules` and
 * `MatchStateMachine` must be driveable with no renderer, no DOM and no
 * browser — that is what lets the rules be tested directly, and what will let
 * a server run them later without pretending to have a screen.
 */

/** Which side of the tie a player is on. Not a team identity; a slot. */
export const Side = {
  Home: 'HOME',
  Away: 'AWAY',
} as const;
export type Side = (typeof Side)[keyof typeof Side];

export function otherSide(side: Side): Side {
  return side === Side.Home ? Side.Away : Side.Home;
}

/** What a kick can end as. There is no third possibility by design. */
export const KickResult = {
  Goal: 'GOAL',
  /** The keeper got something to it and it stayed out. */
  Saved: 'SAVED',
  /** Wide, over, or off the frame and away. */
  OffTarget: 'OFF_TARGET',
  /** Struck the post or bar and did not cross the line. */
  Woodwork: 'WOODWORK',
  /** The 8-second clock ran out before the striker committed. */
  Expired: 'EXPIRED',
} as const;
export type KickResult = (typeof KickResult)[keyof typeof KickResult];

/** Exactly one result counts. Everything else is a miss. */
export function isGoal(result: KickResult): boolean {
  return result === KickResult.Goal;
}

/** One completed kick, as the rules see it. */
export interface KickRecord {
  readonly by: Side;
  readonly result: KickResult;
  /** 1-5 in the standard phase; 6+ in sudden death. */
  readonly ordinal: number;
}

export const Phase = {
  Standard: 'STANDARD',
  SuddenDeath: 'SUDDEN_DEATH',
  Complete: 'COMPLETE',
} as const;
export type Phase = (typeof Phase)[keyof typeof Phase];

/**
 * The whole match, as data.
 *
 * Every field is readonly and every transition returns a new object. A rules
 * engine that mutates is a rules engine that cannot be replayed, and replay is
 * what makes a server-authoritative match verifiable.
 */
export interface MatchState {
  readonly kicks: readonly KickRecord[];
  /** Who takes the first kick of the match, from the coin toss. */
  readonly firstKicker: Side;
  readonly phase: Phase;
  /** Set only when the match is over. */
  readonly winner: Side | null;
}

/** How many kicks each side gets before sudden death. */
export const KICKS_PER_SIDE = 5;

/** How long the striker has once the whistle has gone, in seconds. */
export const SHOT_CLOCK_SECONDS = 8;
