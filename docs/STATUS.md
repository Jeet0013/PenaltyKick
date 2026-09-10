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
  **75 passing tests**. Verified.
- **`Assets/Scripts/Unity/`** — typechecks only against hand-written stubs in
  `tools/unity-stubs/`. That catches typos, cross-file mistakes and type errors
  against Core, and it caught three on its first run. It proves **nothing** about
  whether the real Unity API matches, because the stubs encode my assumptions.
  **Unverified until someone opens the editor.**

To clear this: free ~20 GB, install Unity **6000.0.23f1** with Android Build
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
| Striker / keeper / referee bodies | **PLACEHOLDER** | Capsules with correct 1.82 m proportions. §21 forbids shipping this |
| Rigged humanoids (§2, §23) | **NOT IMPLEMENTED** | Needs MakeHuman — `docs/CHARACTER-PIPELINE.md`. Owner task |
| Character animation (§16, §17) | **PLACEHOLDER** | Procedural pivot poses; the swap point is `CharacterVisual` |
| Crowd (§19, §48) | **PLACEHOLDER** | Instanced quads, seated in rows. Not human figures |
| Crowd LOD tiers (§7 art) | **NOT IMPLEMENTED** | One tier only |
| Crowd reaction states (§19) | **NOT IMPLEMENTED** | |
| Pitch markings | **WORKING** | Regulation box, arc, spot — the main scale cue |
| Neon / holograms (§20) | **PARTIAL** | Emissive trim only |

## Not started

Everything below is **NOT IMPLEMENTED**, and correctly so — the brief says to
stop after Phase B.

Audio (§30), dynamic music (§31), haptics (§29), replay (§26), goal celebration
sequence (§22), special goal types (§23), menus, team select (§32 data exists,
no screen), local two-player (§5), keeper-role play for the human (§14),
tournament (§38), ranking (§39), multiplayer (§40), cosmetics (§36),
player attributes (§34).

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
5. **`QualityController` computes a render scale it never applies.** It needs
   the URP asset's `renderScale`, which requires a URP asset to exist — one is
   created by the editor on first open.
