# Technical Preferences

<!-- project.yaml at the repo root is the machine-readable source of truth for
     engine, specialists, naming, performance, platform, and testing.framework.
     This file is the human-readable LEGACY FALLBACK: agents and skills resolve
     each key from project.yaml first and fall back here only when the
     project.yaml key is absent. /setup-engine dual-writes both.
     Forbidden patterns and allowed libraries are NOT migrated — they live only
     in this file. Populated by /setup-engine; updated as decisions are made. -->

> Populated 2026-11-01 for Unity 2022.3.62f3c1. Every value below is **verified
> against the repository**, not assumed — the source is noted inline where it
> matters. Engine reference: `docs/engine-reference/unity/VERSION.md`.

## Engine & Language

- **Engine**: Unity 2022.3.62f3c1 (LTS) — `ProjectSettings/ProjectVersion.txt`
- **Language**: C# (C# 9, Unity 2022.3 Roslyn)
- **Rendering**: Built-in Render Pipeline (BiRP) — `ProjectSettings/GraphicsSettings.asset` has `m_CustomRenderPipeline: {fileID: 0}`; no `com.unity.render-pipelines.*` in the manifest
- **Physics**: PhysX (3D) — `com.unity.modules.physics`; 2D physics unused

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Windows) only — `FYP_Game_Proposal.md`: 单机（无联机）, PC (Windows)
- **Input Methods**: Keyboard / Mouse
- **Primary Input**: Keyboard/Mouse (first-person: WASD + mouse look)
- **Gamepad Support**: None implemented. Partial is the recommended target for a PC title if gamepad is ever wanted, but no gamepad bindings exist today — `PlayerMovement` reads only the legacy axes `Horizontal`/`Vertical`/`Mouse X`/`Mouse Y`
- **Touch Support**: None
- **Platform Notes**:
  - **Legacy Input Manager only.** `com.unity.inputsystem` is NOT installed; all 27 input call sites use `UnityEngine.Input`. Do not migrate to the new Input System without an explicit decision.
  - The **single E-key owner** rule is architectural: `InteractionRunner` is the only component that polls `KeyCode.E`. New interactables implement `IInteractable` and register with `InteractionRegistry` — they never poll input themselves.
  - Fixed bindings: `WASD` move · mouse look · `E` interact · `F` flashlight · `Tab` order-board detail · `Space` jump (charged super-jump costs 1 battery).
  - Automated tests cannot rely on real input: when the Editor is unfocused the player loop freezes, so tests drive `Update`/`LateUpdate` reflectively (`AGENTS.md` §6).

## Naming Conventions

