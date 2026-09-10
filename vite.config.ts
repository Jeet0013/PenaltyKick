// `vitest/config` rather than `vite` — it is the same `defineConfig` widened
// to accept the `test` block below. Importing from 'vite' typechecks the build
// options and rejects the test ones, which is a confusing error for a config
// that runs correctly.
import { defineConfig } from 'vitest/config';

import { buildId } from './scripts/build-id.mjs';

export default defineConfig({
  define: {
    __BUILD_ID__: JSON.stringify(buildId()),
  },
  server: {
    // Exposed on the LAN so the game can be opened on a real phone. A browser
    // on a desktop is not a useful proxy for a touch device in a 3D game.
    host: true,
    port: 5173,
  },
  build: {
    target: 'es2022',
    sourcemap: true,
    /*
     * Inline every asset, however large.
     *
     * `build:single` produces one self-contained HTML file, and it inlines the
     * JS only. Vite's 4 KB default would emit anything bigger into `assets/`,
     * so the single file would reference art it does not contain — and on
     * GitHub Pages, which serves exactly one `index.html`, those references
     * resolve to nothing. Both builds set this, and they have to agree: a
     * limit that differs between them is a bug that appears only in the
     * artifact nobody runs locally.
     */
    assetsInlineLimit: Number.MAX_SAFE_INTEGER,
    rollupOptions: {
      output: {
        // Three.js and Rapier are large and change rarely — split them out so
        // game-code edits do not invalidate the vendor chunk for returning
        // players.
        manualChunks(id: string) {
          if (id.includes('node_modules/three')) return 'three';
          if (id.includes('@dimforge/rapier3d-compat')) return 'rapier';
          return undefined;
        },
      },
    },
  },
  test: {
    // Most of this game is arithmetic — trajectories, timing windows, scoring —
    // and none of that needs a DOM. Suites that do declare it per file with
    // `@vitest-environment happy-dom`.
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
