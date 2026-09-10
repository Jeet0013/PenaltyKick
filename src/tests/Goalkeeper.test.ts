import { describe, expect, it } from 'vitest';

import {
  DiveDirection,
  KEEPER,
  type KeeperCommit,
  attemptSave,
  diveThatCovers,
  extensionAt,
} from '../game/Goalkeeper';

function commit(overrides: Partial<KeeperCommit> = {}): KeeperCommit {
  return { direction: DiveDirection.RightLow, committedAt: 0, linePosition: 0, ...overrides };
}

const at = (x: number, y: number) => ({ x, y, z: -11 });

describe('the early/late trade', () => {
  /*
   * The whole skill of keeping. Diving early gives full extension but is
   * readable; diving late is unreadable but short. If reach did not depend on
   * time, one of the two would simply be correct and the mechanic would die.
   */
  it('reaches further the earlier it commits', () => {
    const early = extensionAt(commit({ committedAt: 0 }), 0.5);
    const late = extensionAt(commit({ committedAt: 0.4 }), 0.5);
    expect(early).toBeGreaterThan(late);
  });

  it('is fully extended given enough time', () => {
    expect(extensionAt(commit({ committedAt: 0 }), KEEPER.extendSeconds + 0.1)).toBe(1);
  });

  it('has no extension before it has left the ground', () => {
    expect(extensionAt(commit({ committedAt: 0.5 }), 0.3)).toBe(0);
  });

  it('eases rather than ramping linearly', () => {
    // Half the extension time should already have covered well over half the
    // distance — otherwise late dives are useless and everyone dives early.
    const half = extensionAt(commit({ committedAt: 0 }), KEEPER.extendSeconds / 2);
    expect(half).toBeGreaterThan(0.6);
  });
});

describe('diving saves', () => {
  it('saves a shot into the corner it dived to', () => {
    const result = attemptSave(at(2.0, 0.6), commit({ direction: DiveDirection.RightLow }), 0.5);
    expect(result.saved).toBe(true);
  });

  it('does not save the opposite corner', () => {
    const result = attemptSave(at(-2.6, 0.6), commit({ direction: DiveDirection.RightLow }), 0.5);
    expect(result.saved).toBe(false);
  });

  it('does not save the right side at the wrong height', () => {
    // Dived low right; the ball went high right.
    const result = attemptSave(at(2.6, 2.2), commit({ direction: DiveDirection.RightLow }), 0.5);
    expect(result.saved).toBe(false);
  });

  it('still saves a shot struck at the body mid-dive', () => {
    // The arm is between the body and the hand, so the span counts, not just
    // the fingertips.
    const result = attemptSave(at(0.4, 0.7), commit({ direction: DiveDirection.RightLow }), 0.5);
    expect(result.saved).toBe(true);
  });

  it('misses a corner it committed to too late', () => {
    /*
     * 2.9 m, not 3.1. A fully extended keeper covers 2.45 m of dive plus a
     * 0.62 m hand — 3.07 m — so a shot at 3.1 beats even a perfect dive, and
     * the test would have proved nothing about timing. Picking the number by
     * eye rather than from the reach model is how a timing test quietly
     * becomes a reach test.
     */
    const far = at(2.9, 0.5);
    const early = attemptSave(far, commit({ direction: DiveDirection.RightLow, committedAt: 0 }), 0.45);
    const late = attemptSave(far, commit({ direction: DiveDirection.RightLow, committedAt: 0.4 }), 0.45);
    expect(early.saved).toBe(true);
    expect(late.saved).toBe(false);
  });

  it('reports where it hit, for the deflection', () => {
    const result = attemptSave(at(2.2, 0.5), commit({ direction: DiveDirection.RightLow }), 0.6);
    expect(result.saved).toBe(true);
    expect(result.contact).not.toBeNull();
  });
});

describe('standing block', () => {
  const standing = commit({ direction: null });

  it('beats a shot down the middle', () => {
    expect(attemptSave(at(0.2, 0.9), standing, 0.5).saved).toBe(true);
  });

  it('loses to a shot into either corner', () => {
    // If a keeper who does nothing can still cover the corners, committing
    // stops being a decision and the whole mechanic collapses.
    expect(attemptSave(at(2.8, 0.6), standing, 0.5).saved).toBe(false);
    expect(attemptSave(at(-2.8, 0.6), standing, 0.5).saved).toBe(false);
  });

  it('loses to a chip over the top', () => {
    expect(attemptSave(at(0, 2.3), standing, 0.5).saved).toBe(false);
  });
});

describe('position on the line', () => {
  it('shifts the whole reach with the keeper', () => {
    const shot = at(2.9, 0.6);
    const centred = attemptSave(shot, commit({ direction: DiveDirection.RightLow, linePosition: 0 }), 0.35);
    const shifted = attemptSave(
      shot,
      commit({ direction: DiveDirection.RightLow, linePosition: 1.2 }),
      0.35,
    );
    // Standing nearer the shot makes it reachable when it otherwise was not.
    expect(shifted.margin).toBeLessThan(centred.margin);
  });
});

describe('determinism', () => {
  it('answers identically for identical inputs', () => {
    const a = attemptSave(at(1.7, 0.8), commit(), 0.44);
    const b = attemptSave(at(1.7, 0.8), commit(), 0.44);
    expect(a).toEqual(b);
  });
});

describe('diveThatCovers', () => {
  it('names the dive a shot needed', () => {
    expect(diveThatCovers(at(-2.5, 0.4))).toBe(DiveDirection.LeftLow);
    expect(diveThatCovers(at(2.5, 2.0))).toBe(DiveDirection.RightHigh);
    expect(diveThatCovers(at(0.1, 0.4))).toBe(DiveDirection.CentreLow);
    expect(diveThatCovers(at(0.1, 2.0))).toBe(DiveDirection.CentreHigh);
  });
});
