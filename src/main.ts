/**
 * Entry point.
 *
 * Deliberately thin: it reports the build, hands off to the game, and makes
 * sure a failure to start says so on screen rather than leaving a black
 * rectangle and a console nobody is looking at on a phone.
 */

import { GAME_CONFIG, IS_DEV } from './config/GameConfig';

const boot = document.getElementById('boot');
const status = document.getElementById('boot-status');

function report(message: string): void {
  if (status) status.textContent = message;
}

function dismissBoot(): void {
  if (!boot) return;
  boot.classList.add('is-hidden');
  // Removed rather than left transparent: an invisible full-screen element
  // still answers hit tests, and swallowing the first tap of a game is a
  // mistake worth not repeating.
  window.setTimeout(() => boot.setAttribute('hidden', ''), 420);
}

async function start(): Promise<void> {
  report('Loading…');

  // The game itself lands here once the design is settled.
  await Promise.resolve();

  report('Ready');
  dismissBoot();
}

void start().catch((error: unknown) => {
  console.error(`[${GAME_CONFIG.name}] startup failed:`, error);
  report('Could not start. Please reload.');
});

if (IS_DEV) {
  console.info(
    `%c${GAME_CONFIG.name} v${GAME_CONFIG.version}`,
    'color:#7fb98a;font-weight:600',
  );
  console.info(`build ${GAME_CONFIG.build}`);
}
