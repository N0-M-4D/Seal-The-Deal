# PERFORMANCE

**Status:** Current · **Last updated:** 2026-10-03

## What the player sees

A steady frame rate locked to the monitor (vSync on), on the PC quality level. Frame drops in the greybox were reported on 2026-10-03; nothing here has been measured yet, so every cause below is a finding from reading the project, not from the Profiler (AGENTS.md §8).

## Changed on 2026-10-03

| Change | Where | Why |
|---|---|---|
| vSync on (was off, with no frame cap) | `ProjectSettings/QualitySettings.asset`, PC level | Uncapped frames swing from frame to frame against the fixed 60 Hz tick, so some frames run two physics ticks and some none. That reads as stutter even at a high average. |
| Each building's still parts are merged after it is built | `TowerBuilder.BuildBuilding`, each template's **Never Moves** list | Floors are instantiated at runtime, so Unity's build-time static batching never touches them: about 26 floors × ~40 boxes, each drawn separately and again per shadow cascade. Opt-in per template, because merging freezes things in place. |
| Shadow cascades 4 → 2 | `Assets/Settings/PC_RPAsset.asset` | Each cascade redraws every shadow caster. Two is plenty at 50 m shadow distance in a greybox. |

## Still suspected, in order

1. **Client reconcile replays physics for the whole scene.** On a client, every reconcile rewinds and re-runs the ticks since the host's state, and each re-run is a full `Physics.Simulate` over every collider and all the networked props (about 140 across both towers). At 100 ms ping that is several extra simulations per tick. Expect clients to drop frames where the host doesn't. Check: Profiler on a client, `Physics.Simulate` call count per frame under FishNet's prediction.
2. **SSAO, HDR and high-quality soft shadows** on the PC renderer, all full-screen cost. Turn SSAO off in `PC_Renderer` as a quick A/B test.
3. **Props.** About 140 `NetworkTransform`s checking every few ticks on the host. The prop cap per floor (PROPS.md) and freezing props on floors with no player nearby (GDD) are the planned fixes.
4. **Testing in the Editor.** An open Scene view or Inspector repaints every frame. Judge frame rate in a build, never in the Editor.

## How to measure

Make a Development Build with Autoconnect Profiler, run host plus one client, and stand in the tower. Record the CPU main thread and GPU time per frame, and the counts for draw calls (`Batches` in Stats) and `Physics.Simulate`. Write the numbers here before deciding the next cut.

## Known limitations

- vSync has no in-game toggle yet; settings don't exist (MENU_AND_HUD.md).
- Merging happens once per build of the tower; a rebuild re-merges. Anything that moves must stay out of a template's Never Moves parts, destructible panels (roadmap) included. Unity's Static checkbox can't be used instead: it is Editor-only and means nothing in a built game.
