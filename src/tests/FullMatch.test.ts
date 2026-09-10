/**
 * A whole match, played headlessly.
 *
 * The unit tests each prove one component. This proves they compose: a match
 * driven only by ticks and strikes reaches a winner, with the scoreline the
 * kicks actually justify.
 *
 * It exists because the browser cannot answer this question cheaply. A hidden
 * tab has `requestAnimationFrame` throttled to nothing, so the running game
 * freezes the moment it loses focus — which is exactly the state an automated
 * screenshot leaves it in. Driving the same state machine from Node removes
 * the browser from the question entirely, which is what the architecture rule
 * about headless rules was for.
 */

import { describe, expect, it } from 'vitest';

import {
  launch,
  Outcome,
  step,
  type BallState,
  type KickInput,
} from '../game/BallPhysics';
import { attemptSave } from '../game/Goalkeeper';
import { decideDive, Difficulty, KeeperTrait, TendencyMemory } from '../game/AiOpponent';
import {
  canStrike,
  MatchPhase,
  MatchStateMachine,
  TIMING,
} from '../game/MatchStateMachine';
import { Stance } from '../game/BallPhysics';
import { KickResult, Phase, Side } from '../game/types';
import { scoreline } from '../game/ShootoutRules';

/** Run a flight to its conclusion at the real fixed step. */
function flyToRest(input: KickInput): BallState {
  let state = launch(input);
  // 1/120 s steps; a penalty is decided in well under three seconds.
  for (let i = 0; i < 600 && state.outcome === Outcome.InFlight; i += 1) {
    state = step(state);
  }
  return state;
}

describe('a full match, headless', () => {
  it('reaches a decided result with a consistent scoreline', () => {
    const machine = new MatchStateMachine(20260910);
    const memory = new TendencyMemory();

    // Alternating targets, so neither the keeper's tendency memory nor the
    // rules get a degenerate all-identical sequence to chew on.
    const targets = [
      { x: 2.6, y: 1.5 },
      { x: -2.7, y: 0.7 },
      { x: 0.2, y: 2.1 },
      { x: -1.9, y: 1.9 },
      { x: 3.1, y: 0.5 },
    ];

    let kicks = 0;
    // Generous: every kick carries ~8.1 s of non-interactive phases, which is
    // ~486 ticks at 1/60, and sudden death can run well past ten kicks.
    let guard = 0;

    while (machine.snapshot.phase !== MatchPhase.Result && guard < 60_000) {
      guard += 1;

      if (!canStrike(machine.snapshot)) {
        // Anything that is merely waiting gets exactly its hold, so no phase
        // is skipped and none is entered twice.
        machine.tick(1 / 60);
        continue;
      }

      const target = targets[kicks % targets.length]!;
      const input: KickInput = {
        targetX: target.x,
        targetY: target.y,
        power: 70,
        curl: kicks % 3 === 0 ? 0.3 : -0.2,
        perfect: false,
        stance: Stance.Driven,
      };

      const decision = decideDive(memory, Difficulty.Normal, KeeperTrait.Patient, machine.rng);
      const flight = flyToRest(input);
      memory.record(input.targetX, input.targetY);

      let result: KickResult;
      if (flight.outcome === Outcome.Goal) {
        const save = attemptSave(flight.position, decision, flight.elapsed);
        result = save.saved ? KickResult.Saved : KickResult.Goal;
      } else if (flight.outcome === Outcome.Woodwork) {
        result = KickResult.Woodwork;
      } else {
        result = KickResult.OffTarget;
      }

      machine.resolveKick({
        striker: machine.striker,
        input,
        keeper: decision,
        outcome: flight.outcome,
        result,
        crossing: { x: flight.position.x, y: flight.position.y },
        saveMargin: 0,
      });
      kicks += 1;
    }

    const { match } = machine.snapshot;

    const score = scoreline(match);

    expect(machine.snapshot.phase).toBe(MatchPhase.Result);
    expect(match.phase).toBe(Phase.Complete);
    // Never ends level: that is the entire point of a shootout.
    expect(score.home).not.toBe(score.away);
    // And the winner the rules named is the one actually ahead.
    expect(match.winner).toBe(score.home > score.away ? Side.Home : Side.Away);

    // Every kick is accounted for, and the score is exactly the goals.
    const goals = match.kicks.filter((k) => k.result === KickResult.Goal).length;
    expect(goals).toBe(score.home + score.away);
    expect(match.kicks.length).toBe(kicks);
    // Five each, unless it clinched early or went to sudden death.
    expect(kicks).toBeGreaterThan(0);
    expect(kicks).toBeLessThanOrEqual(40);
  });

  it('will not let the striker kick before the whistle', () => {
    const machine = new MatchStateMachine(7);
    const seen = new Set<MatchPhase>();

    // Walk to the first live kick, asserting the gate holds the whole way.
    for (let i = 0; i < 2000 && machine.snapshot.phase !== MatchPhase.LiveKick; i += 1) {
      seen.add(machine.snapshot.phase);
      expect(canStrike(machine.snapshot)).toBe(false);
      machine.tick(1 / 60);
    }

    expect(machine.snapshot.phase).toBe(MatchPhase.LiveKick);
    expect(canStrike(machine.snapshot)).toBe(true);
    // The whistle is a phase of its own, not an implicit part of setup.
    expect(seen.has(MatchPhase.RefereeWhistle)).toBe(true);
    expect(seen.has(MatchPhase.CoinToss)).toBe(true);
  });

  it('expires the shot clock into a miss rather than hanging', () => {
    const machine = new MatchStateMachine(11);
    while (machine.snapshot.phase !== MatchPhase.LiveKick) machine.tick(1 / 60);

    // Sit on the ball past the clock.
    for (let t = 0; t < TIMING.shotClock + 1; t += 1 / 60) machine.tick(1 / 60);

    expect(machine.snapshot.phase).not.toBe(MatchPhase.LiveKick);
    expect(machine.snapshot.match.kicks.at(-1)?.result).toBe(KickResult.Expired);
  });
});
