# Penalty Kick

A 3D penalty shootout for the browser. TypeScript + Vite + Three.js + Rapier.

## Status

**Scaffold only.** The engine, build and deploy pipeline are in place and
proven; the game's rules, feel and art direction have not been specified yet
and nothing should be invented ahead of them.

## Commands

```bash
npm install
npm run dev          # Vite dev server, exposed on the LAN for phone testing
npm run build        # typecheck + tests + production build
npm run build:single # one self-contained HTML file → dist-single/
npm run typecheck
npm test
```

## Binding rules

Carried over from the previous project, where each was learned the hard way:

1. **Verify on a real device, not a desktop browser.** Four bugs in the last
   project were invisible everywhere except a phone: a CSS rule that rendered
   two full-screen images at once, controls with a border too faint to see on
   a dark screen, an audio fallback firing on first use, and a light that
   washed out one seat in four.
2. **The build stamp is not decoration.** Every artifact says which build it
   is, on screen. A bug was reported three times against a build that predated
   its own fix; the stamp is what ends that conversation in one glance.
3. **Never let a deploy be a manual step.** CI builds and publishes on merge to
   `main`, gated on typecheck and tests. A hand-build is invisible when it does
   not happen.
4. **Measure before diagnosing.** The four-player lighting bug was first blamed
   on a material; the material was already fixed and the symptom remained. One
   query of the camera and light positions found it immediately.
5. **No placeholder gameplay logic** where a real implementation is possible.
6. Tunables belong in `config/`, never inline in the systems that read them.

## Architecture invariants

- Rules and scoring must be driveable headlessly — no import from rendering
  into gameplay. It is what makes the logic testable and keeps multiplayer
  possible later.
- Systems talk through an event bus, not direct references.
- One module, one responsibility. No giant files.

## What is deliberately not here yet

Rules, pitch dimensions, keeper behaviour, shot mechanics, art direction, UI.
All of it waits for the design.
