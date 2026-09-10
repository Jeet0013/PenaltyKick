/**
 * The in-match HUD.
 *
 * Score and pips at the top, shot clock, power and curl at the bottom, and
 * nothing across the goal. That last constraint is the one that shapes the
 * layout: the goal mouth is where the player is looking and where the reticle
 * has to be readable, so the middle of the screen stays empty.
 *
 * DOM over the canvas rather than drawn in the scene. Text in a 3D scene is
 * either blurry or expensive, and the accessibility requirements — UI scale,
 * high contrast, subtitles, screen-reader output — are things the DOM already
 * does properly and WebGL does not do at all.
 */

import { Stance } from '../game/BallPhysics';
import type { Pip } from '../game/ShootoutRules';
import type { Team } from '../game/Teams';

export interface HudTeams {
  readonly home: Team;
  readonly away: Team;
}

export class Hud {
  readonly root: HTMLElement;
  readonly #homePips: HTMLElement;
  readonly #awayPips: HTMLElement;
  readonly #score: HTMLElement;
  readonly #clock: HTMLElement;
  readonly #powerFill: HTMLElement;
  readonly #curlFill: HTMLElement;
  readonly #prompt: HTMLElement;
  readonly #subtitle: HTMLElement;
  readonly #stances: Map<Stance, HTMLButtonElement> = new Map();
  #onStance: ((stance: Stance) => void) | null = null;

