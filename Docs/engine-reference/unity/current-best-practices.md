# Unity 2022.3 LTS — Current Best Practices

**Project version:** Unity 2022.3.62f3c1 · **Last verified:** 2026-11-01

Practices that fit **this** project's actual stack: Built-in RP, legacy Input,
UGUI/TextMeshPro, PhysX, C# 9, assembly definitions.

> Rewritten 2026-11-01 at re-pin time. The Unity 6 version of this file is
> preserved at `upgrade-path/current-best-practices.md` and does **not** apply.

---

## Project setup

### Stay on 2022.3 LTS
2022.3 is a supported LTS line and is what every scene, prefab and the 5 ADRs in
`Docs/adr/` were authored against. There is no upgrade mandate.

### Assembly definitions are already in place — use them
The project splits into six assemblies with one-way dependencies (see
`Docs/adr/0004`):

| Assembly | Path |
|---|---|
| `FYP.Core` | `Assets/Scripts/Core/FYP.Core.asmdef` |
| `FYP.Player` | `Assets/Scripts/Player/FYP.Player.asmdef` |
| `FYP.Interaction` | `Assets/Scripts/Interaction/FYP.Interaction.asmdef` |
| `FYP.World` | `Assets/Scripts/World/FYP.World.asmdef` |
| `FYP.Content` | `Assets/Scripts/Content/FYP.Content.asmdef` |
| `FYP.Editor` | `Assets/Editor/FYP.Editor.asmdef` |

New runtime code belongs in the existing assembly that matches its layer. Adding
a reference that points "backwards" against the ADR-0004 dependency direction is
a design defect, not a convenience.

### Script moves must use `AssetDatabase.MoveAsset`
Moving a `.cs` file with the filesystem loses its `.meta` GUID and every scene
and prefab reference to it turns into `Missing`. This is documented in
`AGENTS.md` §7 item 8 and is a real, previously-hit failure in this repo.

## Rendering (Built-in RP)

- **Surface Shaders and hand-written HLSL** are the supported authoring path. 8
  such shaders already ship under `Assets/Art/Worlds/FloodedGrounds/`.
- **No Shader Graph, no Renderer Features, no URP Volume framework.** Post
  processing here is either per-camera image effects or none.
- **`Custom/PortalPanelProj`** (`Assets/Shaders/PortalPanelProj.shader`) is a
  project-critical BiRP shader — world-space projection sampling, deliberately
  not mesh-UV. Do not convert it to a URP pattern.
- **Built-in RP has a limited per-pixel light count.** This was the root cause of
  "the flashlight does not light Metro": 66 lights exhausted the per-pixel budget
  and everything fell back to vertex lighting. The fix was `Light.renderMode =
  ForcePixel` on the flashlight cone plus a near-field point light. Keep that in
  mind before adding more realtime lights to a scene.

## Input (legacy Input Manager)

- `Input.GetKey(KeyCode.X)`, `Input.GetKeyDown`, `Input.GetAxis("Horizontal")`,
  `Input.GetMouseButton(0)` are correct and supported.
- The project funnels all E-key interaction through a single
  `InteractionRunner` component (`Assets/Scripts/Interaction/`). Do not add a
  second component that reads the E key — that is an explicit architectural rule
  (`AGENTS.md` §2, "唯一 E 键"). The one existing exception is `ChargeStation`'s
  global cancel-while-charging poll (`ChargeStation.cs:44`), which is guarded by
  `BatterySystem.IsCharging`, its own distance check, and a 0.25 s same-press
  lock. Match that bar or route through `InteractionRunner`. See `modules/input.md`.
- **`F` is the single "use item" key**, and which item it drives is decided by
  `PlayerGear.UseKeyOwner` (= `SelectedIndex`, cycled by the mouse wheel). Three
  components read `KeyCode.F` — `Flashlight`, `EmfDetector`, `OrderTerminal` —
  plus `PowerWire` for the puzzle — and each must first check that the selected
  slot is its own, or one press would light the torch *and* scan. This is the
  same single-owner discipline as E, applied to F.
- `Input.GetKeyDown` in `Update` is frame-rate independent in the sense that it is
  true for exactly one frame; do **not** move input polling into `FixedUpdate`.

## UI (UGUI + TextMeshPro)

- `Canvas` / `RectTransform` / `LayoutGroup` / `TextMeshProUGUI` are the runtime
  UI primitives. There is no UI Toolkit runtime UI in this project.
- The CRT terminal aesthetic is delivered by `CrtTheme` / `CrtOverlay`
  (`Assets/Scripts/Core/`), not by a UI framework feature.
- IMGUI (`OnGUI`) is used in 19 places — mostly editor tooling
  (`Assets/Editor/MetroSceneDev.cs`, `FactorySceneDev.cs`), the `PT_*` test
  drivers, and on-screen diagnostics (`LoadDiag`). That is fine for editor tools;
  do not build new runtime HUD in IMGUI.
