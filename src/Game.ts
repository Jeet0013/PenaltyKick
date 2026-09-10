/**
 * The game: everything above joined up.
 *
 * ## What this file is allowed to do
 *
 * Wire things together and translate input. It holds no rules — those are in
 * `ShootoutRules`, which does not import from here and never will. That
 * separation is what lets the same rules run on a server later, and it is
 * enforced by direction: `game/` never imports `scenes/`, `ui/` or this file.
 *
 * ## The loop
 *
 * Rendering runs at whatever rate the browser offers. The ball does not: real
 * time is accumulated and spent in fixed 1/120 s steps, so a flight is
 * identical at 30 fps and at 144 fps. A frame-scaled physics step would make
 * the same kick score on one machine and miss on another.
 */

import * as THREE from 'three';

import {
  type BallState,
  FIELD,
  type KickInput,
  Outcome,
  PHYSICS,
  Stance,
  launch,
  step,
} from './game/BallPhysics';
import {
  Difficulty,
  KeeperTrait,
  TendencyMemory,
  decideDive,
} from './game/AiOpponent';
import { type KeeperCommit, attemptSave, extensionAt } from './game/Goalkeeper';
import { MatchPhase, MatchStateMachine, canStrike } from './game/MatchStateMachine';
import { pipsFor, scoreline } from './game/ShootoutRules';
import { SLICE_TEAMS } from './game/Teams';
import { KickResult, Side } from './game/types';
import { Ball, Keeper, Referee, Striker } from './characters/Figures';
import { Arena } from './scenes/Arena';
import { CameraShot, Cameras } from './scenes/Cameras';
import { Lighting } from './scenes/Lighting';
import { QUALITY, type Quality, detectQuality } from './scenes/quality';
import { Hud } from './ui/Hud';
import { Celebration } from './ui/Celebration';
import { Reticle } from './ui/Reticle';
import { AudioCues } from './audio/AudioCues';

/** How the player is holding the power meter. */
interface Charge {
  readonly startedAt: number;
  readonly originX: number;
  power: number;
  curl: number;
}

export class Game {
  readonly #renderer: THREE.WebGLRenderer;
  readonly #scene = new THREE.Scene();
  readonly #cameras: Cameras;
  readonly #arena: Arena;
  readonly #lighting: Lighting;
  readonly #hud: Hud;
  readonly #reticle: Reticle;
  readonly #celebration: Celebration;
  readonly #audio: AudioCues;

  readonly #ball = new Ball();
  readonly #striker: Striker;
  readonly #keeper: Keeper;
  readonly #referee = new Referee();

  readonly #match: MatchStateMachine;
  readonly #memory = new TendencyMemory();

  #quality: Quality;
  #stance: Stance = Stance.Driven;
  #aim = { x: 0, y: 1.1 };
  #charge: Charge | null = null;
  #flight: BallState | null = null;
  #accumulator = 0;
  #last = 0;
  #running = false;
  #keeperCommit: KeeperCommit | null = null;
  #frame = 0;

  constructor(container: HTMLElement, seed = Date.now() >>> 0) {
    this.#quality = detectQuality();
    const preset = QUALITY[this.#quality];

    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'display:block;width:100%;height:100%;touch-action:none';
    container.append(canvas);

    this.#renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: preset.antialias,
      powerPreference: 'high-performance',
      alpha: false,
    });
    // Physically-correct pipeline. Without it the emissive trim and the wet
    // turf blow out to flat white under the floodlight.
    this.#renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.#renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.#renderer.toneMappingExposure = 1.08;
    this.#renderer.shadowMap.enabled = preset.shadows;
    this.#renderer.shadowMap.type = THREE.PCFShadowMap;

    this.#scene.background = new THREE.Color(0x05070d);
    this.#scene.fog = new THREE.Fog(0x05070d, 40, 150);

    this.#cameras = new Cameras(container.clientWidth / Math.max(1, container.clientHeight));

    this.#lighting = new Lighting(this.#quality);
    this.#lighting.applyEnvironment(this.#renderer, this.#scene, this.#quality);
    this.#scene.add(this.#lighting.group);

