import { describe, expect, it } from 'vitest';

import {
  Difficulty,
  KeeperTrait,
  TendencyMemory,
  decideDive,
  decideShot,
  profileFor,
} from '../game/AiOpponent';
import { MatchPhase, MatchStateMachine, TIMING, canStrike } from '../game/MatchStateMachine';
import { Rng } from '../game/rng';
import { KickResult } from '../game/types';

describe('match flow', () => {
  it('runs the spec sequence in order', () => {
    const m = new MatchStateMachine(1);
    const seen: MatchPhase[] = [m.snapshot.phase];
    m.onChange((s) => seen.push(s.phase));

    m.advance(); // intro -> coin toss
    m.advance(); // -> kick setup
    m.advance(); // -> whistle
    m.advance(); // -> live

    expect(seen).toEqual([
      MatchPhase.Intro,
      MatchPhase.CoinToss,
      MatchPhase.KickSetup,
      MatchPhase.RefereeWhistle,
      MatchPhase.LiveKick,
    ]);
  });

  it('forbids a kick before the whistle', () => {
    const m = new MatchStateMachine(1);
    // The rule the spec states outright, enforced in exactly one place.
    for (const _ of [0, 1, 2]) {
      expect(canStrike(m.snapshot)).toBe(false);
      m.advance();
    }
    m.advance();
    expect(canStrike(m.snapshot)).toBe(true);
  });

  it('will not resolve a kick that is not live', () => {
    const m = new MatchStateMachine(1);
    expect(() => m.expireShotClock()).not.toThrow();
    expect(m.snapshot.phase).toBe(MatchPhase.Intro);
  });

  it('runs a shot clock only while live', () => {
    const m = new MatchStateMachine(1);
    expect(m.shotClockRemaining).toBeNull();
    for (const _ of [0, 1, 2, 3]) m.advance();
    expect(m.shotClockRemaining).toBeCloseTo(TIMING.shotClock, 5);
  });

  it('records a miss when the clock expires', () => {
    const m = new MatchStateMachine(1);
    for (const _ of [0, 1, 2, 3]) m.advance();
    m.tick(TIMING.shotClock + 0.01);
    expect(m.snapshot.match.kicks).toHaveLength(1);
    expect(m.snapshot.match.kicks[0]?.result).toBe(KickResult.Expired);
    expect(m.snapshot.phase).toBe(MatchPhase.BallResolution);
  });

  it('is reproducible from its seed, including the toss', () => {
    // The property that makes a replay a replay.
    expect(new MatchStateMachine(99).snapshot.match.firstKicker).toBe(
      new MatchStateMachine(99).snapshot.match.firstKicker,
    );
  });
});

describe('AI honesty', () => {
  it('caps prediction below certainty at every difficulty', () => {
    // An AI that always reads right is not difficult, it is broken.
    for (const d of [Difficulty.Easy, Difficulty.Normal, Difficulty.Hard] as const) {
      expect(profileFor(d).predictionCap).toBeLessThan(1);
    }
  });

  it('keeps a reaction delay at every difficulty', () => {
    for (const d of [Difficulty.Easy, Difficulty.Normal, Difficulty.Hard] as const) {
      expect(profileFor(d).reactionDelay).toBeGreaterThan(0);
    }
  });

  it('reacts later and aims truer as difficulty rises', () => {
    expect(profileFor(Difficulty.Hard).reactionDelay).toBeGreaterThan(
      profileFor(Difficulty.Easy).reactionDelay,
    );
    expect(profileFor(Difficulty.Hard).aimScatter).toBeLessThan(
      profileFor(Difficulty.Easy).aimScatter,
    );
  });

  it('has no confidence about a player it has never seen', () => {
    expect(new TendencyMemory().read().confidence).toBe(0);
  });

  it('grows confidence only as a tendency establishes itself', () => {
    const memory = new TendencyMemory();
    memory.record(2.6, 0.5);
    const afterOne = memory.read().confidence;
    for (let i = 0; i < 5; i += 1) memory.record(2.6, 0.5);
    expect(memory.read().confidence).toBeGreaterThan(afterOne);
  });

  it('does not read a first-time opponent even on Hard', () => {
    /*
     * The measurable version of "not psychic". With no history, dives should
     * be spread across the six options rather than landing on one.
     */
    const rng = new Rng(7);
    const memory = new TendencyMemory();
    const counts = new Map<string, number>();
    for (let i = 0; i < 240; i += 1) {
      const d = decideDive(memory, Difficulty.Hard, KeeperTrait.Patient, rng);
      counts.set(d.direction, (counts.get(d.direction) ?? 0) + 1);
    }
    expect(counts.size).toBeGreaterThan(3);
    expect(Math.max(...counts.values())).toBeLessThan(160);
  });

  it('is reproducible from the seed', () => {
    const run = () => {
      const rng = new Rng(31);
      const memory = new TendencyMemory();
      return [0, 1, 2].map(() => decideDive(memory, Difficulty.Normal, KeeperTrait.Aggressive, rng));
    };
    expect(run()).toEqual(run());
  });

  it('scatters its aim, never its result', () => {
    // decideShot returns an aim. Nothing downstream may alter the outcome, and
    // the only way to keep that true is for the AI to have no say after launch.
    const rng = new Rng(5);
    const shot = decideShot(Difficulty.Easy, rng);
    expect(shot).toHaveProperty('targetX');
    expect(shot).toHaveProperty('power');
    expect(Object.keys(shot)).not.toContain('forceOutcome');
  });
});
