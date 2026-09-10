import { describe, expect, it } from 'vitest';

import { SLICE_TEAMS, TEAMS, teamById } from '../game/Teams';

describe('teams', () => {
  it('ships the eight the spec names', () => {
    expect(TEAMS).toHaveLength(8);
  });

  it('has no competitive attributes at all', () => {
    /*
     * The promise enforced as a test. A team cannot be stronger if there is no
     * field that could make it stronger — and a field that does not exist
     * cannot quietly acquire a use in a later commit.
     */
    const allowed = new Set(['id', 'name', 'identity', 'primary', 'secondary', 'mood']);
    for (const team of TEAMS) {
      for (const key of Object.keys(team)) {
        expect(allowed.has(key)).toBe(true);
      }
    }
  });

  it('gives every team a distinct identity and colour', () => {
    expect(new Set(TEAMS.map((t) => t.id)).size).toBe(TEAMS.length);
    expect(new Set(TEAMS.map((t) => t.primary)).size).toBe(TEAMS.length);
  });

  it('uses the two the slice was specified with', () => {
    expect(SLICE_TEAMS.home.name).toBe('Helix United');
    expect(SLICE_TEAMS.away.name).toBe('Crimson Orbit');
  });

  it('refuses an unknown id rather than returning something plausible', () => {
    expect(() => teamById('real-madrid')).toThrow();
  });
});
