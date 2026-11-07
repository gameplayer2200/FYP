# Unity 2022.3 LTS — Deprecated APIs

**Project version:** Unity 2022.3.62f3c1 · **Last verified:** 2026-11-01

> **Sourcing caveat, read first.** This file was rewritten 2026-11-01 when the
> project was re-pinned from Unity 6.3 to 2022.3.62f3c1. The entries below are
> limited to what this repository itself evidences (package lock, code usage) and
> to APIs whose status in 2022.3 is not in dispute. Anything that would require a
> fresh fetch of the 2022.3 manual is marked **NOT SOURCEABLE** rather than
> written from memory. When in doubt, consult
> `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/`.

---

## The single most important correction

The previous revision of this file said:

| Deprecated (WRONG for this project) | Replacement (WRONG for this project) |
|---|---|
| `Input.GetKey()` | `Keyboard.current[Key.X].isPressed` |
| `Input.GetKeyDown()` | `Keyboard.current[Key.X].wasPressedThisFrame` |
| `Input.GetAxis()` | `InputAction` callbacks |

**That guidance does not apply here and would break the build.**
`com.unity.inputsystem` is **not installed** (absent from `Packages/manifest.json`),
so `Keyboard.current` and `InputAction` do not resolve. Meanwhile the project has
**27 call sites** using `Input.GetKey` / `Input.GetAxis` / `Input.GetMouseButton` /
`Input.mouseScrollDelta`, which are the correct and supported API in 2022.3.

**Do not migrate the input layer.** See `modules/input.md`.

---

## Deprecated *within* 2022.3 — safe to know about

These are long-standing deprecations that predate 2022.3 and are uncontroversial:

| Deprecated | Replacement | Notes |
|---|---|---|
| `Application.LoadLevel` / `LoadLevelAsync` | `SceneManager.LoadScene` / `LoadSceneAsync` | Removed long before 2022.3. This project uses `SceneManager` — 0 usages of `LoadLevel` found. |
| `WWW` | `UnityWebRequest` | Removed. 0 usages in this project. |
| `GameObject.rigidbody` / `.camera` / `.audio` etc. shorthand | `GetComponent<Rigidbody>()` etc. | Removed. 0 usages of `.rigidbody` shorthand found. |
| `Object.FindObjectsOfType` returning unsorted results | Prefer explicit ordering | Not deprecated in 2022.3 — see note below. |

## Explicitly NOT deprecated in 2022.3

Verified against this repository's actual usage, which compiles today:

| API | Status here | Evidence |
|---|---|---|
| `Input.GetKey` / `GetKeyDown` / `GetKeyUp` / `GetAxis` / `GetAxisRaw` / `GetMouseButtonDown` / `mouseScrollDelta` | **Supported, in use** | **27 call sites** under `Assets/Scripts/` |
| `Object.FindObjectOfType<T>()` / `FindObjectsOfType<T>()` | **Supported, in use** | **79 call sites** under `Assets/Scripts/` + `Assets/Editor/` |
| `Resources.Load` | **Supported, in use** | 9 call sites |
| `OnGUI` / IMGUI | **Supported, in use** | 19 call sites (editor tooling + diagnostics) |
| Surface shaders (`#pragma surface`) | **Supported — this is a BiRP project** | 8 third-party shader files, all inside the FloodedGrounds world pack (3 in `Content/Shaders/`, 5 in `Resources/`), e.g. `Assets/Art/Worlds/FloodedGrounds/Content/Shaders/FG_PBR_TopBlend.shader` |
| `UnityEngine.AI.NavMeshBuilder` / `NavMesh.AddNavMeshData` | **Supported, in use** | `EnemyGhoul.cs:867-874` bakes NavMesh at runtime |

> `FindObjectOfType` is the one most likely to be "helpfully" modernised by an
> agent that has seen Unity 6 material: Unity 6 renamed these to
> `FindFirstObjectByType` / `FindAnyObjectByType`. **Those names do not exist in
> 2022.3.** Leave the 79 call sites alone.

## NOT SOURCEABLE

The following were not re-verified against the 2022.3 manual during this rewrite
and must not be stated as fact without checking:

- Exact deprecation status of individual `UnityEngine` members added or changed
  across the 2022.3 patch series (`.62f3c1` is a late patch).
- `UnityEngine.Experimental.*` namespace contents for 2022.3.
- Per-platform API availability tables.

Consult the 2022.3 Scripting API directly for these.
