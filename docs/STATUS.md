# Status

§56 requires every feature to be labelled **WORKING**, **PARTIAL**,
**PLACEHOLDER** or **NOT IMPLEMENTED**, and forbids pretending an incomplete
feature is finished. This is that ledger, and it is deliberately unflattering.

## The one thing to read first

**Unity has never been run against this code.** Unity is not installed on this
machine and there is not enough free disk to install it — 10 GB available
against 12–18 GB for an editor with Android and iOS build support.

That splits everything below into two very different confidence levels:

- **`Assets/Scripts/Core/`** — compiles under `dotnet build` and is covered by
  **104 passing tests**, run by CI on every push. Verified.
- **`Assets/Scripts/Unity/`** — typechecks only against hand-written stubs in
  `tools/unity-stubs/`. That catches typos, cross-file mistakes and type errors
  against Core, and it caught three on its first run. It proves **nothing** about
  whether the real Unity API matches, because the stubs encode my assumptions.
  **Unverified until someone opens the editor.**

To clear this: free ~20 GB, install Unity **6000.0.83f1** with Android Build
Support, open `unity/CyberGoalShootout`, press Play. No scene setup is needed —
`GameBootstrap` builds itself via `RuntimeInitializeOnLoadMethod`.

---

## Phase A — Foundation

| Feature | Status | Notes |
|---|---|---|
| Project structure (§50) | **WORKING** | Folders, two asmdefs, package manifest |
| Core/Unity assembly split (§51) | **WORKING** | Core has `noEngineReferences: true` |
| Game state system (§49) | **WORKING** | All 20 states; tested |
| Stadium blockout (§55) | **PARTIAL** | Geometry written, never rendered |
| Camera (§25) | **PARTIAL** | 7 shots written, framing unverified on a device |
| Ball, goal | **WORKING** | Physics tested; rendering unverified |

## Phase B — Core penalty gameplay

| Feature | Status | Notes |
|---|---|---|
| Swipe shooting (§9) | **WORKING** | Direction/power/curve; 9 tests |
| Ball physics (§13) | **WORKING** | 14 tests, incl. ground bounce and tunnelling |
| Goal detection | **WORKING** | Interpolated crossing; tunnelling test |
| Basic goalkeeper (§15) | **WORKING** | Reach model, 10 tests |
| Basic scoring (§6) | **WORKING** | 10 tests |
| Perfect timing (§11) | **WORKING** | 6 tests |
| One full penalty cycle (§60) | **PARTIAL** | Written and typechecked; never played |
| Match formats (§5 Quick/Standard) | **WORKING** | 3 and 5 kicks; 6 tests |
| Keeper gesture controls (§14) | **PARTIAL** | Parsing done and tested; not yet bound to input |
| Special goal grades (§23) | **WORKING** | 9 tests; labels wired to the HUD |
| Spectacular save (§24) | **WORKING** | Slow-motion gate is one function |
| Replay recording (§26) | **PARTIAL** | Recording and re-simulation tested; no replay camera |

## Phase C — Match rules (arrived early, via the port)

| Feature | Status | Notes |
|---|---|---|
| Alternating strikers | **WORKING** | |
| Five penalties each (§6) | **WORKING** | |
| Early victory (§7) | **WORKING** | Both the 4–1 and 3–0 cases tested |
| Sudden death (§8) | **WORKING** | Completed-round rule tested |
| Winner detection | **WORKING** | Full-match test asserts the scoreline agrees |

## Characters and art — the honest part

| Feature | Status | Notes |
|---|---|---|
| Striker / keeper / referee bodies | **PARTIAL** | Generated skinned humanoids on a 20-bone rig, 2,573 verts each. Faces, hair, hands, kit zones. Not scanned art |
| Rigged humanoids (§2, §23) | **PARTIAL** | Rig and proportions are real; no scanned detail. MakeHuman still the intended path |
| Character animation (§16, §17) | **PLACEHOLDER** | Procedural bone poses; the swap point is `CharacterVisual` |
| Crowd (§19, §48) | **PARTIAL** | Instanced low-poly seated humanoids, 4 silhouettes, 9 tint groups |
| Crowd reaction states (§19) | **WORKING** | Idle / tension / goal / save / victory, driven from match state |
| Crowd celebration (§19, §22) | **WORKING** | Rectified per-instance jump on a goal |
| Camera flashes (§20) | **WORKING** | Spawn-pool, additive, rate scales with mood |
| Cheerleaders | **WORKING** | 12 performers, beat-driven routine, pom-poms |
| Crowd LOD tiers (§7 art) | **NOT IMPLEMENTED** | One tier; density scales with quality |
| Vertex-colour kit shader | **PARTIAL** | `CharacterKit.shader` written, never compiled by Unity |
| Pitch markings | **WORKING** | Regulation box, arc, spot — the main scale cue |
| Neon / holograms (§20) | **PARTIAL** | Emissive trim only |

## Determinism, and why it is listed as a feature

`MatchReplayTests` plays a full match, records **only the inputs**, replays it,
and asserts every result and every crossing point matches to the last bit. A
further test tampers with a recording and asserts the outcome changes.

This is the keystone. The analytic ball flight, the seeded `Rng`, the fixed
timestep and the doubles in `Vec3` all exist to make it pass, and §40's
server-side validation is the same property viewed from the other side: a match
that can be re-derived from its inputs can be checked by a server from its
inputs. If that test ever fails, replays are wrong and honest players would be
rejected as cheats.

## Not started

Everything below is **NOT IMPLEMENTED**. The brief says to stop after Phase B,
and these are Phase C and beyond.

Audio (§30), dynamic music (§31), haptics (§29), replay *camera* (§26),
goal celebration sequence (§22), menus, team select screen (§32 data exists),
local two-player (§5), tournament (§38), ranking (§39), online multiplayer
(§40), cosmetics (§36), player attributes (§34).

## The winding bug, as a warning about what typechecking cannot see

The generated character meshes were wound inside-out — every lofted limb and the
head — for as long as they existed. They typechecked perfectly the whole time.
With back-face culling that renders a figure hollow: you see the inside of the
far surface, and it looks like a modelling error rather than an index-order one.

It was caught by one agent reading another's code, then confirmed numerically
against Unity's documented rule (`front normal = Cross(b-a, c-a)`), not by any
compiler. Treat every visual claim in this document accordingly: the geometry is
verified structurally — bone weights sum to 1, the sole sits at y = 0, triangles
face outward — and **nothing here has been rendered.**

## Known gaps worth naming

1. **No `.unity` scene file.** Deliberate: hand-written scene YAML is GUID-laden
   and fails opaquely, and none could be validated here. `GameBootstrap` builds
   the scene in code instead. Revisit once an editor exists.
2. **No `.meta` files.** Unity generates them on first import. They should be
   committed after that first open, or every collaborator gets different GUIDs.
3. **Legacy `Input` rather than the Input System package.** A screen position
   over time is the whole requirement, both backends give it identically, and
   this way the project runs whether or not the package finished importing.
   Revisit for gamepad support (§5).
4. **Quality tiers are applied but unmeasured.** §46's frame targets cannot be
   confirmed without a device.
5. **Kit colours depend on an uncompiled shader.** `CharacterKit.shader` is
   hand-written and has never been through Unity's compiler. If it fails,
   `ShaderLibrary.CharacterKit` logs a warning and falls back to URP/Lit, which
   ignores vertex colour — characters would then be one flat colour rather than
   magenta. Loud on purpose.
6. **`QualityController` computes a render scale it never applies.** It needs
   the URP asset's `renderScale`, which requires a URP asset to exist — one is
   created by the editor on first open.