    this.#arena = new Arena(this.#quality, {
      home: SLICE_TEAMS.home.primary,
      away: SLICE_TEAMS.away.primary,
    });
    this.#scene.add(this.#arena.group);

    this.#striker = new Striker(SLICE_TEAMS.home.primary, SLICE_TEAMS.home.secondary);
    this.#keeper = new Keeper(SLICE_TEAMS.away.primary, SLICE_TEAMS.away.secondary);
    this.#scene.add(
      this.#striker.figure.group,
      this.#keeper.figure.group,
      this.#referee.group,
      this.#ball.mesh,
    );

    this.#match = new MatchStateMachine(seed);
    this.#hud = new Hud(container, { home: SLICE_TEAMS.home, away: SLICE_TEAMS.away });
    this.#reticle = new Reticle(container);
    this.#celebration = new Celebration(container);
    this.#audio = new AudioCues();

    this.#hud.onStanceChange((stance) => {
      this.#stance = stance;
      this.#hud.setStance(stance);
    });

    this.#bindInput(canvas);
    this.#match.onChange((snapshot) => this.#onPhase(snapshot.phase));
    this.#resize(container);
    window.addEventListener('resize', () => this.#resize(container));
  }

  start(): void {
    if (this.#running) return;
    this.#running = true;
    this.#last = performance.now();
    this.#loop();
  }

  get quality(): Quality {
    return this.#quality;
  }

  // ── Input ────────────────────────────────────────────────────────────────

  /**
   * Aim by dragging, power by holding, curl by drifting sideways while held.
   *
   * One gesture does all three, which is what makes it work on a phone: press
   * where you want it, slide to bend it, release to hit it.
   */
  #bindInput(canvas: HTMLCanvasElement): void {
    canvas.addEventListener('pointerdown', (event) => {
      void this.#audio.unlock();
      if (!canStrike(this.#match.snapshot)) return;

      this.#aimFrom(event, canvas);
      this.#charge = {
        startedAt: performance.now(),
        originX: event.clientX,
        power: 0,
        curl: 0,
      };
      canvas.setPointerCapture(event.pointerId);
    });

    canvas.addEventListener('pointermove', (event) => {
      if (this.#charge) {
        // Sideways drift while charging is curl — the ball bends the way the
        // finger moved, which is the only mapping anyone guesses right.
        const drift = (event.clientX - this.#charge.originX) / (canvas.clientWidth * 0.25);
        this.#charge.curl = Math.max(-1, Math.min(1, drift));
        this.#hud.setCurl(this.#charge.curl);
        return;
      }
      if (canStrike(this.#match.snapshot)) this.#aimFrom(event, canvas);
    });

    const release = (): void => {
      if (!this.#charge) return;
      const charge = this.#charge;
      this.#charge = null;
      this.#strike(charge);
    };

    canvas.addEventListener('pointerup', release);
    canvas.addEventListener('pointercancel', release);

    window.addEventListener('keydown', (event) => {
      const map: Record<string, Stance> = { '1': Stance.Driven, '2': Stance.Placed, '3': Stance.Finesse, '4': Stance.Chip };
      const stance = map[event.key];
      if (stance) {
        this.#stance = stance;
        this.#hud.setStance(stance);
      }
    });
  }

  /** Screen point to a spot in the goal plane. */
  #aimFrom(event: PointerEvent, canvas: HTMLCanvasElement): void {
    const rect = canvas.getBoundingClientRect();
    const nx = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const ny = ((event.clientY - rect.top) / rect.height) * 2 - 1;

    // Clamped a little beyond the frame so the player can genuinely miss —
    // a reticle that cannot leave the goal makes aiming meaningless.
    const half = FIELD.goalWidth / 2 + 0.8;
    this.#aim = {
      x: Math.max(-half, Math.min(half, nx * half)),
      y: Math.max(0.15, Math.min(FIELD.goalHeight + 0.9, (1 - (ny + 1) / 2) * (FIELD.goalHeight + 1.2))),
    };
    this.#reticle.setTarget(this.#aim.x, this.#aim.y);
  }

  /** Release: build the kick, commit the keeper, simulate, resolve. */
  #strike(charge: Charge): void {
    const snapshot = this.#match.snapshot;
    if (!canStrike(snapshot)) return;

    const input: KickInput = {
      targetX: this.#aim.x,
      targetY: this.#aim.y,
      power: charge.power,
      curl: charge.curl,
      // The 80 ms window from the spec, measured against the meter's sweep.
      perfect: charge.power >= 58 && charge.power <= 82 && Math.abs(charge.curl) < 0.85,
      stance: this.#stance,
    };

    // The AI keeper decides now, from tendencies only — it has never seen the
    // reticle, and this is the moment that fact is enforced.
    const decision = decideDive(this.#memory, Difficulty.Normal, KeeperTrait.Patient, this.#match.rng);
    this.#keeperCommit = {
      direction: decision.direction,
      committedAt: decision.committedAt,
      linePosition: decision.linePosition,
    };

    this.#memory.record(input.targetX, input.targetY);
    this.#flight = launch(input);
    this.#audio.boot(charge.power);
    this.#cameras.impulse(charge.power / 480);
    this.#cameras.setShot(CameraShot.Follow);
    this.#hud.setPrompt('');
  }

  // ── Phases ───────────────────────────────────────────────────────────────

  #onPhase(phase: MatchPhase): void {
    switch (phase) {
      case MatchPhase.CoinToss: {
        const first = this.#match.snapshot.match.firstKicker;
        const name = first === Side.Home ? SLICE_TEAMS.home.name : SLICE_TEAMS.away.name;
        this.#hud.say(`Coin toss — ${name} kick first.`);
        break;
      }
      case MatchPhase.KickSetup:
        this.#ball.reset();
        this.#flight = null;
        this.#keeperCommit = null;
        this.#reticle.setVisible(true);
        this.#cameras.setShot(CameraShot.Striker);
        this.#hud.setPrompt('Prepare');
        this.#hud.say('');
        break;
      case MatchPhase.RefereeWhistle:
        this.#hud.setPrompt('Prepare');
        this.#hud.say('Referee: whistle.');
        this.#audio.whistle();
        break;
      case MatchPhase.LiveKick:
        this.#hud.setPrompt('Kick live');
        break;
      case MatchPhase.BallResolution:
        this.#reticle.setVisible(false);
        break;
      case MatchPhase.Reaction: {
        const last = this.#match.snapshot.match.kicks.at(-1);
        if (last?.result === KickResult.Goal) {
          this.#celebration.surge(SLICE_TEAMS.home.primary);
          this.#audio.goal();
          this.#hud.say('Goal.');
        } else {
          this.#audio.save();
          this.#hud.say(last?.result === KickResult.Saved ? 'Saved.' : 'Off target.');
        }
        break;
      }
      case MatchPhase.ScoreUpdate: {
        const score = scoreline(this.#match.snapshot.match);
        this.#hud.setScore(score.home, score.away);
        break;
      }
      case MatchPhase.Result: {
        const state = this.#match.snapshot.match;
        const score = scoreline(state);
        const winner = state.winner === Side.Home ? SLICE_TEAMS.home : SLICE_TEAMS.away;
        this.#cameras.setShot(CameraShot.Result);
        this.#reticle.setVisible(false);
        this.#celebration.winner(
          `${winner.name.toUpperCase()} WIN`,
          `${score.home} : ${score.away}`,
          winner.primary,
        );
        this.#hud.say(`${winner.name} win, ${score.home} to ${score.away}.`);
        break;
      }
      default:
        break;
    }
  }

  // ── Loop ─────────────────────────────────────────────────────────────────

  #loop = (): void => {
    if (!this.#running) return;
    this.#frame = requestAnimationFrame(this.#loop);

    const now = performance.now();
    // Clamped: a backgrounded tab can hand back a delta of many seconds, and
    // spending it would fast-forward the whole match in one frame.
    const delta = Math.min(0.1, (now - this.#last) / 1000);
    this.#last = now;

    this.#match.tick(delta);
    this.#advanceFlight(delta);
    this.#updateActors(delta);

    const snapshot = this.#match.snapshot;
    this.#hud.setClock(this.#match.shotClockRemaining);
    this.#hud.setPips(
      pipsFor(snapshot.match, Side.Home),
      pipsFor(snapshot.match, Side.Away),
    );

    if (this.#charge) {
      // The meter sweeps up and back down, so holding too long is a real cost
      // rather than a free maximum.
      const held = (performance.now() - this.#charge.startedAt) / 1000;
      const sweep = (Math.sin(held * 2.4 - Math.PI / 2) + 1) / 2;
      this.#charge.power = sweep * 100;
      this.#hud.setPower(this.#charge.power);
    }

    this.#arena.update(delta, this.#celebration.intensity);
    this.#cameras.update(
      delta,
      this.#flight ? this.#flight.position : null,
      snapshot.phase === MatchPhase.LiveKick ? Math.min(1, snapshot.elapsed / 1.2) : 0,
    );

    this.#renderer.render(this.#scene, this.#cameras.camera);
  };

  /**
   * Spend accumulated time in fixed steps.
   *
   * The whole determinism guarantee lives in this method. Never `step(delta)`.
   */
  #advanceFlight(delta: number): void {
    if (!this.#flight) return;

    this.#accumulator += delta;
    while (this.#accumulator >= PHYSICS.timeStep && this.#flight.outcome === Outcome.InFlight) {
      this.#accumulator -= PHYSICS.timeStep;
      this.#flight = step(this.#flight);
    }

    this.#ball.setPosition(this.#flight.position);
    this.#ball.spin(this.#flight.velocity, delta);

    if (this.#flight.outcome !== Outcome.InFlight) {
      this.#accumulator = 0;
      this.#finish(this.#flight);
    }
  }

  /** Turn a flight outcome plus the keeper's dive into a rules result. */
  #finish(flight: BallState): void {
    const commit = this.#keeperCommit;
    this.#flight = null;

    let result: KickResult;

    if (flight.outcome === Outcome.Goal && commit) {
      const save = attemptSave(flight.position, commit, flight.elapsed);
      result = save.saved ? KickResult.Saved : KickResult.Goal;
      if (save.saved) this.#cameras.impulse(0.12);
    } else if (flight.outcome === Outcome.Goal) {
      result = KickResult.Goal;
    } else if (flight.outcome === Outcome.Woodwork) {
      result = KickResult.Woodwork;
    } else {
      result = KickResult.OffTarget;
    }

    this.#match.resolveKick({
      striker: this.#match.striker,
      input: {
        targetX: this.#aim.x,
        targetY: this.#aim.y,
        power: 0,
        curl: 0,
        perfect: false,
        stance: this.#stance,
      },
      keeper: commit ?? { direction: null, committedAt: 0, linePosition: 0 },
      outcome: flight.outcome,
      result,
      crossing: { x: flight.position.x, y: flight.position.y },
      saveMargin: 0,
    });
  }

  #updateActors(delta: number): void {
    const snapshot = this.#match.snapshot;
    const t = performance.now() / 1000;

    // The referee's arm is the visual whistle, for anyone who cannot hear it.
    const whistling =
      snapshot.phase === MatchPhase.RefereeWhistle || snapshot.phase === MatchPhase.LiveKick;
    this.#referee.setSignal(whistling ? Math.min(1, snapshot.elapsed / 0.3) : 0, t);

    if (this.#flight) {
      this.#striker.setStrike(this.#flight.elapsed * 4);
      const commit = this.#keeperCommit;
      if (commit) {
        this.#keeper.setDive(
          commit.direction,
          extensionAt(commit, this.#flight.elapsed),
          commit.linePosition,
        );
      }
    } else if (snapshot.phase === MatchPhase.LiveKick && this.#charge) {
      this.#striker.setRunUp(Math.min(1, (performance.now() - this.#charge.startedAt) / 700));
      this.#keeper.setLine(Math.sin(t * 1.4) * 0.7, t);
    } else {
      this.#striker.idle(t);
      this.#keeper.setLine(Math.sin(t * 1.1) * 0.5, t);
    }

    void delta;
  }

  #resize(container: HTMLElement): void {
    const width = container.clientWidth;
    const height = Math.max(1, container.clientHeight);
    const ratio = Math.min(window.devicePixelRatio || 1, QUALITY[this.#quality].maxPixelRatio);
    this.#renderer.setPixelRatio(ratio);
    this.#renderer.setSize(width, height, false);
    this.#cameras.resize(width / height);
  }

  dispose(): void {
    this.#running = false;
    cancelAnimationFrame(this.#frame);
    this.#arena.dispose();
    this.#lighting.dispose();
    this.#striker.dispose();
    this.#keeper.dispose();
    this.#referee.dispose();
    this.#ball.dispose();
    this.#hud.dispose();
    this.#reticle.dispose();
    this.#celebration.dispose();
    this.#renderer.dispose();
  }
}