- **Classes**: PascalCase (e.g. `PlayerMovement`, `PortalWindow`)
- **Variables**: camelCase for locals/parameters and for private fields — **no underscore prefix** (measured: `[SerializeField] private float rtHeight;`, `private int rtHeight`, etc.; the framework's `_camelCase` Unity default does not match this repository)
- **Signals/Events**: `PascalCase On<Event>` (e.g. `MetroBlackout.OnPowerRestored`)
- **Files**: PascalCase matching the primary class (`PortalWindow.cs`)
- **Scenes/Prefabs**: Scenes are PascalCase (`SpecimenLab.unity`, `FactoryDay.unity`). **Prefabs are mixed — measured, not a single convention:** PascalCase for system prefabs (`ChargeStation.prefab`, `GhoulEnemy.prefab`), lowercase for the player (`player.prefab`), kebab-case for portal doors (`anywhere-door-Metro.prefab`), and CJK for gameplay props (`道具_手电筒_Lab.prefab`). **Do not rename existing prefabs** — scene references depend on the paths. Match the neighbouring files in whichever folder you are adding to rather than imposing one style globally.
- **Constants**: PascalCase for `const` / `static readonly` (e.g. `Duration`, `SlotSize`, `PadX`, `FrameBudgetMs`, `CrawlSeconds`) — **measured: every constant in `Assets/Scripts/` is PascalCase; there is not a single SCREAMING_SNAKE constant.** The framework's Unity default (`SCREAMING_SNAKE`) does not match this repository; follow the code, not the default.

## Performance Budgets

- **Target Framerate**: 60 fps on desktop
- **Frame Budget**: 16.6 ms
- **Draw Calls**: [NOT SET — no measured baseline in this repo]
- **Memory Ceiling**: [NOT SET — `Assets/` is ~3.3 GB on disk after the 2026-10-31 slimming commit `f21ba07` (it was 6.7 GB before); runtime `Texture2D` was measured at 1737 MB on 2026-10-27, which is the only recorded figure]

> These are **targets, not measurements**. Nothing in the repository records a
> profiled frame time. The heaviest known cost is realtime lighting in Metro
> (66 lights, which exhausted the Built-in RP per-pixel light budget — see
> `docs/engine-reference/unity/modules/rendering.md`).

## Testing

- **Framework**: NUnit via `com.unity.test-framework 1.1.33` — installed
  **transitively** through `com.unity.feature.development 1.0.1`. There is **no
  `Assets/Tests` folder and no test assembly definition yet.**
- **Minimum Coverage**: [NOT SET]
- **Required Tests**: Balance formulas, gameplay systems, networking (not applicable — single-player)
- **Current authoritative gate** (this is what actually gets run):
  - `PortalWindow.SelfTest` via MCP `execute_code` — numeric portal-projection
    self-check (`AGENTS.md` §5.1)
  - The natural-pipeline regression checklist (`AGENTS.md` §5.2): preload →
    open → cross → return → no false triggers → door sync → both windows correct
    → console clean
  - Editor-side staged builders `MetroSceneDev` / `FactorySceneDev` (`PT_*`
    play-test drivers) for scene-level verification
- **Warning**: `commands.test` will report `testcasecount="0"` until a test
  assembly exists. **Zero tests is not a pass.**

## Forbidden Patterns

These are established invariants in this codebase, each with a source. They are
not style preferences — violating one has already produced a real bug.

- **A second component polling `KeyCode.E`** — double-trigger bugs. `InteractionRunner` is the sole owner (`AGENTS.md` §2).
- **Moving a `.cs` file with the filesystem** — loses the `.meta` GUID and turns every scene/prefab reference into `Missing`. Use `AssetDatabase.MoveAsset` (`AGENTS.md` §7 item 8, ADR-0004).
- **Adding a trigger collider to "fix" portal crossing** — the crossing test is a mathematical plane test in `PortalWindow.LateUpdate`; a collider creates double-fire (`docs/engine-reference/unity/modules/physics.md`).
- **Converting the player from `CharacterController` to `Rigidbody`** — the portal crossing math and movement feel both depend on it (`modules/physics.md`).
- **`FindFirstObjectByType` / `FindAnyObjectByType`** — Unity 6 names that **do not exist in 2022.3**. Use `FindObjectOfType` (`docs/engine-reference/unity/deprecated-apis.md`).
- **`Keyboard.current[...]` / `InputAction`** — `com.unity.inputsystem` is not installed; this does not compile (`modules/input.md`).
- **URP/HDRP APIs: `UniversalRenderPipelineAsset`, Renderer Features, `VolumeProfile`** — this is a BiRP project (`modules/rendering.md`).
- **Per-frame `FindObjectOfType` / `GetComponent`** — cache in `Awake`/`OnEnable` (`current-best-practices.md`).
- **`as Component == null` for liveness checks** — false negative on destroyed Unity objects. Use `ReferenceEquals` first, as `InteractionRunner.IsAlive` documents (`AGENTS.md` §7 item 7).
- **Creating or deleting files under `Assets/` while Play mode is running** — asset refresh invalidates live material/window state (`AGENTS.md` §7 item 1).
- **Changing `com.coplaydev.unity-mcp` from `file:` to a git URL** — UPM on this machine fails to resolve it with `not in a git directory` (`AGENTS.md` §1).
- **Deleting a prefab a scene instance references** — the instance becomes a "Missing Prefab" shell; edit the scene first, then delete (`AGENTS.md` §7 item 13).
- **Unity 6 APIs in general** — see `docs/engine-reference/unity/deprecated-apis.md`; the project is pinned to 2022.3 LTS with no upgrade target.

## Allowed Libraries / Addons

Only what is actually installed and in use. Adding an entry here means the
dependency is being integrated now, not that it might be useful later.

- `com.unity.ugui 1.0.0` — runtime UI (Canvas/UGUI)
- `com.unity.textmeshpro 3.0.7` — text rendering (separate package in 2022.3)
- `com.unity.cloud.gltfast 6.14.1` — glTF/GLB import; see `plugins/gltfast.md` for the texture-memory trap
- `com.unity.timeline 1.7.7` — installed, **unused**; not the project's sequencing model
- `com.unity.cinemachine 3.1.7` — installed, **no components in use** (only the `CinemachineIgnored` tag)
- `com.unity.probuilder 5.2.4` — installed, **unused**; editor-side modelling only
- `com.unity.visualeffectgraph 14.0.12` — installed, **no `.vfx` assets**; limited BiRP support
- `com.unity.visualscripting 1.9.4` — installed, **unused**; all logic is C#
- `com.unity.collab-proxy 2.12.4` — Version Control window plumbing
- `com.unity.feature.development 1.0.1` — feature set (brings test-framework + code coverage + IDE integration)
- `com.coplaydev.unity-mcp` (`file:` local package) — MCP editor bridge; see `PLUGINS.md`

**Not installed, and not to be added casually:** URP/HDRP, Input System, DOTS
(Entities/Jobs/Burst as direct deps), Addressables, Netcode/Mirror, AI Navigation
(`com.unity.ai.navigation`), FMOD/Wwise.

## Architecture Decisions Log

- `Docs/adr/0001-single-portal-implementation.md` — one portal implementation, no per-world variants
- `Docs/adr/0002-cross-scene-refs-are-data.md` — cross-scene references are resolved at runtime, never serialized
- `Docs/adr/0003-worlds-stay-resident.md` — worlds stay resident; loading decisions belong to one arbiter
- `Docs/adr/0004-assembly-layering.md` — six assemblies, one-way dependencies
- `Docs/adr/0005-world-streamer.md` — `WorldStreamer` is the sole authority on scene load/unload

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: `unity-specialist`
- **Language/Code Specialist**: `unity-specialist` (C# review — the primary covers it)
- **Shader Specialist**: `unity-shader-specialist` — **with a caveat**: this project is BiRP, so route shader work here for HLSL/Surface Shaders and `Custom/PortalPanelProj`, **not** for Shader Graph (which has no supported path here)
- **UI Specialist**: `unity-ui-specialist` — for UGUI Canvas/TextMeshPro work; **not** UI Toolkit runtime UI, which this project does not use
- **Additional Specialists**: `unity-dots-specialist` and `unity-addressables-specialist` are listed because the Unity routing table names them, but **neither package is installed** — no `com.unity.entities`/`burst`/`collections` and no `com.unity.addressables` in `Packages/manifest.json`. Both reference pointers resolve to stubs that say so (`docs/engine-reference/unity/plugins/dots-entities.md`, `.../addressables.md`).
- **Routing Notes**: Invoke the primary for architecture, general C# and scene/prefab work. Invoke the shader specialist for rendering and materials — but brief it on BiRP first, since most shader material it has seen is URP. Invoke the UI specialist for HUD work, and point it at `CrtTheme`/`CrtOverlay` and the `renderOwner` pattern. Do **not** spawn the DOTS or Addressables specialists for this project as it stands — neither system is in use.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (`.cs`) | `unity-specialist` |
| Shader / material files (`.shader`, `.mat`, `.cginc`, `.compute`) | `unity-shader-specialist` |
| UI / screen files (Canvas prefabs, `.uxml`, `.uss`) | `unity-ui-specialist` |
| Scene / prefab / level files (`.unity`, `.prefab`) | `unity-specialist` |
| Native extension / plugin files (`.dll`, native plugins) | `unity-specialist` |
| Editor tooling (`Assets/Editor/*.cs`) | `unity-specialist` |
| General architecture review | `unity-specialist` |
