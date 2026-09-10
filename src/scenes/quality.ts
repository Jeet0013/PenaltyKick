/**
 * Quality tiers.
 *
 * Mandatory per the spec, and the reason is the one the last project proved:
 * the device that matters is a phone, and a scene tuned on a desktop will
 * thermally throttle on one inside two minutes.
 *
 * Nothing here may be required for readability. Bloom, crowd density and
 * particles are all allowed to disappear entirely; the ball, the goal and the
 * reticle are not.
 */

export const Quality = {
  Low: 'LOW',
  Medium: 'MEDIUM',
  High: 'HIGH',
} as const;
export type Quality = (typeof Quality)[keyof typeof Quality];

export interface QualityPreset {
  readonly shadows: boolean;
  readonly shadowMapSize: number;
  /** Hard ceiling on devicePixelRatio — the single largest mobile cost. */
  readonly maxPixelRatio: number;
  readonly antialias: boolean;
  readonly bloom: boolean;
  readonly crowdCount: number;
  readonly droneCount: number;
  readonly turfSegments: number;
  readonly environment: boolean;
}

export const QUALITY: Record<Quality, QualityPreset> = {
  [Quality.Low]: {
    shadows: false,
    shadowMapSize: 0,
    maxPixelRatio: 1,
    antialias: false,
    bloom: false,
    crowdCount: 420,
    droneCount: 0,
    turfSegments: 1,
    // The IBL lookup on every physical material is not what a struggling GPU
    // should be spending on.
    environment: false,
  },
  [Quality.Medium]: {
    shadows: true,
    shadowMapSize: 1024,
    maxPixelRatio: 1.5,
    antialias: true,
    bloom: true,
    crowdCount: 1400,
    droneCount: 14,
    turfSegments: 1,
    environment: true,
  },
  [Quality.High]: {
    shadows: true,
    shadowMapSize: 2048,
    maxPixelRatio: 2,
    antialias: true,
    bloom: true,
    crowdCount: 3200,
    droneCount: 30,
    turfSegments: 2,
    environment: true,
  },
};

/**
 * Guess a starting tier.
 *
 * A guess, and treated as one: the settings screen can override it, and the
 * guess errs downward. A player who lowers quality has made a choice; a player
 * whose phone gets hot has had one made for them.
 */
export function detectQuality(): Quality {
  if (typeof navigator === 'undefined') return Quality.Medium;

  const cores = navigator.hardwareConcurrency ?? 4;
  const coarse =
    typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches;

  if (coarse) return cores >= 8 ? Quality.Medium : Quality.Low;
  return cores >= 8 ? Quality.High : Quality.Medium;
}
