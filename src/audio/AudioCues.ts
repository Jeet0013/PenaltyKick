/**
 * Sound, synthesised.
 *
 * Every cue is generated from oscillators and filtered noise. Nothing is
 * recorded, sampled, scraped from a broadcast, or lifted from another game —
 * the spec is unambiguous about that, and synthesis is the only way to be
 * certain of it rather than to hope.
 *
 * The whistle is the one that matters. It is not atmosphere: it is the signal
 * that the kick may be taken, and the rules depend on the player hearing it.
 * A visual cue carries the same information for anyone who cannot — see the
 * referee's raised arm and the subtitle line.
 */

export class AudioCues {
  #context: AudioContext | null = null;
  #master: GainNode | null = null;
  #noise: AudioBuffer | null = null;
  #enabled = true;

  /**
   * Start the audio context.
   *
   * Browsers refuse to begin audio without a gesture, and there is no setting
   * that changes it. Called from the first pointerdown; idempotent, because
   * iOS can suspend the context again later and re-running this is what
   * recovers it.
   */
  async unlock(): Promise<void> {
    if (this.#context) {
      if (this.#context.state === 'suspended') await this.#context.resume();
      return;
    }
    try {
      const Ctor =
        window.AudioContext ??
        (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!Ctor) return;

      const context = new Ctor();
      const master = context.createGain();
      master.gain.value = 0.75;
      master.connect(context.destination);
      this.#context = context;
      this.#master = master;
      await context.resume();
    } catch {
      // Sound is a nicety; never let its absence stop the game.
    }
  }

  setEnabled(enabled: boolean): void {
    this.#enabled = enabled;
    if (this.#master) this.#master.gain.value = enabled ? 0.75 : 0;
  }

  /** Suspend with the page, so a pocketed phone is silent. */
  setPageVisible(visible: boolean): void {
    if (!this.#context) return;
    if (visible) void this.#context.resume();
    else void this.#context.suspend();
  }

  /**
   * The referee's whistle.
   *
   * Two detuned square waves with a fast warble. A single tone reads as a
   * beep; the beat between two close frequencies is what makes it a whistle,
   * and the warble is the pea.
   */
  whistle(): void {
    const ctx = this.#ready();
    if (!ctx || !this.#master) return;
    const now = ctx.currentTime;

    for (const [i, freq] of [3180, 3260].entries()) {
      const osc = ctx.createOscillator();
      osc.type = 'square';
      osc.frequency.setValueAtTime(freq, now);
      // The warble: a fast wobble across the burst.
      osc.frequency.linearRampToValueAtTime(freq * 1.012, now + 0.1);
      osc.frequency.linearRampToValueAtTime(freq, now + 0.2);

      const band = ctx.createBiquadFilter();
      band.type = 'bandpass';
      band.frequency.value = 3200;
      band.Q.value = 6;

      const gain = ctx.createGain();
      gain.gain.setValueAtTime(0.0001, now);
      gain.gain.exponentialRampToValueAtTime(0.16 - i * 0.04, now + 0.02);
      gain.gain.setValueAtTime(0.16 - i * 0.04, now + 0.26);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.42);

      osc.connect(band).connect(gain).connect(this.#master);
      osc.start(now);
      osc.stop(now + 0.45);
    }
  }

  /** Boot on ball: a short thump with a click on top. */
  boot(power: number): void {
    const ctx = this.#ready();
    if (!ctx || !this.#master) return;
    const now = ctx.currentTime;
    const strength = 0.3 + (power / 100) * 0.7;

    const body = ctx.createOscillator();
    body.type = 'sine';
    body.frequency.setValueAtTime(160, now);
    body.frequency.exponentialRampToValueAtTime(48, now + 0.12);

    const bodyGain = ctx.createGain();
    bodyGain.gain.setValueAtTime(0.42 * strength, now);
    bodyGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.17);

    body.connect(bodyGain).connect(this.#master);
    body.start(now);
    body.stop(now + 0.2);

    // The leather crack, without which it is a drum rather than a kick.
    const click = this.#burst(ctx);
    const high = ctx.createBiquadFilter();
    high.type = 'highpass';
    high.frequency.value = 1800;
    const clickGain = ctx.createGain();
    clickGain.gain.setValueAtTime(0.22 * strength, now);
    clickGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.05);
    click.connect(high).connect(clickGain).connect(this.#master);
    click.start(now);
    click.stop(now + 0.06);
  }

  /** Net ripple plus a crowd swell. */
  goal(): void {
    const ctx = this.#ready();
    if (!ctx || !this.#master) return;
    const now = ctx.currentTime;

    const net = this.#burst(ctx);
    const band = ctx.createBiquadFilter();
    band.type = 'bandpass';
    band.frequency.value = 900;
    band.Q.value = 1.2;
    const netGain = ctx.createGain();
    netGain.gain.setValueAtTime(0.2, now);
    netGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.32);
    net.connect(band).connect(netGain).connect(this.#master);
    net.start(now);
    net.stop(now + 0.35);

    this.#crowd(now + 0.06, 1.6, 0.26);

    // A rising third: the "you scored" cue that cuts through a phone speaker,
    // where a broadband crowd loses all its body.
    for (const [i, f] of [523.25, 659.25, 783.99].entries()) {
      const osc = ctx.createOscillator();
      osc.type = 'triangle';
      const start = now + 0.14 + i * 0.07;
      osc.frequency.setValueAtTime(f, start);
      const gain = ctx.createGain();
      gain.gain.setValueAtTime(0.0001, start);
      gain.gain.exponentialRampToValueAtTime(0.11, start + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.3);
      osc.connect(gain).connect(this.#master);
      osc.start(start);
      osc.stop(start + 0.32);
    }
  }

  /** Glove impact and a crowd groan. */
  save(): void {
    const ctx = this.#ready();
    if (!ctx || !this.#master) return;
    const now = ctx.currentTime;

    const slap = this.#burst(ctx);
    const band = ctx.createBiquadFilter();
    band.type = 'bandpass';
    band.frequency.value = 1500;
    band.Q.value = 0.9;
    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0.3, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.12);
    slap.connect(band).connect(gain).connect(this.#master);
    slap.start(now);
    slap.stop(now + 0.14);

    // Lower and shorter than the goal swell: a groan, not a cheer.
    this.#crowd(now + 0.04, 1.1, 0.16, 520);
  }

  /** A crowd bed built from filtered noise with a slow envelope. */
  #crowd(at: number, duration: number, level: number, cutoff = 1400): void {
    const ctx = this.#context;
    if (!ctx || !this.#master) return;

    const source = this.#burst(ctx);
    const band = ctx.createBiquadFilter();
    band.type = 'bandpass';
    band.frequency.value = cutoff;
    band.Q.value = 0.7;

    const gain = ctx.createGain();
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(level, at + 0.18);
    gain.gain.setValueAtTime(level, at + duration * 0.6);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);

    source.connect(band).connect(gain).connect(this.#master);
    source.start(at);
    source.stop(at + duration + 0.05);
  }

  /** Two seconds of noise, built once and replayed. */
  #burst(ctx: AudioContext): AudioBufferSourceNode {
    if (!this.#noise) {
      const length = ctx.sampleRate * 2;
      const buffer = ctx.createBuffer(1, length, ctx.sampleRate);
      const data = buffer.getChannelData(0);
      let drift = 0;
      for (let i = 0; i < length; i += 1) {
        // A slow random walk over white noise gives the uneven swell of voices
        // rather than a flat hiss.
        drift = drift * 0.995 + (Math.random() - 0.5) * 0.05;
        data[i] = (Math.random() * 2 - 1) * (0.55 + drift);
      }
      this.#noise = buffer;
    }
    const source = ctx.createBufferSource();
    source.buffer = this.#noise;
    source.loop = true;
    return source;
  }

  #ready(): AudioContext | null {
    if (!this.#enabled) return null;
    return this.#context;
  }

  dispose(): void {
    void this.#context?.close();
    this.#context = null;
  }
}
