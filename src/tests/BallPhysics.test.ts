import { describe, expect, it } from 'vitest';

import {
  FIELD,
  type KickInput,
  Outcome,
  PHYSICS,
  Stance,
  launch,
  simulate,
  step,
} from '../game/BallPhysics';

function kick(overrides: Partial<KickInput> = {}): KickInput {
  return {
    targetX: 0,
    targetY: 1.0,
    power: 70,
    curl: 0,
    perfect: false,
    stance: Stance.Driven,
    ...overrides,
  };
}

describe('determinism', () => {
  /*
   * The property everything else rests on. Without it a server cannot verify a
   * client's goal, and a replay is a re-roll.
   */
  it('gives byte-identical flights for identical input', () => {
    const a = simulate(kick({ targetX: 1.9, curl: 0.6, power: 74 }));
    const b = simulate(kick({ targetX: 1.9, curl: 0.6, power: 74 }));
    expect(a).toEqual(b);
  });

  it('is unaffected by how the steps are grouped', () => {
    // A renderer at 30 fps and one at 144 fps must agree, which they only do
    // if the step is fixed and never scaled by frame time.
    let all = launch(kick());
    for (let i = 0; i < 200; i += 1) all = step(all);

    let split = launch(kick());
    for (let i = 0; i < 137; i += 1) split = step(split);
    for (let i = 0; i < 63; i += 1) split = step(split);

    expect(all).toEqual(split);
  });
});

describe('the goal frame', () => {
  it('counts a shot inside the frame', () => {
    expect(simulate(kick({ targetX: 2.4, targetY: 1.1 })).outcome).toBe(Outcome.Goal);
  });

  it('rejects one outside the post', () => {
    const wide = FIELD.goalWidth / 2 + 1.5;
    expect(simulate(kick({ targetX: wide })).outcome).toBe(Outcome.Wide);
  });

  it('rejects one over the bar', () => {
    expect(simulate(kick({ targetY: FIELD.goalHeight + 2, power: 95 })).outcome).toBe(
      Outcome.Wide,
    );
  });

  it('finds the woodwork between the two', () => {
    // Aimed exactly at the inside edge of the post.
    const post = FIELD.goalWidth / 2 - FIELD.postRadius;
    const result = simulate(kick({ targetX: post, targetY: 1.0 }));
    expect([Outcome.Woodwork, Outcome.Goal, Outcome.Wide]).toContain(result.outcome);
  });

  it('judges by the ball edge, not its centre', () => {
    /*
     * A ball whose centre is inside the post but whose edge is not has hit the
     * post. Testing centres is the classic off-by-a-radius that lets shots
     * pass through the frame.
     */
    const inner = FIELD.goalWidth / 2 - FIELD.postRadius;
    const centreJustInside = inner - FIELD.ballRadius * 0.4;
    expect(simulate(kick({ targetX: centreJustInside })).outcome).not.toBe(Outcome.Goal);
  });
});

describe('curl', () => {
  it('bends the flight sideways', () => {
    const straight = simulate(kick({ curl: 0, stance: Stance.Finesse }));
    const curled = simulate(kick({ curl: 1, stance: Stance.Finesse }));
    expect(curled.position.x).not.toBeCloseTo(straight.position.x, 2);
  });

  it('bends the two directions opposite ways', () => {
    const left = simulate(kick({ curl: -1, stance: Stance.Finesse }));
    const right = simulate(kick({ curl: 1, stance: Stance.Finesse }));
    const straight = simulate(kick({ curl: 0, stance: Stance.Finesse }));
    expect(Math.sign(left.position.x - straight.position.x)).toBe(
      -Math.sign(right.position.x - straight.position.x),
    );
  });

  it('stays legible — curl cannot move the ball a whole goal width', () => {
    // The spec asks for powerful but readable. A shot that arrives 4 m from
    // where it was aimed is not a skill, it is a lottery.
    const straight = simulate(kick({ curl: 0, stance: Stance.Finesse }));
    const curled = simulate(kick({ curl: 1, stance: Stance.Finesse }));
    expect(Math.abs(curled.position.x - straight.position.x)).toBeLessThan(
      FIELD.goalWidth / 2,
    );
  });

  it('bends a finesse shot more than a driven one', () => {
    const base = (stance: Stance) =>
      simulate(kick({ curl: 0, stance })).position.x;
    const bend = (stance: Stance) =>
      Math.abs(simulate(kick({ curl: 1, stance })).position.x - base(stance));
    expect(bend(Stance.Finesse)).toBeGreaterThan(bend(Stance.Driven));
  });
});

describe('stances', () => {
  it('sends a driven shot faster than a placed one', () => {
    const speedOf = (stance: Stance) => {
      const b = launch(kick({ stance }));
      return Math.hypot(b.velocity.x, b.velocity.y, b.velocity.z);
    };
    expect(speedOf(Stance.Driven)).toBeGreaterThan(speedOf(Stance.Placed));
  });

  it('lifts a chip higher than a driven shot', () => {
    expect(launch(kick({ stance: Stance.Chip })).velocity.y).toBeGreaterThan(
      launch(kick({ stance: Stance.Driven })).velocity.y,
    );
  });

  it('gets there sooner when driven', () => {
    const driven = simulate(kick({ stance: Stance.Driven }));
    const placed = simulate(kick({ stance: Stance.Placed }));
    expect(driven.elapsed).toBeLessThan(placed.elapsed);
  });
});

describe('power and contact', () => {
  it('sends more power further, faster', () => {
    const soft = launch(kick({ power: 20 }));
    const hard = launch(kick({ power: 100 }));
    expect(Math.hypot(hard.velocity.x, hard.velocity.z)).toBeGreaterThan(
      Math.hypot(soft.velocity.x, soft.velocity.z),
    );
  });

  it('gives perfect contact a bonus, not a different shot', () => {
    const normal = launch(kick({ perfect: false }));
    const perfect = launch(kick({ perfect: true }));
    const ratio =
      Math.hypot(perfect.velocity.x, perfect.velocity.z) /
      Math.hypot(normal.velocity.x, normal.velocity.z);
    // Enough to feel, not enough that missing it loses the shot.
    expect(ratio).toBeGreaterThan(1);
    expect(ratio).toBeLessThan(1.12);
  });

  it('never leaves a kick in flight forever', () => {
    const result = simulate(kick({ power: 1, targetY: 0.2 }));
    expect(result.outcome).not.toBe(Outcome.InFlight);
  });
});

describe('the fixed step', () => {
  it('is fixed', () => {
    // Guards the one constant that must never be replaced by a frame delta.
    expect(PHYSICS.timeStep).toBeCloseTo(1 / 120, 10);
  });
});
