/**
 * Entry point.
 *
 * Thin on purpose: report the build, start the game, and make sure a failure
 * to start says so on screen rather than leaving a black rectangle and a
 * console nobody is looking at on a phone.
 */

import { GAME_CONFIG, IS_DEV } from './config/GameConfig';
import { Game } from './Game';

const boot = document.getElementById('boot');
const status = document.getElementById('boot-status');

function report(message: string): void {
  if (status) status.textContent = message;
}

function dismissBoot(): void {
  if (!boot) return;
  boot.classList.add('is-hidden');
  // Removed rather than left transparent: an invisible full-screen element
  // still answers hit tests, and swallowing a game's first tap is a mistake
  // worth not repeating.
  window.setTimeout(() => boot.setAttribute('hidden', ''), 420);
}

function start(): void {
  const container = document.getElementById('app');
  if (!container) throw new Error('main: no #app to mount into');

  report('Building the arena…');
  const game = new Game(container);
  game.start();

  report('Ready');
  dismissBoot();

  // The build stamp, on screen. The device that matters is a phone at the far
  // end of a link, and the first thing worth establishing about any bug report
  // is which build is actually running there.
  const stamp = document.createElement('div');
  stamp.textContent = `Build ${GAME_CONFIG.build}`;
  stamp.style.cssText = [
    'position:absolute',
    'bottom:max(4px, env(safe-area-inset-bottom))',
    'left:0',
    'right:0',
    'text-align:center',
    'font:500 10px/1 ui-monospace, SFMono-Regular, Menlo, monospace',
    'letter-spacing:0.1em',
    'color:rgba(143,163,184,0.4)',
    'pointer-events:none',
    // Bottom-right, clear of the meters. Centred, it overprinted the power
    // track — and an unreadable build stamp is the same as no build stamp.
    'text-align:right',
  ].join(';');
  container.append(stamp);

  if (IS_DEV) {
    Reflect.set(globalThis, 'game', game);
    console.info(`%c${GAME_CONFIG.name} v${GAME_CONFIG.version}`, 'color:#2ad4f5;font-weight:600');
    console.info(`build ${GAME_CONFIG.build} · quality ${game.quality}`);
  }
}

try {
  start();
} catch (error) {
  console.error(`[${GAME_CONFIG.name}] startup failed:`, error);
  report('Could not start. Please reload.');
}
