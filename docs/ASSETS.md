# Asset licences

§52 requires the source and licence of every shipped asset to be tracked. This
file is that record. **Nothing ships unless it has a row here.**

## Current state: no external assets

Every visual in the build is generated at runtime from primitives and code —
turf, markings, goal, net, stands, crowd, characters. Provenance is therefore
trivially answerable: we made it.

| Asset | Source | Licence | Commercial use | Attribution | Notes |
|-------|--------|---------|----------------|-------------|-------|
| _(none yet)_ | | | | | |

## Before adding anything

§9 and §11 of the art direction, restated as a checklist:

1. **Find the licence.** A public GitHub repository is not a licence. If there
   is no `LICENSE` file and no explicit statement, treat it as all rights
   reserved and walk away.
2. **Check commercial use is permitted**, since this may become a commercial
   product.
3. **Check redistribution** — some licences allow use but not shipping inside a
   binary.
4. **Note attribution requirements**, and where the credit will actually appear.
5. **Confirm it is not extracted from another game.** §21 and §52 both forbid
   it, and a rip from a commercial football title is the single most likely way
   this project acquires a legal problem.
6. **Add the row before the commit that adds the file.**

## Explicitly forbidden

Carried from the original brief, and not negotiable:

- Art, audio, player likenesses, trademarks, logos, screenshots or recordings
  from any competitor game, football club, league or broadcast.
- Any real club, competition or player name or badge (§32).
- Anything ripped from a commercial game, however it is re-exported.

## A note on the previous project

Two audio clips in the earlier Carrom project came from YouTube shorts. They
worked, and their licence status was never established. That is exactly the
situation this file exists to prevent: a placeholder that quietly becomes a
shipped asset because nobody wrote down where it came from.