- **Any new player-facing Chinese string must live in a `.cs` file**, and
  `FYP/UI/生成字体预热字符表` (`Assets/Editor/CrtCharsetGenerator.cs`) must be
  re-run afterwards. The HUD font is a 16 MB dynamic OTF whose atlas rebuild costs
  142 ms once it reaches 4000 glyphs; `CrtFontPrewarm` pre-requests every character
  found in `Assets/Scripts` during the loading screen. A string added without
  re-running the generator reintroduces a first-glyph hitch. See `modules/ui.md`.

## Scripting patterns

### Cache component lookups; do not do them per frame
`FindObjectOfType` and `GetComponent` in `Update` are the usual cause of frame
spikes. The project already uses 79 `FindObjectOfType*` calls — mostly in `Awake`
/ `Start` / one-shot setup, which is correct. Do not add per-frame calls.

### `OnEnable` is where cached state must be re-initialised
Domain reload clears static registries (`PlayerRef`, `BatterySystem.Instance`,
`Inventory.Instance`) and `Awake` does not re-run for already-loaded objects.
`LightGroup`, `UnreachableLightAnomaly` and `PoweredLight` already initialise in
`OnEnable` for this reason. Follow that pattern for anything caching a singleton.

### Null-check Unity objects with `ReferenceEquals` first
`as Component == null` gives a false negative for destroyed-but-not-null Unity
objects. `InteractionRunner.IsAlive` documents this; follow the same shape.

### `[SerializeField] private` over `public` fields
Expose inspector fields as `[SerializeField] private T name;` — **no underscore
prefix**. Measured across `Assets/Scripts/`: `[SerializeField] private float
rtHeight`, `[SerializeField] private int knobCapacity`, etc. (`EnemyGhoul.cs` has
a single `private float _footOffset`, a runtime cache, not an inspector field.)
Earlier drafts of this file said `_camelCase`; that does not match the code.

## Physics (PhysX)

- 3D PhysX only; `com.unity.modules.physics` is present, and 2D physics is unused.
- Character movement is **not** Rigidbody-driven — `PlayerMovement` uses a
  `CharacterController`. Do not convert it to a Rigidbody controller without a
  design decision; the portal crossing math depends on its transform behaviour.
- Colliders on doors are authored per-door; the portal trigger is a mathematical
  plane test in `PortalWindow.LateUpdate`, not a collider. Do not "fix" it by
  adding a trigger volume.

## Performance

- Budget: 60 fps on desktop, 16.6 ms frame. No hard numbers are recorded in this
  repo beyond that; treat it as the target until profiled.
- The heaviest known cost is realtime lighting in the Metro scene (66 lights).
  Measure before adding lights.
- **Enemy NavMesh is baked at runtime, per scene, synchronously.** `EnemyGhoul`
  calls `NavMeshBuilder.BuildNavMeshData` on first load of a world and re-bakes a
  box up to 400 m across when a chase leaves the baked area. A 150 m footprint is
  real CPU on that frame; do not add many enemies per scene without measuring, and
  do not shorten `rebakeCooldown` (default 2 s) — it becomes a rebake loop. See
  `modules/navigation.md`.
- **`SoundOcclusion` raycasts 3 sources per frame** (`probesPerFrame = 3`,
  `probeInterval = 0.22`) against layer mask `~0`, i.e. everything. It auto-spawns
  and is `DontDestroyOnLoad`; do not add a second one. See `modules/audio.md`.

## Testing

- `com.unity.test-framework 1.1.33` is installed **transitively** via
  `com.unity.feature.development 1.0.1`. There is **no `Assets/Tests` folder** and
  **no test assembly definition** yet.
- `commands.test` in `project.yaml` runs EditMode tests. It will report
  `testcasecount="0"` until a test assembly exists — **that is not a pass.**
- The project's current verification practice is different and is documented in
  `AGENTS.md` §5: `PortalWindow.SelfTest` via MCP `execute_code`, plus the
  natural-pipeline regression checklist. That is the authoritative gate for
  door/portal work.
- Editor-side tooling is driven through `MetroSceneDev` / `FactorySceneDev`
  staged methods — an established pattern worth extending rather than replacing.

## Version control

- Trunk-based on `main`. `Assets/` is **3.3 GB** (after the 2026-10-31 slimming
  commit `f21ba07`; it was 6.7 GB before). Large binaries go through **Git LFS** —
  `.gitattributes` routes `*.prefab`, `*.unity`, `*.psd`, `*.blend` (and more) to
  `filter=lfs`. If `git lfs` is not installed, those files check out as pointer
  text and every scene/prefab is unusable.
- Never commit during an active Play session (asset refresh mid-play breaks
  material/window state — `AGENTS.md` §7 item 1).
