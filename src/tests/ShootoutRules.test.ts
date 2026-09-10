import { describe, expect, it } from 'vitest';

import {
  createMatch,
  kicksRemaining,
  pipsFor,
  recordKick,
  scoreOf,
  sideToKick,
} from '../game/ShootoutRules';
import { KickResult, type MatchState, Phase, Side } from '../game/types';

const GOAL = KickResult.Goal;
const MISS = KickResult.Saved;

/** Play a sequence of results in turn order. */
function play(results: readonly KickResult[], firstKicker: Side = Side.Home): MatchState {
  return results.reduce<MatchState>(
    (state, result) => recordKick(state, result),
    createMatch(firstKicker),
  );
}

describe('turn order', () => {
  it('starts with whoever won the toss', () => {
    expect(sideToKick(createMatch(Side.Away))).toBe(Side.Away);
  });

  it('alternates strictly', () => {
    let state = createMatch(Side.Home);
    const order: Side[] = [];
    for (let i = 0; i < 6; i += 1) {
      order.push(sideToKick(state));
      state = recordKick(state, MISS);
    }
    expect(order).toEqual([
      Side.Home,
      Side.Away,
      Side.Home,
      Side.Away,
      Side.Home,
      Side.Away,
    ]);
  });
});

describe('what counts', () => {
  it('scores a goal and nothing else', () => {
    // Every non-goal result must be worth exactly zero — this is the one place
    // a "nearly" could creep in.
    for (const result of [
      KickResult.Saved,
      KickResult.OffTarget,
      KickResult.Woodwork,
      KickResult.Expired,
    ] as const) {
      const state = play([result]);
      expect(scoreOf(state, Side.Home)).toBe(0);
    }
    expect(scoreOf(play([GOAL]), Side.Home)).toBe(1);
  });

  it('counts an expired shot clock as a miss, not a replay', () => {
    const state = play([KickResult.Expired]);
    expect(scoreOf(state, Side.Home)).toBe(0);
    // And the turn passes: a player who stalls does not get another go.
    expect(sideToKick(state)).toBe(Side.Away);
  });
});

describe('a full five each', () => {
  it('declares the higher score the winner', () => {
    /*
     * Home 3, Away 2, and never mathematically clinched before the tenth kick.
     *
     * The sequence matters: after Home's fifth (3-2 with one Away kick left)
     * Away can still reach 3 and force sudden death, so the match must run to
     * the end. Getting this wrong is easy — the first draft of this test was
     * 2-2 and correctly failed.
     */
    const state = play([
      GOAL, GOAL,
      GOAL, GOAL,
      MISS, MISS,
      MISS, MISS,
      GOAL, MISS,
    ]);
    expect(state.phase).toBe(Phase.Complete);
    expect(state.winner).toBe(Side.Home);
    expect(scoreOf(state, Side.Home)).toBe(3);
    expect(scoreOf(state, Side.Away)).toBe(2);
  });

  it('goes to sudden death when level', () => {
    const state = play([
      GOAL, GOAL,
      GOAL, GOAL,
      MISS, MISS,
      MISS, MISS,
      MISS, MISS,
    ]);
    expect(state.phase).toBe(Phase.SuddenDeath);
    expect(state.winner).toBeNull();
  });
});

