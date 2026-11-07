# Unity 2022.3 — Audio Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project audio:** Built-in `AudioSource` / `AudioClip`, plus a runtime occlusion
system (`SoundOcclusion`). No FMOD/Wwise, **no `AudioMixer` asset at all**.

---

## How this project produces sound

Most audio is **procedurally synthesised into WAV files** by editor tooling, then
played as ordinary `AudioClip`s. There is no middleware, and no mixer.

| File | Source | Purpose |
|---|---|---|
| `Assets/Audio/Metro/emf_storm.wav` | `MetroSceneDev.Stage4_MakeSfx` | 50 Hz hum + white noise + crackle — EMF storm |
| `Assets/Audio/Metro/arrival_pressure.wav` | same | Brown-noise low-passed rumble — ghost arrival |
| `Assets/Audio/Factory/factory_bell.wav` | `FactorySceneDev.Stage0_Sfx` | Shift-change bell |
| `Assets/Audio/Factory/factory_hum.wav` | same | Machine hum |
| `Assets/Audio/Factory/factory_clank.wav` | same | Metal clank |
| `Assets/Audio/Factory/factory_sting.wav` | same | Patrol-beam sting |
| `Assets/Audio/Factory/gate_roll.wav` | same | Gate rolling |

Regenerating them means re-running the relevant `Stage*_Sfx` method — **the
generation code is the source of truth, not the WAV files.**

## `SoundOcclusion` — the one audio system with real logic

`Assets/Scripts/World/SoundOcclusion.cs` (208 lines) makes walls muffle sound, so
the player can hear *where* something is rather than just that it is nearby.

- **Auto-spawns.** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` creates a
  `DontDestroyOnLoad` GameObject named `Sound Occlusion` (`:80`–`:86`). It is not placed
  in any scene — **do not add it manually**, you will get two.
- `[DefaultExecutionOrder(-30)]`.
- **Detection** (`:139`): a single `Physics.Raycast(ear, dir, d * 0.92f, ~0,
  QueryTriggerInteraction.Ignore)` from the listener to each source. Any hit →
  occluded. The `~0` layer mask means **everything** blocks sound, including
  layer-isolated world geometry.
- **Effect**: volume × `occludedVolume` (0.34) plus an `AudioLowPassFilter` cut to
  `occludedCutoff` (760 Hz) versus `openCutoff` (21000 Hz). Filtering, not just
  attenuation — lowering volume alone reads as "farther away", not "behind a wall".
- **Per-source state is cached**: `baseVolume` is captured once and multiplied,
  never written back. If you edit an `AudioSource` volume at runtime, tell
  `SoundOcclusion` (it re-scans) or your value is overwritten by the blend.
- **Budgets**: `probeInterval = 0.22` s, `probesPerFrame = 3` (round-robin cursor),
  `minDistance = 2.5` (skip close sources), `maxDistance = 60`, `blendSpeed = 5.5`.
- **Never hard-switches.** Blending is what keeps it from clicking.
- `bypass = true` disables occlusion for A/B comparison.
- Public read-only counters for Play-mode tests: `OccludedCount`, `TrackedCount`.

## Playback patterns in use

- **3D positional sources** for world ambience (tunnel depth, machine rooms).
  Set `spatialBlend = 1.0` and configure `rolloffMode`/`minDistance`/`maxDistance`;
  the default logarithmic rolloff with `minDistance = 1` is too quiet at gameplay
  distances.
- **2D sources** for HUD/feedback stings (`spatialBlend = 0`).
- **One-shot vs. loop**: `PlayOneShot` for stings, `loop = true` + `Play()` for
  beds. Stop loops explicitly on state change — a leaked looping source is audible
  forever.

## Pitfalls

- **`AudioSource.PlayOneShot` on a disabled GameObject does nothing.** If a sting
  goes silent after a scene state change, check that the host object is active.
- **`AudioListener` must be on exactly one active object** — normally the player
  camera. Development scenes contain extra players (`Player(Metro开发)`,
  `Player(工厂开发)`); the dual-player guard disables the extras, but a stray
  listener produces "audio is muffled / doubled". `SoundOcclusion` resolves the
  listener via `FindObjectOfType<AudioListener>()`, so a second one silently
  changes what every occlusion ray measures from.
- **`SoundOcclusion` raycasts against `~0`.** A new collision-only or invisible
  blocker will muffle sound. That is usually what you want; it is a surprise when
  it is not.
- **Pitch shifts change duration.** The ghost-arrival sequence relies on a slow
  pitch ramp; changing it changes the 9 s timing.
- **`AudioClip` import settings matter**: the generated WAVs are short. Keep
  `Load Type = Decompress On Load` for stings, `Streaming` for long beds.
- **`AudioLowPassFilter` is added at runtime** by `SoundOcclusion` (`:188` looks
  it up, and adds one if absent). Do not also add one in the Inspector — two
  filters double the attenuation.

## What is NOT here

- **No `AudioMixer` asset exists in the project** (verified: no `.mixer` file
  under `Assets/`). Volume control is per-source. If you add a mixer, route every
  source through groups in one pass rather than ad hoc, and check `SoundOcclusion`
  still finds its sources.
- **No spatial audio plugin.** Occlusion is the custom raycast system above.
- **Reverb is only in `Flooded_Grounds.unity`** (`AudioReverbZone` present). Metro
  and the factories have none — their space is conveyed by the 3D sources and
  occlusion, not by reverb.
