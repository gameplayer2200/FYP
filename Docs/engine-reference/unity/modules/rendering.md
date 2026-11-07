# Unity 2022.3 — Rendering Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project pipeline:** Built-in Render Pipeline (BiRP). `m_CustomRenderPipeline: {fileID: 0}`.

---

## What this means concretely

There is no Scriptable Render Pipeline. Consequently there is **no**:

- `UniversalRenderPipelineAsset` / `UniversalRendererData`
- Renderer Features (the URP `ScriptableRendererFeature` extension point)
- URP Volume framework / `VolumeProfile` overrides (Bloom, Vignette, Tonemapping…)
- Shader Graph as a supported authoring path for this project
- `Light2D`, URP 2D renderer

Anything you may have seen in URP tutorials does not transfer. Check before
proposing it.

## What this project actually uses

| Concern | Mechanism |
|---|---|
| Custom projection shader | `Assets/Shaders/PortalPanelProj.shader` (`Custom/PortalPanelProj`) — world-space projection sampling, deliberately **not** mesh UV. This is the portal "screen". |
| EMF scan shaders | `Assets/Shaders/ScanMatrix.shader` + `Assets/Shaders/ScanWaveOverlay.shader`, driven by `EmfDetector` / `ScanWaveOverlay` |
| Third-party Surface Shaders | 8 files, all in the FloodedGrounds pack: 3 in `Assets/Art/Worlds/FloodedGrounds/Content/Shaders/` (`FG_PBR_TopBlend`, `FG_PBR_Water`, `FG_Triplanar_BumpSpec`) + 5 Unity vegetation shaders in `.../Resources/` (`TreeCreator*` ×4, `WavingGrass`) |
| Screen-space VFX | `Assets/Art/VFX/MatrixScan/` — MIT, `ScreenSpaceMatrix.cs` + 2 shaders + a compute shader |
| UI | UGUI Canvas + TextMeshPro, with a CRT overlay (`CrtOverlay`) for scanlines |
| Lighting | Realtime only; no baked lightmaps in the gameplay scenes |

**Only three shaders in this project are first-party** (`Assets/Shaders/`). The
Surface Shaders and everything under `Assets/Art/` are imported third-party
assets — treat them as black boxes, and do not convert them to Shader Graph
(which this project cannot use, see above).

## Built-in RP light budget — the known trap

Built-in RP has a **limited per-pixel light count** per object (Quality Settings →
Pixel Light Count, default 4 in many tiers). Beyond that, lights fall back to
vertex lighting and objects visibly stop receiving them.

This project hit it hard: **Metro has 66 realtime lights**, which pushed the
flashlight out of the per-pixel budget entirely and made it appear not to work.
The fix (2026-10-17) was:

- flashlight cone `Light.renderMode = LightRenderMode.ForcePixel`
- intensity raised 2.4 → 3.6
- a near-field compensation point light, range 4.5 / intensity 0.7, also `ForcePixel`

**Before adding realtime lights to a scene**, check whether the affected objects
already have their per-pixel budget consumed. `ForcePixel` is the escape hatch,
but it costs fill rate.

## Fog, skybox, ambient

Configured per scene via `RenderSettings`. Metro uses linear fog 15–170 in a
teal-green tint. The `WorldLightIsolation` system (`Assets/Scripts/World/`) keeps
each world's lights confined to that world's layer, which matters because all
worlds live in one coordinate space and are streamed in/out.

## Shader compile errors look like

`Shader error in '...': ...` in the Console, and materials render magenta. If you
see magenta after editing a shader, check the Console — it is a compile error, not
a rendering setting.

## Post-processing

No URP Volume anywhere. Two things exist, and they are easy to confuse:

1. **`OnRenderImage` image effects — this is what the game actually uses.**
   `ScanWaveOverlay` (`Assets/Scripts/Player/ScanWaveOverlay.cs:130`) is attached
   to the player camera at runtime by `EmfDetector` and draws the full-screen
   digital scan wave. `EmfDetector` notes the constraint explicitly: *"OnRenderImage
   only fires on components attached to the camera."* Any new full-screen effect
   must follow that pattern — a component on a normal GameObject silently never
   runs.
2. **A vendored Post Processing Stack v1** at
   `Assets/Art/Worlds/FloodedGrounds/PostProcessing/` (the third-party package
   that ships with that world asset). **No scene uses it** — no
   `PostProcessingBehaviour` component exists in any `.unity` file, and the
   `PPS_Ghoul.asset` profile in `Assets/Prefab/Enemy/` is referenced by nothing.
   Do not assume it is wired up; do not add it without deciding to adopt the whole
   stack.

There are also URP/HDRP Volume Profile assets under `Assets/Art/Laboratory/Settings/`
and a URP dissolve shader package under `Assets/Art/VOiD1 Gaming - Free Dissolve
Shader for Unity URP/`. **Both are inert third-party content from a URP/HDRP
context** — the profiles bind to render pipelines this project does not have, and
the dissolve shaders will render magenta under BiRP. Do not build on them.
