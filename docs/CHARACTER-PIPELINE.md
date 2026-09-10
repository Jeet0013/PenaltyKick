# Characters: from MakeHuman to the pitch

You chose MakeHuman (§10 of the art direction) over CC0 asset packs. That gives
you characters that are unambiguously yours, with no third-party licence
travelling into the build. The cost is that **MakeHuman is a desktop GUI
application and cannot be driven from this session** — generating the bodies is
a task only you can do.

Everything on the code side is already in place. When you drop a prefab in, it
is picked up with no other change.

---

## What is needed

Four characters, in priority order (§23's first character milestone):

| # | Character   | Why it is first                                           |
|---|-------------|-----------------------------------------------------------|
| 1 | Striker     | Closest to camera in the main gameplay shot                |
| 2 | Goalkeeper  | On screen for every single penalty, and animates the most  |
| 3 | Referee     | Visible during the whistle; can be lower detail            |
| 4 | Crowd bases | 4–6 varied bodies, reused with material variation          |

---

## Step 1 — Install

MakeHuman Community: <https://static.makehumancommunity.org/makehuman.html>

Free, and cross-platform. **Verify the licence text yourself before shipping** —
my understanding is that the application is AGPL while the meshes and assets it
produces are released CC0, which is what makes it suitable here, but that is
exactly the kind of claim you want to confirm from the source rather than take
from me. Record what you find in `docs/ASSETS.md`.

## Step 2 — Build a body

Aim for **athletic, semi-realistic, believable** (§35 and §1 of the art
direction), not stylised and not photoreal.

- Vary height, build and proportions between the four characters. §8 warns
  against cloning one body, and the crowd is where that shows worst.
- Give the striker and keeper athletic builds; the referee can be older and
  heavier, which helps them read as a different role at a glance.

## Step 3 — Skeleton

In the **Pose/Animate → Skeleton** tab, choose a rig with a standard humanoid
bone layout. **"Default no toes"** is the usual recommendation for Unity, because
Unity's Humanoid avatar has no toe requirement and a simpler foot maps more
reliably.

Do not export without a skeleton. The whole animation system depends on
retargeting (§12), and retargeting needs bones.

## Step 4 — Export

**Files → Export**, choose **FBX**, and set:

- **Feet on ground**: on
- **Scale**: metres (Unity is metres; a decimetre export arrives ten times too
  big and every camera framing has to be redone)
- **Binary FBX** if offered

Save into `unity/CyberGoalShootout/Assets/Models/Characters/`.

> If FBX gives trouble, the fallback is MHX2 → Blender → FBX. It is more steps
> and gives more control over the armature.

## Step 5 — Import settings in Unity

Select the model, then in the Inspector:

1. **Rig** tab → **Animation Type: Humanoid**, **Avatar Definition: Create From
   This Model** → Apply.
2. Click **Configure…** and check the bone map. Green is mapped, red is missing.
   Hips, spine, chest, neck, head, both arms and both legs must be mapped or
   retargeted animation will not work.
3. **Model** tab → enable **Optimize Mesh**; set **Mesh Compression** to Medium
   for the crowd bases, off for the three hero characters.

## Step 6 — Drop it in

Make the imported model a prefab, then:

1. Add a **`CharacterVisual`** component to the prefab root.
2. Assign the prefab to the **`rigged`** field of the relevant
   **`HumanoidFactory`**.

That is the entire integration. `HumanoidFactory.Create` checks `rigged` first
and instantiates it instead of building placeholder geometry, and
`CharacterVisual` detects the `Animator` and switches from posing capsule pivots
to setting animator parameters. **No other file changes.**

## Step 7 — Animator parameters

If you use an Animator Controller, name its parameters to match
`AnimatorParams` in `CharacterVisual.cs`:

| Parameter        | Type    | Used for                                    |
|------------------|---------|---------------------------------------------|
| `Speed`          | Float   | Idle ↔ run-up blend                         |
| `Ready`          | Bool    | Keeper's ready stance                       |
| `Kick`           | Trigger | The strike                                  |
| `Dive`           | Trigger | Fires the dive                              |
| `DiveSide`       | Float   | −1 left, 0 centre, +1 right                 |
| `DiveHeight`     | Float   | 0 low, 1 high                               |
| `DiveExtension`  | Float   | **0–1, from the rules — see below**         |
| `SignalRaise`    | Float   | Referee's whistle arm                       |

### `DiveExtension` is not an animation timeline

It is fed straight from `Goalkeeper.ExtensionAt`, the same number the save
calculation uses. Drive the dive animation from it — as normalised time on the
clip — rather than letting the clip play at its own pace.

The reason is §53: if the animation reaches full stretch faster than the rules
grant reach, players see the keeper visibly touch a ball that is scored as a
goal, and no amount of tuning the clip fixes it. Driving both from one number
makes that disagreement impossible rather than unlikely.

---

## Animations

MakeHuman does not produce animation. Options, in order of preference:

1. **Mixamo** (<https://www.mixamo.com>) — free, and its rigs retarget cleanly
   to Unity Humanoid. It does require a free Adobe account, so it is a
   credential of sorts; the brief rules out *paid* APIs, and this is not one,
   but the choice is yours. Check the current licence terms directly.
2. **Author in Blender** — most work, total ownership, no account.
3. **CC0 animation libraries** — verify each one's licence individually and log
   it in `docs/ASSETS.md`.

### The kick has to be custom

§14 of the art direction is right about this and it is worth restating: a
generic kick animation will not line up with the ball. The foot must visibly
contact it, the ball must not move first, and the leg must not pass through it.
Expect to adjust contact timing by hand against
`MatchDirector.Timing.Shooting`, which is when the strike window opens.

---

## Until then

Every character in the build is a **PLACEHOLDER** with correct 1.82 m
proportions — hips at 0.92, shoulders at 1.52 — so that a real model dropped in
lands at the same scale the cameras were framed against. They are labelled as
placeholder in `docs/STATUS.md` and will stay labelled until replaced.

§21 is explicit that placeholder geometry may not ship as final artwork. Nothing
here pretends otherwise.
