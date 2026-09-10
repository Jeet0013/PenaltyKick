/**
 * The aiming reticle.
 *
 * Holographic, and — per the spec — never allowed to conceal the goalkeeper.
 * That single constraint decides the design: a ring with an open centre and a
 * clear outline, rather than a filled marker or a crosshair with a dot. The
 * player has to be able to see what they are shooting past.
 *
 * There is deliberately no trajectory preview. The spec permits one only in
 * tutorial and practice, never in competitive play, and the reason is sound: a
 * preview turns aiming from a judgement into a readout.
 */

export class Reticle {
  readonly #element: HTMLElement;
  #visible = false;

  constructor(container: HTMLElement) {
    injectStyles();
    this.#element = document.createElement('div');
    this.#element.className = 'reticle';
    this.#element.setAttribute('aria-hidden', 'true');
    this.#element.innerHTML = `
      <svg viewBox="0 0 64 64">
        <circle class="reticle__outline" cx="32" cy="32" r="22" />
        <circle class="reticle__ring" cx="32" cy="32" r="22" />
        <path class="reticle__tick" d="M32 4v10M32 50v10M4 32h10M50 32h10" />
      </svg>`;
    container.append(this.#element);
    this.setVisible(false);
  }

  /**
   * Place it, in goal-plane metres.
   *
   * Positioned as a percentage of the viewport rather than projected through
   * the camera. The camera moves during the run-up, and a reticle that drifts
   * because the camera eased forward would be unaimable — the target is a
   * point on the goal, not a point in the world.
   */
  setTarget(x: number, y: number): void {
    const left = 50 + (x / 4.6) * 22;
    const bottom = 26 + (y / 3.6) * 30;
    this.#element.style.left = `${left}%`;
    this.#element.style.bottom = `${bottom}%`;
  }

  setVisible(visible: boolean): void {
    this.#visible = visible;
    this.#element.style.opacity = visible ? '1' : '0';
  }

  get visible(): boolean {
    return this.#visible;
  }

  dispose(): void {
    this.#element.remove();
  }
}

let injected = false;
function injectStyles(): void {
  if (injected) return;
  injected = true;
  const style = document.createElement('style');
  style.textContent = `
.reticle {
  position: absolute;
  width: 64px;
  height: 64px;
  transform: translate(-50%, 50%);
  pointer-events: none;
  transition: opacity 180ms ease;
  filter: drop-shadow(0 0 8px rgba(42, 212, 245, 0.55));
}

.reticle svg { width: 100%; height: 100%; }

/*
 * Two rings: a dark outline under a bright one.
 *
 * The accessibility outline from the spec. Against a pale net the cyan alone
 * disappears; against dark grass the dark outline alone does. One under the
 * other survives both, which a single stroke of any colour cannot.
 */
.reticle__outline {
  fill: none;
  stroke: rgba(0, 0, 0, 0.75);
  stroke-width: 5;
}

.reticle__ring {
  fill: none;
  stroke: #2ad4f5;
  stroke-width: 2;
}

.reticle__tick {
  stroke: #2ad4f5;
  stroke-width: 2;
  stroke-linecap: round;
  filter: drop-shadow(0 0 2px rgba(0,0,0,0.8));
}

@media (prefers-reduced-motion: reduce) {
  .reticle { transition: none; }
}
`;
  document.head.append(style);
}
