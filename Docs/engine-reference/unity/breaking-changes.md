# Unity 2022.3 LTS — Breaking Changes

**Project version:** Unity 2022.3.62f3c1 · **Last verified:** 2026-11-01

> **Scope.** This file covers changes relevant to **arriving at 2022.3**, i.e.
> upgrading from 2021 LTS or earlier. This project is already on 2022.3 and its
> 191 scripts compile, so these are background, not action items.
>
> Changes *after* 2022.3 (Unity 6.x) live in `upgrade-path/breaking-changes.md`
> and **do not apply** to this project.

## Sourcing caveat

Rewritten 2026-11-01 at re-pin time. The entries below are limited to
long-standing, uncontroversial facts plus what this repository evidences. Items
requiring a fresh fetch of Unity's 2022.3 upgrade guide are marked
**NOT SOURCEABLE** rather than recalled.

Official upgrade guide:
`https://docs.unity3d.com/2022.3/Documentation/Manual/UpgradeGuide2022LTS.html`

---

## HIGH RISK — would break existing code

### Built-in Render Pipeline is the only pipeline here
There is no URP/HDRP upgrade in this project. `ProjectSettings/GraphicsSettings.asset`
has `m_CustomRenderPipeline: {fileID: 0}`, and `Packages/manifest.json` contains no
`com.unity.render-pipelines.*`.

**Consequence:** any URP-specific guidance (Shader Graph as the default authoring
path, `UniversalRenderPipelineAsset`, Renderer Features, URP Volume overrides)
does not apply. Custom shaders here are Surface Shaders and hand-written HLSL —
see the 8 `#pragma surface` shaders under `Assets/Art/Worlds/FloodedGrounds/`.

### Legacy Input Manager is the input layer
`com.unity.inputsystem` is absent. 27 call sites use `Input.*`.

**Consequence:** do not introduce the new Input System, `InputAction`, or
`Keyboard.current` without an explicit decision to add the package and migrate.

### C# language level is C# 9
Unity 2022.3's Roslyn supports C# 9. Features from C# 10/11/12 (file-scoped
namespaces, `required` members, raw string literals, primary constructors on
classes, collection expressions) will **not compile**.

## MEDIUM RISK

### `com.unity.cinemachine 3.1.7`
Cinemachine 3.x changed the API substantially from 2.x (`CinemachineVirtualCamera`
→ `CinemachineCamera`, component-based instead of monolithic). The project is
already on 3.1.7, so code should target the 3.x API. Do not paste 2.x snippets.

### `com.unity.textmeshpro 3.0.7`
TMP is a separate package here, not folded into UGUI as it later became. Use
`TMPro` namespace APIs as shipped with 3.0.7.

## LOW RISK

### `com.unity.cloud.gltfast 6.14.1`
Used for glTF/GLB import. API surface is package-local; consult the package docs
shipped under `Library/PackageCache/` rather than engine docs.

## NOT SOURCEABLE

Not re-verified this run:

- The precise list of APIs removed between 2021 LTS and 2022.3.
- Behavioural changes in 2022.3 physics, animation, or serialization.
- Patch-level (2022.3.x) regressions or fixes.

Consult Unity's official 2022 LTS upgrade guide for these.
