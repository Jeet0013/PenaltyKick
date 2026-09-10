import { describe, expect, it } from 'vitest';

import { GAME_CONFIG } from './GameConfig';

/*
 * A scaffold test, and not a pointless one: it proves the test runner, the
 * TypeScript path resolution and the Vite `define` substitution all work
 * together. A project whose first real test is also the first time the
 * harness has ever run is a project that debugs two things at once.
 */
describe('GAME_CONFIG', () => {
  it('reports a build identity', () => {
    expect(GAME_CONFIG.build).toBeTruthy();
  });

  it('is either a dev build or a timestamped one', () => {
    // The stamp format is `YYYYMMDD-HHMMSSZ`, which is what makes it sortable
    // and what a bug report can be checked against.
    expect(GAME_CONFIG.build).toMatch(/^(dev|\d{8}-\d{6}Z)$/);
  });
});
