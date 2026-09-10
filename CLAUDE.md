# CYBER GOAL: SHOOTOUT

A 3D futuristic penalty shootout. **Unity + C# + URP**, mobile-first, landscape.

## Engine: Unity (changed 2026-09-10)

This project ran on Three.js until the master development prompt specified
Unity, C# and URP. The owner was asked directly, given the cost — losing the
browser share link, losing 5,173 working lines — and confirmed Unity.

So the earlier rule in this file, which said the engine question was "already
closed" in favour of Three.js, is **void**. Do not re-open it in either
direction without asking the owner; it has now been decided twice.

The Three.js prototype is preserved at the repository root (`src/`, `index.html`)
and still builds. It is the **design proof**, not the product: its rules,
physics constants, keeper reach model and AI were validated by 77 tests, and
that logic is what was ported to C#, not thrown away. Treat it as reference.

## Layout

```
unity/CyberGoalShootout/   the game
  Assets/Scripts/Core/     no UnityEngine reference — see below
  Assets/Scripts/Unity/    MonoBehaviours, the only place Unity types appear
core-tests/                dotnet test project running the Core assembly
src/, index.html           the preserved Three.js prototype
```

## The Core assembly must never reference UnityEngine

The single most important rule here, and it is not a style preference:

- **It is the only way to test anything on this machine.** Unity is not
  installed, and there is not enough disk space to install it. A Core assembly
  that targets netstandard2.1 compiles and runs under `dotnet test` in seconds.
  A rule that imports `UnityEngine` cannot be run at all until someone installs
  a 15 GB editor.
- It is what §51 of the brief asks for, and what makes server-authoritative
  multiplayer possible later: the server cannot run a MonoBehaviour.
- It keeps the match deterministic. `UnityEngine.Random` and `Time.deltaTime`
  are both outside the simulation's control; `Rng` and an explicit timestep are
  not.

`Vector3` and friends therefore have small plain-C# equivalents in Core. Convert
at the boundary, in the MonoBehaviour, and nowhere else.

## Commands

```bash
dotnet test core-tests            # the rules, physics, keeper and AI
dotnet build unity/CyberGoalShootout/Assets/Scripts/Core
```

Unity's own Test Runner picks up the same NUnit tests once the editor exists.

## Binding rules

Carried from the previous project, each learned the hard way:

1. **Verify on a real device, not a desktop browser or the Editor.** Four bugs
   in the last project were invisible everywhere except a phone.
2. **The build stamp is not decoration.** Every artifact says which build it is,
   on screen. A bug was reported three times against a build that predated its
   own fix.
3. **Never let a deploy be a manual step.**
4. **Measure before diagnosing.** A lighting bug was blamed on a material that
   was already correct; one query of the actual camera and light positions found
   it in a minute. The cyan pitch in the web prototype had three separate
   causes and guessing found none of them.
5. **No placeholder gameplay logic** where a real implementation is possible.
6. Tunables belong in config, never inline in the systems that read them.
7. **Say what is placeholder.** The brief asks for WORKING / PARTIAL /
   PLACEHOLDER / NOT IMPLEMENTED, and capsule characters are PLACEHOLDER until
   real rigged humanoids replace them (§21 of the art direction).

## Characters

Owner chose MakeHuman (§10) over CC0 asset packs. MakeHuman is a desktop GUI
that cannot be driven from here, so character generation is an owner task;
`docs/CHARACTER-PIPELINE.md` has the exact settings and export options, and the
integration points are already in the code. Until then the figures are
anatomically-proportioned placeholders and are labelled as such.