  constructor(container: HTMLElement, teams: HudTeams) {
    injectStyles();

    this.root = el('div', 'hud');

    // ── Top: names, score, pips ──────────────────────────────────────────
    const top = el('div', 'hud__top');

    const homeBlock = el('div', 'hud__team hud__team--home');
    const homeName = el('span', 'hud__name');
    homeName.textContent = teams.home.name;
    homeName.style.color = hex(teams.home.primary);
    this.#homePips = el('div', 'hud__pips');
    homeBlock.append(homeName, this.#homePips);

    this.#score = el('div', 'hud__score');
    this.#score.textContent = '0 : 0';

    const awayBlock = el('div', 'hud__team hud__team--away');
    const awayName = el('span', 'hud__name');
    awayName.textContent = teams.away.name;
    awayName.style.color = hex(teams.away.primary);
    this.#awayPips = el('div', 'hud__pips');
    awayBlock.append(awayName, this.#awayPips);

    top.append(homeBlock, this.#score, awayBlock);

    // ── Middle: the shot clock and the state prompt ──────────────────────
    this.#clock = el('div', 'hud__clock');
    this.#prompt = el('div', 'hud__prompt');

    /*
     * Subtitles for the referee and the crowd.
     *
     * Required by the spec, and it is not decoration: the whistle is the cue
     * that the kick may be taken, and a player who cannot hear it has no other
     * way to know. `aria-live` means a screen reader announces it too.
     */
    this.#subtitle = el('div', 'hud__subtitle');
    this.#subtitle.setAttribute('role', 'status');
    this.#subtitle.setAttribute('aria-live', 'polite');

    // ── Bottom: stance, power, curl ──────────────────────────────────────
    const bottom = el('div', 'hud__bottom');

    const stanceRow = el('div', 'hud__stances');
    for (const stance of [Stance.Driven, Stance.Placed, Stance.Finesse, Stance.Chip]) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'hud__stance';
      button.textContent = stance.charAt(0) + stance.slice(1).toLowerCase();
      button.dataset['stance'] = stance;
      button.addEventListener('click', (event) => {
        event.stopPropagation();
        this.#onStance?.(stance);
      });
      this.#stances.set(stance, button);
      stanceRow.append(button);
    }

    const meters = el('div', 'hud__meters');

    const power = el('div', 'hud__meter');
    const powerLabel = el('span', 'hud__meter-label');
    powerLabel.textContent = 'Power';
    const powerTrack = el('div', 'hud__track');
    this.#powerFill = el('div', 'hud__fill hud__fill--power');
    // The optimal band from the spec, drawn on the track so it can be aimed for.
    const sweet = el('div', 'hud__sweet');
    powerTrack.append(sweet, this.#powerFill);
    power.append(powerLabel, powerTrack);

    const curl = el('div', 'hud__meter');
    const curlLabel = el('span', 'hud__meter-label');
    curlLabel.textContent = 'Curl';
    const curlTrack = el('div', 'hud__track hud__track--curl');
    this.#curlFill = el('div', 'hud__fill hud__fill--curl');
    curlTrack.append(this.#curlFill);
    curl.append(curlLabel, curlTrack);

    meters.append(power, curl);
    bottom.append(stanceRow, meters);

    this.root.append(top, this.#clock, this.#prompt, this.#subtitle, bottom);
    container.append(this.root);

    this.setStance(Stance.Driven);
  }

  onStanceChange(handler: (stance: Stance) => void): void {
    this.#onStance = handler;
  }

  setStance(stance: Stance): void {
    for (const [key, button] of this.#stances) {
      button.classList.toggle('is-active', key === stance);
      button.setAttribute('aria-pressed', String(key === stance));
    }
  }

  setScore(home: number, away: number): void {
    this.#score.textContent = `${home} : ${away}`;
  }

  /**
   * Draw the pips.
   *
   * Shape as well as colour: a filled disc for a goal, a hollow ring for a
   * miss. Colourblind-safe by construction rather than by choosing kinder
   * greens — if the only difference is hue, some players simply cannot read
   * the score.
   */
  setPips(home: readonly Pip[], away: readonly Pip[]): void {
    render(this.#homePips, home);
    render(this.#awayPips, away);
  }

  /** @param remaining seconds, or null when the clock is not running */
  setClock(remaining: number | null): void {
    if (remaining === null) {
      this.#clock.textContent = '';
      this.#clock.classList.remove('is-urgent');
      return;
    }
    this.#clock.textContent = remaining.toFixed(1);
    this.#clock.classList.toggle('is-urgent', remaining <= 3);
  }

  setPrompt(text: string): void {
    this.#prompt.textContent = text;
  }

  /** The spoken cue, written down. */
  say(text: string): void {
    this.#subtitle.textContent = text;
  }

  setPower(value: number): void {
    this.#powerFill.style.width = `${Math.max(0, Math.min(100, value))}%`;
    // Gold in the optimal band, per the palette rule: gold means "this is the
    // good one", and it is used nowhere else except victory.
    this.#powerFill.classList.toggle('is-sweet', value >= 58 && value <= 82);
  }

  /** @param value -1 to 1 */
  setCurl(value: number): void {
    const clamped = Math.max(-1, Math.min(1, value));
    this.#curlFill.style.transform = `translateX(${clamped * 50}%)`;
  }

  setVisible(visible: boolean): void {
    this.root.style.display = visible ? 'block' : 'none';
  }

  dispose(): void {
    this.root.remove();
  }
}

function render(host: HTMLElement, pips: readonly Pip[]): void {
  host.replaceChildren();
  for (const pip of pips) {
    const dot = el('span', `hud__pip hud__pip--${pip.toLowerCase()}`);
    dot.setAttribute('aria-label', pip === 'GOAL' ? 'scored' : pip === 'MISS' ? 'missed' : 'to come');
    host.append(dot);
  }
}

function el(tag: string, className: string): HTMLElement {
  const node = document.createElement(tag);
  node.className = className;
  return node;
}

function hex(color: number): string {
  return `#${color.toString(16).padStart(6, '0')}`;
}

let injected = false;
function injectStyles(): void {
  if (injected) return;
  injected = true;
  const style = document.createElement('style');
  style.textContent = HUD_CSS;
  document.head.append(style);
}

const HUD_CSS = `
:root {
  --hud-scale: 1;
  --cyan: #2ad4f5;
  --gold: #ffcf5c;
  --ink: #eaf4ff;
  --muted: #8fa3b8;
}

.hud {
  position: absolute;
  inset: 0;
  pointer-events: none;
  font: 500 calc(14px * var(--hud-scale))/1.4 system-ui, -apple-system, sans-serif;
  color: var(--ink);
  /* Nothing may sit across the goal mouth; the middle stays empty by layout. */
  display: block;
}

.hud__top {
  position: absolute;
  top: max(12px, env(safe-area-inset-top));
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: clamp(14px, 4vw, 40px);
  padding: 10px 18px;
  border-radius: 3px;
  background: linear-gradient(180deg, rgba(6,12,20,0.82), rgba(4,8,14,0.7));
  box-shadow: inset 0 0 0 1px rgba(42, 212, 245, 0.22);
  backdrop-filter: blur(6px);
}

.hud__team { display: flex; flex-direction: column; gap: 5px; }
.hud__team--away { align-items: flex-end; }

.hud__name {
  font: 600 calc(11px * var(--hud-scale))/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  white-space: nowrap;
}

.hud__pips { display: flex; gap: 5px; }

.hud__pip {
  width: calc(11px * var(--hud-scale));
  height: calc(11px * var(--hud-scale));
  border-radius: 50%;
  box-sizing: border-box;
}

/*
 * Shape, not only colour.
 *
 * A goal is a filled disc, a miss is a hollow ring, a pending kick is a faint
 * outline. Someone who cannot separate the hues can still read the score,
 * which is the whole point of the requirement.
 */
.hud__pip--goal { background: #3ce08a; box-shadow: 0 0 8px rgba(60,224,138,0.55); }
.hud__pip--miss { background: transparent; border: 2px solid #ff5a6a; }
.hud__pip--pending { background: transparent; border: 1px solid rgba(143,163,184,0.45); }

.hud__score {
  font: 700 calc(26px * var(--hud-scale))/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  font-variant-numeric: tabular-nums;
  letter-spacing: 0.04em;
}

.hud__clock {
  position: absolute;
  top: calc(max(12px, env(safe-area-inset-top)) + 74px);
  left: 50%;
  transform: translateX(-50%);
  font: 700 calc(30px * var(--hud-scale))/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  font-variant-numeric: tabular-nums;
  color: var(--cyan);
  text-shadow: 0 0 18px rgba(42,212,245,0.5);
}

.hud__clock.is-urgent { color: #ff5a6a; text-shadow: 0 0 18px rgba(255,90,106,0.55); }

.hud__prompt {
  position: absolute;
  top: calc(max(12px, env(safe-area-inset-top)) + 118px);
  left: 50%;
  transform: translateX(-50%);
  font: 600 calc(13px * var(--hud-scale))/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  letter-spacing: 0.28em;
  text-transform: uppercase;
  color: var(--muted);
  white-space: nowrap;
}

.hud__subtitle {
  position: absolute;
  bottom: calc(max(16px, env(safe-area-inset-bottom)) + 150px);
  left: 50%;
  transform: translateX(-50%);
  max-width: min(90vw, 40ch);
  text-align: center;
  padding: 6px 14px;
  border-radius: 2px;
  background: rgba(4,8,14,0.78);
  font-size: calc(13px * var(--hud-scale));
  min-height: 1.4em;
}

/*
 * No text, no chip.
 *
 * The padding and min-height meant an empty subtitle still painted a dark
 * rectangle in the middle of the pitch — it looked like a hole in the world,
 * and it took a raycast to work out it was not a scene object at all.
 */
.hud__subtitle:empty {
  display: none;
}

.hud__bottom {
  position: absolute;
  bottom: calc(max(14px, env(safe-area-inset-bottom)) + 18px);
  left: 50%;
  transform: translateX(-50%);
  width: min(560px, 92vw);
  display: flex;
  flex-direction: column;
  gap: 10px;
  pointer-events: auto;
}

.hud__stances { display: flex; gap: 6px; justify-content: center; }

.hud__stance {
  flex: 1 1 0;
  /* Never under the 44px touch minimum, whatever the UI scale. */
  min-height: 44px;
  padding: 0 4px;
  border: 1px solid rgba(42,212,245,0.3);
  border-radius: 2px;
  background: rgba(6,12,20,0.72);
  color: var(--muted);
  font: 600 calc(11px * var(--hud-scale))/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  cursor: pointer;
  transition: color 160ms ease, border-color 160ms ease, background-color 160ms ease;
}

.hud__stance.is-active {
  color: #04121a;
  background: var(--cyan);
  border-color: var(--cyan);
}

.hud__meters { display: flex; gap: 12px; }
.hud__meter { flex: 1; display: flex; flex-direction: column; gap: 4px; }

.hud__meter-label {
  font: 600 calc(9px * var(--hud-scale))/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: var(--muted);
}

.hud__track {
  position: relative;
  height: 12px;
  border-radius: 2px;
  background: rgba(6,12,20,0.8);
  box-shadow: inset 0 0 0 1px rgba(42,212,245,0.22);
  overflow: hidden;
}

.hud__sweet {
  position: absolute;
  left: 58%;
  width: 24%;
  top: 0;
  bottom: 0;
  background: rgba(255,207,92,0.18);
  border-left: 1px solid rgba(255,207,92,0.5);
  border-right: 1px solid rgba(255,207,92,0.5);
}

.hud__fill {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  width: 0;
  background: var(--cyan);
}

.hud__fill--power.is-sweet { background: var(--gold); }

.hud__track--curl { display: flex; justify-content: center; }
.hud__fill--curl { position: relative; left: auto; width: 3px; background: var(--cyan); }

/* Accessibility switches, all driven from the settings panel. */
.a11y-contrast .hud__top,
.a11y-contrast .hud__subtitle { background: #000; }
.a11y-contrast .hud { color: #fff; }
.a11y-contrast .hud__pip--pending { border-color: #fff; }

@media (prefers-reduced-motion: reduce) {
  .hud__fill { transition: none !important; }
}
`;
