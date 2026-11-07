# Unity Engine — Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 2022.3.62f3c1 (LTS) |
| **Installed at pin time** | `2022.3.62f3c1_1623fc0bbb97` — probed 2026-11-01 at `C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe`. Match. |
| **Release Date** | 2022.3 LTS line (not re-fetched this run) |
| **Project Pinned** | 2026-11-01 |
| **Last Docs Verified** | 2026-11-01 |
| **LLM Knowledge Cutoff** | May 2025 |
| **Risk Level** | **LOW** — 2022.3 LTS is within LLM training data (Unity coverage to ~2022 LTS) |

## This project is Unity 2022.3 LTS only

Pinned by operator instruction on 2026-11-01. There is **no Unity 6 target**.
Do not propose Unity 6 APIs, packages, or migrations as if they applied here.

`docs/engine-reference/unity/upgrade-path/` holds a previously curated **Unity 6.3
LTS** reference set. It is retained as *forward-looking material for a possible
future upgrade* and is **not a description of this project**. Nothing in it may be
applied to this codebase without an explicit upgrade decision.

## Project's actual stack — verified, not assumed

| Fact | Value | How it was established |
|---|---|---|
| Render pipeline | **Built-in (BiRP)** | `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline: {fileID: 0}`; no `com.unity.render-pipelines.*` in `Packages/manifest.json` |
| Input | **Legacy Input Manager** | `com.unity.inputsystem` absent from manifest; 27 call sites use `Input.*`; **0** files reference `UnityEngine.InputSystem` |
| UI | **UGUI + TextMeshPro** | `com.unity.ugui 1.0.0`, `com.unity.textmeshpro 3.0.7` |
| Physics | **PhysX** (3D) | `com.unity.modules.physics` |
| C# language level | **C# 9** | Unity 2022.3 Roslyn |
| Test framework | **`com.unity.test-framework 1.1.33`**, installed transitively via `com.unity.feature.development 1.0.1` | `Packages/packages-lock.json`; no `Assets/Tests` folder yet |
| Cinemachine | `com.unity.cinemachine 3.1.7` | manifest |
| glTF import | `com.unity.cloud.gltfast 6.14.1` | manifest |
| ProBuilder | `com.unity.probuilder 5.2.4` | manifest |

> **Note on `com.unity.test-framework`.** It is not a direct dependency in
> `manifest.json` — it arrives through `com.unity.feature.development`. That is
> enough for `commands.test` to run, but adding a direct entry is safer if test
> code ever needs to compile in a player build.

## What is safe to use here

Everything in the Unity 2022.3 LTS manual and Scripting API. In particular, and
in direct contrast to the retained Unity 6 notes:

- **`Input.GetKey` / `Input.GetAxis` / `Input.GetMouseButton` are NOT deprecated
  in 2022.3.** They are the project's input layer. Do not migrate them.
- **UGUI is the correct runtime UI choice here**, not a legacy fallback. UI
  Toolkit exists in 2022.3 but this project does not use it.
- **BiRP shaders, `Surface Shader`, and hand-written HLSL are correct.** URP
  Shader Graph patterns do not apply.
- **`MonoBehaviour` + `Update`/`LateUpdate` is the right pattern.** DOTS/Entities
  is not installed.

## Where to look next

| Need | File |
|---|---|
| APIs to avoid *within 2022.3* | `deprecated-apis.md` |
| Changes from 2021 LTS → 2022.3 | `breaking-changes.md` |
| Idiomatic 2022.3 patterns | `current-best-practices.md` |
| Subsystem detail | `modules/{rendering,input,ui,physics,audio,animation,navigation,networking}.md` |
| Optional packages actually installed | `PLUGINS.md` |
| Per-package detail | `plugins/{cinemachine,probuilder,timeline,gltfast}.md` |
| DOTS / Addressables | `plugins/{dots-entities,addressables}.md` — both are **stubs stating NOT INSTALLED**. They exist because the Unity specialist routing names those two agents, so their reference pointer has to resolve to something truthful |
| Possible future upgrade | `upgrade-path/` — **not applicable today** |
