# Unity 2022.3 — UI Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project UI stack:** UGUI (`com.unity.ugui 1.0.0`) + TextMeshPro (`com.unity.textmeshpro 3.0.7`).
UI Toolkit runtime UI is **not** used.

---

## Which UI system to reach for

| Need | Use | Why |
|---|---|---|
| Runtime HUD, menus, panels | **UGUI** (`Canvas`, `RectTransform`, `Image`, `TextMeshProUGUI`) | This is what the whole project uses |
| Editor tool windows | **IMGUI** (`OnGUI`) or UIElements/UI Toolkit editor APIs | Existing tools (`MetroSceneDev`, `FactorySceneDev`) are IMGUI |
| World-space labels | TextMeshPro in world space, or `InteractionLabel` | Already implemented |

**Do not introduce UI Toolkit runtime UI** (`UIDocument`, UXML/USS) here. It
exists in 2022.3, but mixing two runtime UI stacks in one project doubles the
surface area for no gain.

## The project's HUD architecture

Everything routes through a few components, all CRT-terminal themed:

| Component | Assembly | Role |
|---|---|---|
| `CrtTheme` | `FYP.Core` | Palette, font, phosphor-green styling constants |
| `CrtOverlay` | `FYP.Core` | Scanline / flicker overlay |
| `OrderBoard` | `FYP.Content` | Top-right HUD; switches form by world (lab = 整备视图, layer world = 订单视图). Tab expands detail. |
| `BillPanel` | `FYP.Content` | Centred electricity bill, ~6.5 s |
| `DutyBriefing` | `FYP.Content` | Top-left typewriter duty rules, once per world per session |
| `SpecimenHud` | `FYP.Content` | Specimen counter (largely absorbed by `OrderBoard`) |
| `InventoryUI` | `FYP.Content` | Bottom item bar with baked icons |
| `HintToast` | `FYP.Interaction` | The single feedback outlet for gameplay messages |

`OrderBoard` and `SpecimenHud` use a **static `renderOwner` first-come-first-served**
guard so two scene instances (Lab + Metro) never draw twice. If you add another
HUD that can exist in two scenes, use the same pattern.

## TextMeshPro specifics for 2022.3

- TMP is a **separate package** here (`com.unity.textmeshpro 3.0.7`), not merged
  into UGUI as in later versions.
- Font assets are created via `Window > TextMeshPro > Font Asset Creator`.
- The CRT look uses a monospace font; `CrtTheme` holds the reference — it loads
  `Resources.Load<Font>("Sourced/Fonts/NotoSansMonoCJKsc-Regular")`
  (`CrtTheme.cs:33`).
- **CJK glyphs: this project has already solved the cost, and you must not undo
  it.** The HUD uses a **dynamic** font (`NotoSansMonoCJKsc`, a 16 MB OTF). Dynamic
  glyphs rasterise on first request, and when the atlas fills it **rebuilds at
  double size** — measured in `CrtCharsetGenerator.cs`: 489 chars → 1024×1024 at
  14–19 ms; 2000 chars → 1024×2048 at 66 ms; 4000 chars → 2048×2048 at **142 ms**.
  The symptom was a hitch exactly when the duty briefing appears on entering a
  layer world.
  - The fix: `Assets/Editor/CrtCharsetGenerator.cs` scans every `.cs` under
    `Assets/Scripts` for CJK characters and writes them to
    `Assets/Resources/CrtCharset.txt` (menu `FYP/UI/生成字体预热字符表`).
    `CrtFontPrewarm.Run()` (`Assets/Scripts/Core/CrtFontPrewarm.cs`) requests them
    all during the loading screen, so the atlas is final before gameplay.
  - **Therefore: any new player-facing Chinese string must be in a `.cs` file**,
    and `FYP/UI/生成字体预热字符表` must be re-run after adding new characters.
    Text typed only into a scene/prefab (object names) is deliberately *not*
    scanned — it never reaches the screen.
  - Adding a string to a `.cs` without re-running the generator reintroduces the
    first-glyph hitch on the frame it appears.

## Canvas patterns

- The HUD canvases are `Screen Space - Overlay`, so they render on top of
  everything and are **excluded from camera-rendered screenshots** (a known
  gotcha when capturing the game view from a specific camera rather than the
  `ScreenCapture` path).
- Set `CanvasScaler` to `Scale With Screen Size` with a reference resolution if
  you add a new canvas, matching the existing ones.

## IMGUI is fine for editor tools

`OnGUI` appears **19 times** across `Assets/Scripts/` and `Assets/Editor/`,
concentrated in editor tooling and on-screen diagnostics (`LoadDiag`, the `PT_*`
test drivers, the `MetroSceneDev`/`FactorySceneDev` staging tools). For new
**editor** windows/tools that is acceptable and matches the existing code. Do not
build new runtime player-facing UI in IMGUI — it has no layout system worth the
name and does not fit the CRT theme pipeline.

## Gotcha: UI raycasts vs. gameplay input

UGUI `GraphicRaycaster` + `EventSystem` consume pointer input before gameplay
sees it. If a HUD panel covers the screen and the player's mouse-look stops
working, check whether an invisible full-screen `Image` with
`raycastTarget = true` is intercepting. Set `raycastTarget = false` on decorative
graphics.