describe('early clinch', () => {
  /*
   * The rule implementations get wrong. A side is safe only when the other
   * cannot even *equal* it — equalling would mean sudden death, not defeat.
   */
  it('ends as soon as the trailing side cannot catch up', () => {
    // Home scores three, Away misses three. Away has two left and trails by
    // three: 0 + 2 < 3, so it is over on the sixth kick.
    const state = play([GOAL, MISS, GOAL, MISS, GOAL, MISS]);
    expect(state.phase).toBe(Phase.Complete);
    expect(state.winner).toBe(Side.Home);
    expect(state.kicks).toHaveLength(6);
  });

  it('does NOT end when the trailing side could still draw level', () => {
    // Home 3, Away 1 with two kicks left: Away can reach 3 and force sudden
    // death, so the match must continue.
    const state = play([GOAL, GOAL, GOAL, MISS, GOAL, MISS]);
    expect(scoreOf(state, Side.Home)).toBe(3);
    expect(scoreOf(state, Side.Away)).toBe(1);
    expect(kicksRemaining(state, Side.Away)).toBe(2);
    expect(state.phase).toBe(Phase.Standard);
    expect(state.winner).toBeNull();
  });

  it('can clinch for the side kicking second', () => {
    // Home misses everything; Away scores its first three. After Away's third,
    // Home has two kicks and trails by three.
    const state = play([MISS, GOAL, MISS, GOAL, MISS, GOAL]);
    expect(state.phase).toBe(Phase.Complete);
    expect(state.winner).toBe(Side.Away);
  });

  it('never clinches on the very first kick', () => {
    const state = play([GOAL]);
    expect(state.phase).toBe(Phase.Standard);
    expect(state.winner).toBeNull();
  });
});

describe('sudden death', () => {
  const level = (): MatchState =>
    play([GOAL, GOAL, GOAL, GOAL, MISS, MISS, MISS, MISS, MISS, MISS]);

  it('does not end mid-round when the first kicker scores', () => {
    // The failure that turns sudden death into a coin toss.
    const state = recordKick(level(), GOAL);
    expect(state.phase).toBe(Phase.SuddenDeath);
    expect(state.winner).toBeNull();
  });

  it('ends when one scores and the other misses in the same round', () => {
    let state = recordKick(level(), GOAL);
    state = recordKick(state, MISS);
    expect(state.phase).toBe(Phase.Complete);
    expect(state.winner).toBe(Side.Home);
  });

  it('continues when both score', () => {
    let state = recordKick(level(), GOAL);
    state = recordKick(state, GOAL);
    expect(state.phase).toBe(Phase.SuddenDeath);
    expect(state.winner).toBeNull();
  });

  it('continues when both miss', () => {
    let state = recordKick(level(), MISS);
    state = recordKick(state, MISS);
    expect(state.phase).toBe(Phase.SuddenDeath);
    expect(state.winner).toBeNull();
  });

  it('survives several level rounds and then decides', () => {
    let state = level();
    for (let round = 0; round < 4; round += 1) {
      state = recordKick(state, GOAL);
      state = recordKick(state, GOAL);
      expect(state.phase).toBe(Phase.SuddenDeath);
    }
    state = recordKick(state, MISS);
    state = recordKick(state, GOAL);
    expect(state.winner).toBe(Side.Away);
  });

  it('lets the side that kicked second in the round win', () => {
    let state = recordKick(level(), MISS);
    state = recordKick(state, GOAL);
    expect(state.winner).toBe(Side.Away);
  });
});

describe('score pips', () => {
  it('shows five, filling from pending', () => {
    expect(pipsFor(createMatch(Side.Home), Side.Home)).toEqual([
      'PENDING', 'PENDING', 'PENDING', 'PENDING', 'PENDING',
    ]);
  });

  it('records goals and misses in order', () => {
    const state = play([GOAL, MISS, MISS, GOAL]);
    expect(pipsFor(state, Side.Home)).toEqual([
      'GOAL', 'MISS', 'PENDING', 'PENDING', 'PENDING',
    ]);
    expect(pipsFor(state, Side.Away)).toEqual([
      'MISS', 'GOAL', 'PENDING', 'PENDING', 'PENDING',
    ]);
  });
});

describe('a finished match is finished', () => {
  it('refuses further kicks', () => {
    const state = play([GOAL, MISS, GOAL, MISS, GOAL, MISS]);
    expect(state.phase).toBe(Phase.Complete);
    expect(() => recordKick(state, GOAL)).toThrow(/already over/);
  });
});
