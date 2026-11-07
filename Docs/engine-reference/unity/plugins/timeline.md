# Timeline 1.7.7 — Project Reference

**Project version:** Unity 2022.3.62f3c1 · Timeline 1.7.7 · **Last verified:** 2026-11-01

---

## Status in this project: installed, unused

- `com.unity.timeline 1.7.7` is a direct dependency.
- **No `.playable` asset exists anywhere in `Assets/`**, and no
  `PlayableDirector` component is present in any scene.
- All sequencing is **coroutine-based** (`AnywhereDoor`, `GhostArrivalEvent`,
  `GhostProduction`, `PatrolBeam`, `MetroBlackout`, `FactoryShiftController`).

## Why it is installed anyway

Timeline ships as part of the default Unity feature set and is a dependency of
several editor packages. Its presence is not evidence that the project uses it.

## Do not introduce Timeline for gameplay logic here

This is a considered recommendation, not just conservatism:

1. **`WorldStreamer` unloads whole scenes.** A `PlayableDirector` bound to objects
   in a scene that gets unloaded mid-sequence will produce null bindings and
   silent failures. The streaming model and Timeline's binding model are a poor
   fit.
2. **Existing sequences are stateful coroutines** that interact with static
   singletons (`EmfStorm`, `BillingLedger`, `ExtractionClock`). Reproducing that
   in Timeline tracks means either custom tracks or duplicating the logic.
3. **Testing.** The project's verification path is MCP `execute_code` calling
   methods reflectively. A Timeline asset is far harder to drive and assert than a
   coroutine entry point.

Timeline is reasonable for a **non-interactive, in-scene cutscene** that plays in
one world without streaming. Nothing in the current vertical slice needs that.

## If you do use it

- Bind tracks by **exposed reference** or explicit binding in code, not by scene
  path — paths break when the world is re-slotted (see `AGENTS.md` §4).
- `PlayableDirector.playOnAwake` + a director in a streamed scene will start when
  the scene loads, which may be far from the player.
- Audio tracks route through `AudioSource`; the project's audio is procedurally
  generated WAVs (`modules/audio.md`), which Timeline handles fine as clips.
