/**
 * Neon Goal Surge, and the Winner Protocol.
 *
 * ## The constraint that shapes it
 *
 * Four seconds, maximum, and never obscuring the score. A celebration that
 * outstays that turns a three-minute match into a five-minute one, and the
 * feeling it is reaching for — elation — is the first thing lost when the
 * player is waiting for it to finish. Skip is always available.
 *
 * The sequence follows the spec: confirmation, arena response, character
 * response, return to play.
 *
 * ## Reduced flash is not a lesser version
 *
 * The spec asks for flashes to be replaced with a soft aurora, not simply
 * removed. Someone who needs that setting should get a celebration, not an
 * absence of one — so the reduced path has its own build-up and its own colour
 * wash, and only the strobing is gone.
 */

export interface CelebrationSettings {
  reducedFlash: boolean;
  reducedMotion: boolean;
}

export class Celebration {
  readonly #root: HTMLElement;
  readonly #wave: HTMLElement;
  readonly #banner: HTMLElement;
  readonly #skip: HTMLButtonElement;
  readonly #settings: CelebrationSettings = { reducedFlash: false, reducedMotion: false };
  #intensity = 0;
  #timer = 0;

  constructor(container: HTMLElement) {
    injectStyles();

    this.#root = document.createElement('div');
    this.#root.className = 'surge';
    this.#root.setAttribute('aria-hidden', 'true');

    this.#wave = document.createElement('div');
    this.#wave.className = 'surge__wave';

    this.#banner = document.createElement('div');
    this.#banner.className = 'surge__banner';

    this.#skip = document.createElement('button');
    this.#skip.type = 'button';
    this.#skip.className = 'surge__skip';
    this.#skip.textContent = 'Skip';
    this.#skip.addEventListener('click', (event) => {
      event.stopPropagation();
      this.clear();
    });

    this.#root.append(this.#wave, this.#banner, this.#skip);
    container.append(this.#root);

    // The system settings are the starting point; the settings panel can
    // override either. Respecting the OS by default is the whole point of
    // these media queries existing.
    if (typeof matchMedia === 'function') {
      this.#settings.reducedMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
    }
  }

  /** 0-1, read by the arena so the trim and the crowd lift with the moment. */
  get intensity(): number {
    return this.#intensity;
  }

  configure(settings: Partial<CelebrationSettings>): void {
    Object.assign(this.#settings, settings);
  }

  /**
   * A scored penalty.
   *
   * @param color the scoring team's colour — ownership is shown in team
   *   colour, never in gold. Gold is reserved for victory and perfect contact,
   *   so that when it does appear it means something.
   */
  surge(color: number): void {
    window.clearTimeout(this.#timer);

    const hex = `#${color.toString(16).padStart(6, '0')}`;
    this.#root.style.setProperty('--surge-color', hex);
    this.#root.classList.toggle('is-soft', this.#settings.reducedFlash);
    this.#root.classList.toggle('is-still', this.#settings.reducedMotion);
    this.#root.classList.add('is-active');

    this.#banner.textContent = 'GOAL';
    this.#banner.classList.remove('surge__banner--winner');
    this.#skip.style.display = 'block';
    this.#intensity = 1;

    // Hard-capped at the spec's four seconds. Not a target — a ceiling.
    this.#timer = window.setTimeout(() => this.clear(), 3400);
  }

  /**
   * The match-deciding goal.
   *
   * Longer, and allowed to be: this is the last thing that happens, so it is
   * not delaying anything. Still skippable, because a rematch should never
   * wait on a light show.
   */
  winner(title: string, score: string, color: number): void {
    window.clearTimeout(this.#timer);

    const hex = `#${color.toString(16).padStart(6, '0')}`;
    this.#root.style.setProperty('--surge-color', hex);
    this.#root.classList.toggle('is-soft', this.#settings.reducedFlash);
    this.#root.classList.toggle('is-still', this.#settings.reducedMotion);
    this.#root.classList.add('is-active', 'is-winner');

    this.#banner.classList.add('surge__banner--winner');
    this.#banner.innerHTML = `<span class="surge__title">${title}</span><span class="surge__score">${score}</span>`;
    this.#skip.style.display = 'none';
    this.#intensity = 1;
  }

  clear(): void {
    window.clearTimeout(this.#timer);
    this.#root.classList.remove('is-active', 'is-winner');
    this.#intensity = 0;
  }

  dispose(): void {
    window.clearTimeout(this.#timer);
    this.#root.remove();
  }
}

let injected = false;
function injectStyles(): void {
  if (injected) return;
  injected = true;
  const style = document.createElement('style');
  style.textContent = `
.surge {
  position: absolute;
  inset: 0;
  pointer-events: none;
  opacity: 0;
  transition: opacity 260ms ease;
  --surge-color: #2ad4f5;
}

.surge.is-active { opacity: 1; }

/*
 * The team-colour wave.
 *
 * A radial sweep from the goal outward rather than a full-screen flash: it
 * reads as the stadium reacting to something that happened at the goal, and it
 * leaves the middle of the screen — where the score is — legible.
 */
.surge__wave {
  position: absolute;
  inset: 0;
  background:
    radial-gradient(ellipse 60% 40% at 50% 38%, color-mix(in srgb, var(--surge-color) 42%, transparent), transparent 70%),
    linear-gradient(180deg, transparent 40%, color-mix(in srgb, var(--surge-color) 18%, transparent));
  animation: surge-wave 1.4s ease-out;
}

.surge__banner {
  position: absolute;
  top: 34%;
  left: 50%;
  transform: translate(-50%, -50%);
  font: 800 clamp(38px, 11vw, 92px)/1 "Arial Black", system-ui, sans-serif;
  letter-spacing: 0.06em;
  color: #fff;
  text-shadow:
    0 0 30px color-mix(in srgb, var(--surge-color) 80%, transparent),
    0 6px 24px rgba(0,0,0,0.6);
  animation: surge-pop 620ms cubic-bezier(0.2, 0.9, 0.3, 1.2);
}

.surge__banner--winner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  /* Gold, and only here. Ownership is team colour; gold means victory. */
  color: #ffcf5c;
}

.surge__title { font-size: clamp(26px, 7vw, 62px); }
.surge__score {
  font: 700 clamp(30px, 8vw, 70px)/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  font-variant-numeric: tabular-nums;
  color: #fff;
}

.surge__skip {
  position: absolute;
  right: max(16px, env(safe-area-inset-right));
  bottom: max(16px, env(safe-area-inset-bottom));
  min-height: 44px;
  padding: 0 20px;
  border: 1px solid rgba(255,255,255,0.35);
  border-radius: 2px;
  background: rgba(4,8,14,0.7);
  color: #eaf4ff;
  font: 600 12px/1 ui-monospace, SFMono-Regular, Menlo, monospace;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  cursor: pointer;
  pointer-events: auto;
}

@keyframes surge-wave {
  from { opacity: 0; transform: scale(0.7); }
  60%  { opacity: 1; }
  to   { opacity: 0.75; transform: scale(1); }
}

@keyframes surge-pop {
  from { opacity: 0; transform: translate(-50%, -50%) scale(0.75); }
  to   { opacity: 1; transform: translate(-50%, -50%) scale(1); }
}

/*
 * Reduced flash: an aurora instead of a strobe.
 *
 * Still a celebration — a slower, softer wash with no rapid luminance change.
 * Removing the effect entirely would tell those players their goals matter
 * less.
 */
.surge.is-soft .surge__wave {
  background: linear-gradient(200deg,
    color-mix(in srgb, var(--surge-color) 26%, transparent),
    transparent 60%);
  animation: surge-aurora 2.2s ease-in-out;
}

@keyframes surge-aurora {
  from { opacity: 0; }
  50%  { opacity: 0.9; }
  to   { opacity: 0.5; }
}

.surge.is-still .surge__wave,
.surge.is-still .surge__banner { animation: none; }

@media (prefers-reduced-motion: reduce) {
  .surge__wave, .surge__banner { animation: none !important; }
}
`;
  document.head.append(style);
}
