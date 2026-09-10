/**
 * The flow of a match, as an explicit state machine.
 *
 * `INTRO → COIN_TOSS → KICK_SETUP → REFEREE_WHISTLE → LIVE_KICK →
 *  BALL_RESOLUTION → REACTION → SCORE_UPDATE → ROLE_SWAP → … → RESULT`
 *
 * ## Why a machine rather than a pile of booleans
 *
 * Two rules in the spec are only enforceable if there is one place that knows
 * what phase the match is in:
 *
 * - **The striker cannot kick before the whistle.** With flags scattered
 *   through an input handler, "has the whistle gone?" is asked in several
 *   places and eventually one of them is wrong.
 * - **Only the server may advance state online.** That is only possible if
 *   advancing is a single named operation rather than something that happens
 *   implicitly when a timer fires.
 *
 * The machine holds no Three.js and no DOM. It emits phases; the renderer
 * reacts to them.
 */

import type { KickInput, Outcome } from './BallPhysics';
import type { KeeperCommit } from './Goalkeeper';
import { Rng } from './rng';
import { createMatch, recordKick, sideToKick } from './ShootoutRules';
import {
  KickResult,
  type MatchState,
  Phase,
  SHOT_CLOCK_SECONDS,
  type Side,
} from './types';

export const MatchPhase = {
  Intro: 'INTRO',
  CoinToss: 'COIN_TOSS',
  KickSetup: 'KICK_SETUP',
  RefereeWhistle: 'REFEREE_WHISTLE',
  LiveKick: 'LIVE_KICK',
  BallResolution: 'BALL_RESOLUTION',
  Reaction: 'REACTION',
  ScoreUpdate: 'SCORE_UPDATE',
  RoleSwap: 'ROLE_SWAP',
  Result: 'RESULT',
} as const;
export type MatchPhase = (typeof MatchPhase)[keyof typeof MatchPhase];

/** How long each non-interactive phase holds, in seconds. */
export const TIMING = {
  intro: 2.2,
  coinToss: 2.0,
  kickSetup: 1.2,
  whistle: 0.7,
  /** The striker's clock, from the spec. */
  shotClock: SHOT_CLOCK_SECONDS,
  /** Camera follows the ball after the strike. */
  resolution: 1.1,
  /** The Neon Goal Surge window. Never longer, or the match stops breathing. */
  reaction: 3.4,
  scoreUpdate: 0.8,
  roleSwap: 0.9,
} as const;

export interface KickAttempt {
  readonly striker: Side;
  readonly input: KickInput;
  readonly keeper: KeeperCommit;
  readonly outcome: Outcome;
  readonly result: KickResult;
  /** Where it crossed the goal plane, for the replay camera. */
  readonly crossing: { readonly x: number; readonly y: number };
  readonly saveMargin: number;
}

export interface MatchSnapshot {
  readonly phase: MatchPhase;
  readonly match: MatchState;
  /** Seconds spent in the current phase. */
  readonly elapsed: number;
  /** The kick just resolved, for the reaction and the replay. */
  readonly lastKick: KickAttempt | null;
}

/**
 * Drives one match.
 *
 * `advance` is the only way a phase changes, and it is deliberately explicit:
 * a network transport can call it from an authoritative server message instead
 * of from a local timer, without the rest of the game noticing.
 */
export class MatchStateMachine {
  #phase: MatchPhase = MatchPhase.Intro;
  #match: MatchState;
  #elapsed = 0;
  #lastKick: KickAttempt | null = null;
  readonly #rng: Rng;
  readonly #listeners = new Set<(snapshot: MatchSnapshot) => void>();

  constructor(seed: number) {
    this.#rng = new Rng(seed);
    // The toss is the first thing the seed decides, so a replay reproduces even
    // who kicked first.
    this.#match = createMatch(this.#rng.chance(0.5) ? 'HOME' : 'AWAY');
  }

  get snapshot(): MatchSnapshot {
    return {
      phase: this.#phase,
      match: this.#match,
      elapsed: this.#elapsed,
      lastKick: this.#lastKick,
    };
  }

  get rng(): Rng {
    return this.#rng;
  }

