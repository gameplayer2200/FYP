# DOTS / Entities — NOT INSTALLED IN THIS PROJECT

**Project version:** Unity 2022.3.62f3c1 · **Last verified:** 2026-11-01

## Read this before advising anything

`com.unity.entities`, `com.unity.burst`, and `com.unity.collections` are **not
dependencies of this project**. There is no `Unity.Entities` namespace reference
anywhere under `Assets/`, no `Baker<>`, no `ISystem`, no `SystemBase`.

If a request arrives here, the correct first response is: **DOTS is not
installed in this project; all gameplay is MonoBehaviour + Update/LateUpdate.**
Do not propose an ECS migration as if the packages were present.

## What this project actually uses instead

| Need | Pattern used here |
|---|---|
| Per-frame gameplay logic | `MonoBehaviour.Update` / `LateUpdate` (e.g. `PortalWindow.TryCross` in `LateUpdate`) |
| Many lightweight objects | Plain GameObjects + `ParticleSystem`; no instancing layer |
| Perf headroom | Static batching, LOD, and the unpack tool in `Assets/Editor/` |
| Data authoring | `ScriptableObject` assets under `Assets/WorldLinks/` and `Assets/Prefab/Tables/` |

## If DOTS is ever genuinely wanted

That is an engine-stack decision, not a code change. It requires:

1. Adding the packages pinned to a **2022.3-compatible** version (Entities 1.x
   requires Unity 2022.2+; verify the exact package version against
   `https://docs.unity3d.com/Packages/` for 2022.3 before committing).
2. A written ADR — this project records engine-level decisions in `Docs/adr/`.
3. An assembly-definition plan: `Docs/adr/0004` fixes the assembly layering
   (`FYP.Core` → `FYP.Player`/`FYP.World`/…), and ECS code cannot live inside the
   existing MonoBehaviour assemblies without breaking that contract.

Reference material from a previous Unity 6.3 curation pass is preserved at
`../upgrade-path/plugins/dots-entities.md`. It describes a **different engine
version** and does not apply here.
