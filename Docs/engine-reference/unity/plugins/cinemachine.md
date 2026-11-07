# Cinemachine 3.1.7 — Project Reference

**Project version:** Unity 2022.3.62f3c1 · Cinemachine 3.1.7 · **Last verified:** 2026-11-01

---

## Status in this project: installed, effectively unused

- `com.unity.cinemachine 3.1.7` is a direct dependency in `Packages/manifest.json`.
- **No `CinemachineBrain`, `CinemachineCamera` / `CinemachineVirtualCamera`, or any
  Cinemachine component exists in any scene or prefab.**
- The only trace is the **tag** `CinemachineIgnored`, defined in
  `ProjectSettings/TagManager.asset` and applied to 182 objects inside
  `Assets/Scenes/OfficePack.unity` and its prefab. That tag is how Cinemachine 3
  excludes objects from its own processing — it is inert without a brain.

The player camera is a plain `Camera` parented to the player, driven by
`PlayerMovement`. Do not replace it with a Cinemachine rig without a concrete
reason; the portal system's off-axis projection camera and the flashlight cone
both hang off that camera's transform.

## If you do adopt Cinemachine here

**Version matters a lot.** This is **Cinemachine 3.x**, whose API differs
substantially from the 2.x material that most tutorials and most LLM training data
describe:

| Cinemachine 2.x | Cinemachine 3.x |
|---|---|
| `CinemachineVirtualCamera` | `CinemachineCamera` |
| `CinemachineFreeLook` | `CinemachineFreeLook` → built from `CinemachineOrbitalFollow` |
| `vcam.m_Priority` (public field) | `vcam.Priority` (property) |
| `CinemachineCore` singleton idioms | largely reworked |
| Body/Aim as separate components | `CinemachineFollow` / `CinemachineRotationComposer` etc. |

Confirm against the installed package docs under
`Library/PackageCache/com.unity.cinemachine@3.1.7/` rather than recalling 2.x
snippets.

## Interactions to watch in this project

1. **`PortalWindow` builds its own camera** (`portalCam`) with an off-axis
   projection matrix and renders the paired world. A Cinemachine brain on the main
   camera does not interfere, but do not parent the portal camera under a
   Cinemachine rig.
2. **The flashlight is parented to the player camera transform.** Swapping the main
   camera for a Cinemachine-driven one means re-parenting the flashlight and the
   interaction ray origin.
3. **Screenshots and tests** (`manage_camera` game-view capture,
   `MetroSceneDev.Shot`) assume `Camera.main`. A Cinemachine setup that changes
   which camera is tagged `MainCamera` will silently break those.
