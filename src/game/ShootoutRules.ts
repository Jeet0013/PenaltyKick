/**
 * The rules of a penalty shootout, as pure functions.
 *
 * No rendering, no DOM, no timers. Feed it kicks, ask it questions. That is
 * what makes the awkward parts — early clinch and sudden death — testable
 * directly rather than by playing a match and hoping.
 *
 * Every function takes a state and returns a new one. Nothing mutates.
 */

import {
  KICKS_PER_SIDE,
  type KickRecord,
  type KickResult,
  type MatchState,
  Phase,
  Side,
  isGoal,
  otherSide,
} from './types';

export function createMatch(firstKicker: Side): MatchState {
  return { kicks: [], firstKicker, phase: Phase.Standard, winner: null };
}

export function scoreOf(state: MatchState, side: Side): number {
  return state.kicks.filter((k) => k.by === side && isGoal(k.result)).length;
}

export function kicksTakenBy(state: MatchState, side: Side): number {
  return state.kicks.filter((k) => k.by === side).length;
}

/** Kicks a side still has in the standard phase. Zero in sudden death. */
export function kicksRemaining(state: MatchState, side: Side): number {
  if (state.phase !== Phase.Standard) return 0;
  return Math.max(0, KICKS_PER_SIDE - kicksTakenBy(state, side));
}

/**
 * Whose turn it is.
 *
 * Strict alternation from the coin toss winner. In sudden death the same
 * alternation continues, which is why this does not need to know the phase:
 * the side with fewer kicks taken is up, and the first kicker breaks the tie.
 */
export function sideToKick(state: MatchState): Side {
  const first = state.firstKicker;
  const second = otherSide(first);
  return kicksTakenBy(state, first) <= kicksTakenBy(state, second) ? first : second;
}

/**
 * Whether the match is already decided with kicks still to take.
 *
 * The rule people get wrong. A side is safe when its score is beyond what the
 * opponent could reach even by scoring every remaining kick — and the opponent
 * has to be *unable to equal* it, not merely unable to pass it, because a tie
 * would send the match to sudden death rather than ending it.
 *
 * Worth stating plainly: this is checked after every kick, not only at the
 * end. A shootout that runs its full ten kicks when the fourth already settled
 * it is a shootout nobody is watching by the end.
 */
function clinchedBy(state: MatchState): Side | null {
  if (state.phase !== Phase.Standard) return null;

  for (const side of [Side.Home, Side.Away] as const) {
    const them = otherSide(side);
    const bestTheyCanReach = scoreOf(state, them) + kicksRemaining(state, them);
    if (scoreOf(state, side) > bestTheyCanReach) return side;
  }
  return null;
}

/**
 * Whether sudden death has produced a winner.
 *
 * The condition is specific: both sides must have taken the same number of
 * kicks — a completed round — and one must have scored where the other did
 * not. Checking after a single kick would end the match the moment the first
 * player of a round scores, which is not sudden death, it is a coin toss.
 */
function suddenDeathWinner(state: MatchState): Side | null {
  if (state.phase !== Phase.SuddenDeath) return null;

  const homeKicks = kicksTakenBy(state, Side.Home);
  const awayKicks = kicksTakenBy(state, Side.Away);
  if (homeKicks !== awayKicks) return null;
  if (homeKicks <= KICKS_PER_SIDE) return null;

  // Only the round just completed decides it; earlier rounds were level or the
  // match would already be over.
  const round = state.kicks.slice(-2);
  const home = round.find((k) => k.by === Side.Home);
  const away = round.find((k) => k.by === Side.Away);
  if (!home || !away) return null;

  if (isGoal(home.result) && !isGoal(away.result)) return Side.Home;
  if (isGoal(away.result) && !isGoal(home.result)) return Side.Away;
  return null;
}

/** Both sides have used their five, and the score is level. */
function standardPhaseExhausted(state: MatchState): boolean {
  return (
    kicksTakenBy(state, Side.Home) >= KICKS_PER_SIDE &&
    kicksTakenBy(state, Side.Away) >= KICKS_PER_SIDE
  );
}

/**
 * Record a kick and re-derive the match.
 *
 * The order matters: a clinch is checked before the phase advances, because a
 * match decided on the ninth kick must not first be promoted to sudden death
 * and then ended.
 */
export function recordKick(state: MatchState, result: KickResult): MatchState {
  if (state.phase === Phase.Complete) {
    throw new Error('recordKick: the match is already over');
  }

  const by = sideToKick(state);
  const kick: KickRecord = {
    by,
    result,
    ordinal: kicksTakenBy(state, by) + 1,
  };

  const next: MatchState = { ...state, kicks: [...state.kicks, kick] };

  const clinched = clinchedBy(next);
  if (clinched) return { ...next, phase: Phase.Complete, winner: clinched };

  if (next.phase === Phase.Standard && standardPhaseExhausted(next)) {
    const home = scoreOf(next, Side.Home);
    const away = scoreOf(next, Side.Away);
    if (home !== away) {
      return { ...next, phase: Phase.Complete, winner: home > away ? Side.Home : Side.Away };
    }
    // Level after five each: the match continues, one round at a time.
    return { ...next, phase: Phase.SuddenDeath };
  }

  const sudden = suddenDeathWinner(next);
  if (sudden) return { ...next, phase: Phase.Complete, winner: sudden };

  return next;
}

/**
 * The five pips per side the HUD draws.
 *
 * Returned by the rules rather than assembled in the UI, so what is displayed
 * and what is scored cannot drift apart. Sudden-death kicks are appended past
 * the fifth, which is why this is not a fixed-length array.
 */
export type Pip = 'GOAL' | 'MISS' | 'PENDING';

export function pipsFor(state: MatchState, side: Side): readonly Pip[] {
  const taken = state.kicks.filter((k) => k.by === side);
  const pips: Pip[] = taken.map((k) => (isGoal(k.result) ? 'GOAL' : 'MISS'));
  while (pips.length < KICKS_PER_SIDE) pips.push('PENDING');
  return pips;
}

/** Human-readable score, for the result hologram and the HUD. */
export function scoreline(state: MatchState): { home: number; away: number } {
  return { home: scoreOf(state, Side.Home), away: scoreOf(state, Side.Away) };
}
