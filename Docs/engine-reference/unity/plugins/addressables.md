# Addressables — NOT INSTALLED IN THIS PROJECT

**Project version:** Unity 2022.3.62f3c1 · **Last verified:** 2026-11-01

## Read this before advising anything

`com.unity.addressables` is **absent from `Packages/manifest.json`**. There is no
`Addressables` namespace reference, no `AssetReference`, no group settings asset,
and no `Addressables.LoadAssetAsync` call anywhere under `Assets/`.

If a request arrives here, the correct first response is: **Addressables is not
installed in this project.** Do not write Addressables code or propose an
asset-management migration as if the package were present.

## What this project actually uses instead

| Need | Pattern used here |
|---|---|
| Load a specific resource by string | `Resources.Load` (9 call sites; paths live under `Assets/Resources/`) |
| Stream whole worlds in and out | **`WorldStreamer`** — its own scene load/unload arbiter (ADR-0005), not Addressables |
| Prefabs in scenes | Direct serialized references (`Assets/Prefab/`) |
| Party-of-worlds preload | `WorldLink.preloadRadius` (15 m) drives `WorldStreamer` |

`WorldStreamer` already owns world streaming deliberately: three rules (preload
on approach, unload when far, cap at `MaxLoadedWorlds=5`) plus never-unload
guards for the lab, the player's current world, and any world with an open door.
Replacing it with Addressables would be a redesign of ADR-0005, not a refactor.

## Constraint that matters most here

`Assets/Resources/` internal paths are referenced by **string** from
`Resources.Load`. Moving files out of `Resources/` into an Addressables group
silently breaks those 9 call sites. `AGENTS.md` §8 records the same warning.

## If Addressables is ever genuinely wanted

1. Add the package pinned to a 2022.3-compatible version.
2. Write an ADR superseding ADR-0005 — the streaming arbiter is load-bearing.
3. Migrate `Resources.Load` call sites and delete the string paths only after the
   new path is proven.

Reference material from a previous Unity 6.3 curation pass is preserved at
`../upgrade-path/plugins/addressables.md`. It describes a **different engine
version** and does not apply here.
