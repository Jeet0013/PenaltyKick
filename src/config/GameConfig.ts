/**
 * Build identity and global tunables.
 *
 * Nothing about the game's rules or feel lives here yet — that arrives with
 * the design. What is here is the one thing every build needs regardless: a
 * way to say which build it is.
 */

/** Injected by Vite at build time; `dev` when running from source. */
declare const __BUILD_ID__: string | undefined;

export const GAME_CONFIG = {
  name: 'CYBER GOAL: SHOOTOUT',
  version: '0.1.0',
  /**
   * Which build this is.
   *
   * Carried over from the last project for a reason worth repeating: testing
   * happens on a phone at the far end of a share link, and the first thing
   * worth establishing about any bug report is which build is actually
   * running there. Three rounds of a bug were reported against a build that
   * predated its own fix, and a visible stamp is what ends that conversation
   * in one glance.
   */
  build: typeof __BUILD_ID__ === 'string' ? __BUILD_ID__ : 'dev',
} as const;

/** True when running from the dev server rather than a built artifact. */
export const IS_DEV = import.meta.env.DEV;