  /** Who is taking this kick. */
  get striker(): Side {
    return sideToKick(this.#match);
  }

  onChange(listener: (snapshot: MatchSnapshot) => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  /**
   * Feed real time in.
   *
   * Only the phases that are *waiting* advance on their own. `LiveKick` never
   * does — it ends when the striker acts or the shot clock expires, and both
   * of those are decisions, not the passage of time.
   */
  tick(deltaSeconds: number): void {
    this.#elapsed += deltaSeconds;

    const hold = this.#holdFor(this.#phase);
    if (hold !== null && this.#elapsed >= hold) this.advance();
  }

  /** Seconds left on the striker's clock, or null when it is not running. */
  get shotClockRemaining(): number | null {
    if (this.#phase !== MatchPhase.LiveKick) return null;
    return Math.max(0, TIMING.shotClock - this.#elapsed);
  }

  /**
   * Resolve a kick.
   *
   * The one place a result enters the match. Called with an already-simulated
   * outcome rather than simulating here, so the same method serves a local
   * kick, an AI kick and — later — a server-validated one.
   */
  resolveKick(attempt: KickAttempt): void {
    if (this.#phase !== MatchPhase.LiveKick) {
      throw new Error(`resolveKick: not live (phase is ${this.#phase})`);
    }
    this.#lastKick = attempt;
    this.#match = recordKick(this.#match, attempt.result);
    this.#enter(MatchPhase.BallResolution);
  }

  /** The striker let the clock run out. A miss, and the turn passes. */
  expireShotClock(): void {
    if (this.#phase !== MatchPhase.LiveKick) return;
    this.#match = recordKick(this.#match, KickResult.Expired);
    this.#lastKick = null;
    this.#enter(MatchPhase.BallResolution);
  }

  /** Move to the next phase. The only transition function. */
  advance(): void {
    switch (this.#phase) {
      case MatchPhase.Intro:
        return this.#enter(MatchPhase.CoinToss);
      case MatchPhase.CoinToss:
      case MatchPhase.RoleSwap:
        return this.#enter(MatchPhase.KickSetup);
      case MatchPhase.KickSetup:
        return this.#enter(MatchPhase.RefereeWhistle);
      case MatchPhase.RefereeWhistle:
        return this.#enter(MatchPhase.LiveKick);
      case MatchPhase.LiveKick:
        // Time alone cannot end a live kick; only a shot or an expiry can.
        return this.expireShotClock();
      case MatchPhase.BallResolution:
        return this.#enter(MatchPhase.Reaction);
      case MatchPhase.Reaction:
        return this.#enter(MatchPhase.ScoreUpdate);
      case MatchPhase.ScoreUpdate:
        return this.#enter(
          this.#match.phase === Phase.Complete ? MatchPhase.Result : MatchPhase.RoleSwap,
        );
      case MatchPhase.Result:
        return;
    }
  }

  /** How long a phase waits before advancing itself, or null if it waits for input. */
  #holdFor(phase: MatchPhase): number | null {
    switch (phase) {
      case MatchPhase.Intro: return TIMING.intro;
      case MatchPhase.CoinToss: return TIMING.coinToss;
      case MatchPhase.KickSetup: return TIMING.kickSetup;
      case MatchPhase.RefereeWhistle: return TIMING.whistle;
      // The clock is a hold, but expiring is a rules event, not a transition.
      case MatchPhase.LiveKick: return TIMING.shotClock;
      case MatchPhase.BallResolution: return TIMING.resolution;
      case MatchPhase.Reaction: return TIMING.reaction;
      case MatchPhase.ScoreUpdate: return TIMING.scoreUpdate;
      case MatchPhase.RoleSwap: return TIMING.roleSwap;
      case MatchPhase.Result: return null;
    }
  }

  #enter(phase: MatchPhase): void {
    this.#phase = phase;
    this.#elapsed = 0;
    for (const listener of this.#listeners) listener(this.snapshot);
  }
}

/**
 * Whether the striker is allowed to strike.
 *
 * Exported so the input layer asks the machine rather than keeping its own
 * copy of the answer. The spec is explicit that the whistle gates the kick,
 * and this is the single place that decides it.
 */
export function canStrike(snapshot: MatchSnapshot): boolean {
  return snapshot.phase === MatchPhase.LiveKick;
}
