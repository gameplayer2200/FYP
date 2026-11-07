# Unity 2022.3 LTS — Optional Packages & Systems

**Project version:** Unity 2022.3.62f3c1 · **Last verified:** 2026-11-01

Index of the optional packages this project **actually has installed**, what each
is really used for here, and what to do before reaching for it.

> The Unity 6.3 version of this file is preserved at
> `upgrade-path/PLUGINS.md` and does **not** describe this project.

---

## Installed packages — verified from `Packages/manifest.json` + `packages-lock.json`

| Package | Version | Source | Used in code? | Real role here |
|---|---|---|---|---|
| `com.unity.ugui` | 1.0.0 | registry | **Yes** — the HUD | Runtime UI. See `modules/ui.md` |
| `com.unity.textmeshpro` | 3.0.7 | registry | **Yes** | Text rendering. Separate package in 2022.3 |
| `com.unity.timeline` | 1.7.7 | registry | No | Installed, unused. See `plugins/timeline.md` |
| `com.unity.cinemachine` | 3.1.7 | registry | **No components** | Installed; only the `CinemachineIgnored` **tag** is referenced (182 times in `OfficePack.unity`) |
| `com.unity.probuilder` | 5.2.4 | registry | No | Installed, no `ProBuilderMesh` anywhere. See `plugins/probuilder.md` |
| `com.unity.cloud.gltfast` | 6.14.1 | registry | **Yes** — `GlbTextureCompressor` | glTF/GLB import pipeline. See `plugins/gltfast.md` |
| `com.unity.visualeffectgraph` | 14.0.12 | builtin | No | No `.vfx` assets. Limited BiRP support |
| `com.unity.visualscripting` | 1.9.4 | registry | No | No script graphs. All logic is C# |
| `com.unity.test-framework` | 1.1.33 | **transitive** | No | Via `com.unity.feature.development`. No tests yet |
| `com.unity.collab-proxy` | 2.12.4 | registry | No | Version Control window plumbing |
| `com.coplaydev.unity-mcp` | `file:` | **local** | Editor tooling | MCP bridge. See the note below |

**Not installed:** Addressables, DOTS/Entities/Burst-as-direct-dep, Input System,
URP/HDRP, Netcode, Mirror, AI Navigation (`com.unity.ai.navigation`).

---

## The local MCP package — do not "fix" it

`com.coplaydev.unity-mcp` is referenced as `file:com.coplaydev.unity-mcp`, a
**local embedded package** at `Packages/com.coplaydev.unity-mcp`.

> **Do not change this to a git URL.** UPM on this machine fails to resolve the git
> form with `not in a git directory`. This was hit and reverted once already; the
> `file:` reference is the working state. `AGENTS.md` §1 records the same rule.

---

## Before adding a new package

1. Check it exists for **2022.3** specifically. A package page that says "Unity 6"
   or lists only `6000.x` compatibility will not resolve here.
2. Prefer what is already installed. Adding URP, DOTS, or Input System is not an
   incremental change in this project — each invalidates a large slice of existing
   code (`AGENTS.md` §3–§4, `modules/rendering.md`, `modules/input.md`).
3. Record the version in `Packages/manifest.json` **and** confirm
   `packages-lock.json` picked it up; a floating version that silently resolves to
   a newer major is how projects break months later.
4. If the package ships runtime code used by gameplay, note it here — the assembly
   definition dependencies (`Docs/adr/0004`) must be updated too.

## Package-level gotchas specific to this project

- **`com.unity.feature.development`** is a feature set, not a library. It drags in
  test-framework, code coverage and IDE integration. Do not remove it casually —
  `commands.test` in `project.yaml` depends on it.
- **BiRP limits what VFX Graph and Shader Graph can do here.** Neither is wired
  into the gameplay scenes; ParticleSystem and hand-written shaders are the
  supported paths (`modules/rendering.md`).
- **Cinemachine 3.x** is a major API break from 2.x. If Cinemachine is ever
  actually adopted, the 3.x docs are the relevant ones — but nothing in this
  project currently uses a Cinemachine camera, only the tag.
