/**
 * Seeded, deterministic randomness.
 *
 * `Math.random` cannot appear anywhere that affects an outcome. Two reasons,
 * and the second is the one that bites:
 *
 * 1. A replay must reproduce the match exactly, and it cannot if the source of
 *    randomness is different every run.
 * 2. In an online match the server and the client have to agree. If the coin
 *    toss or an AI decision comes from an unseeded generator, they will
 *    disagree, and the disagreement will look like cheating rather than like a
 *    bug.
 *
 * mulberry32: small, fast, and good enough for a coin toss and an AI's
 * tendencies. It is not cryptographic and must never be used as if it were.
 */
export class Rng {
  #state: number;

  constructor(seed: number) {
    // Forced to a 32-bit unsigned integer so the same seed behaves the same
    // whether it arrived as 7, 7.0 or -7.
    this.#state = seed >>> 0;
  }

  /** A float in [0, 1). */
  next(): number {
    this.#state = (this.#state + 0x6d2b79f5) >>> 0;
    let t = this.#state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }

  /** An integer in [0, max). */
  int(max: number): number {
    return Math.floor(this.next() * max);
  }

  /** True with the given probability. */
  chance(probability: number): boolean {
    return this.next() < probability;
  }

  /** One of the options, uniformly. */
  pick<T>(options: readonly T[]): T {
    const chosen = options[this.int(options.length)];
    if (chosen === undefined) throw new Error('Rng.pick: empty options');
    return chosen;
  }

  /**
   * The generator's position, so a match can be resumed or verified.
   *
   * Exposed rather than hidden: a server that wants to prove a replay needs the
   * seed *and* how far through it the match had got.
   */
  get state(): number {
    return this.#state;
  }
}
