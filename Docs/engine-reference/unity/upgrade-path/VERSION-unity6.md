# Unity 6.3 LTS — retained upgrade reference (NOT this project's engine)

> This file lives in `upgrade-path/` and is **not** the project's version
> reference. The live one is `../VERSION.md` (Unity 2022.3.62f3c1). The table
> below was the project header at the time this material was written; the body
> is Unity 6.3. See `README.md` in this directory.

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 2022.3.62f3c1 (LTS) |
| **Installed at pin time** | 2022.3.62f3c1 — probed 2026-11-01 at `C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe` (`ProductVersion` = `2022.3.62f3c1_1623fc0bbb97`). Match. A second editor, `6000.6.4f1`, is also installed on this machine but is NOT this project's engine. |
| **Release Date** | NOT SOURCEABLE — 2022.3.62f3c1's release date was not re-fetched this run |
| **Project Pinned** | 2026-11-01 |
| **Last Docs Verified** | 2026-11-01 (header/version only — the Unity 6.x body below was NOT re-verified) |
| **LLM Knowledge Cutoff** | May 2025 |
| **Risk Level** | LOW — 2022.3 LTS is squarely within LLM training data (Unity coverage up to ~2022 LTS) |

## ⚠️ Read this before using the body of this file

**This project runs Unity 2022.3.62f3c1. Everything below the "Unity 6.x" heading
was curated for a Unity 6.3 LTS pin and describes a NEWER engine than the one
installed here.**

Re-pinned 2026-11-01 by `/setup-engine` at the operator's explicit instruction
(the reference previously pinned Unity 6.3 LTS while `ProjectSettings/ProjectVersion.txt`
said `2022.3.62f3`, and both `AGENTS.md` and every shipped scene target 2022.3).

How to use it now:

- **For this project, 2022.3 LTS is the authority.** APIs, packages and defaults
  come from the 2022.3 manual, not from the Unity 6.x notes below.
- **The Unity 6.x material is retained, not endorsed.** It is kept because it is
  the only sourced record in this repo of what changed after the training cutoff,
  and deleting it would destroy information. Treat it as *forward-looking context
  for a possible future upgrade*, never as a description of this codebase.
- **Never emit a Unity 6 API here.** If a snippet below uses Entities 1.x, the new
  Input System as default, GPU Resident Drawer, WebGPU, or "UI Toolkit replaces
  UGUI for new projects", it does not apply: this project is BiRP, legacy Input,
  UGUI/TextMeshPro, and C# 9 per the 2022.3 compiler.
- The Unity 6.x body's own `<!-- timeline-unverified -->` warning (release dates
  inconsistent with the stated cutoff) still stands and is unchanged.

Project-specific facts that override anything below:

| Fact | Value | Source |
|---|---|---|
| Render pipeline | **Built-in (BiRP)** — not URP/HDRP | `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline: {fileID: 0}`; no `com.unity.render-pipelines.*` in `Packages/manifest.json` |
| Input | Legacy Input Manager | No `com.unity.inputsystem` in the manifest |
| UI | UGUI + TextMeshPro (`com.unity.ugui 1.0.0`, `com.unity.textmeshpro 3.0.7`) | `Packages/manifest.json` |
| Physics | PhysX | `com.unity.modules.physics` |
| Test framework | `com.unity.test-framework 1.1.33`, installed **transitively** via `com.unity.feature.development 1.0.1`; no `Assets/Tests` folder yet | `Packages/packages-lock.json` |

---

## Unity 6.x — retained reference (NOT this project's version)

> The section below was authored for a **Unity 6.3 LTS** pin. See the warning
> above before applying any of it.

## Knowledge Gap Warning

The LLM's training data likely covers Unity up to ~2022 LTS (2022.3). The entire
Unity 6 release series (formerly Unity 2023 Tech Stream) introduced significant
changes that the model does NOT know about. Always cross-reference this directory
before suggesting Unity API calls.

## Installed-Version Gap Warning

The warning above is one-directional — it covers the **model** knowing less than
this pin. The reverse gap is real and `/setup-engine` §3 creates it deliberately
("pin the newer one and upgrade later"): this reference can sit **ahead of the
installed editor**, and an agent citing it correctly then emits APIs that do not
compile locally. **Check `Installed at pin time` above before trusting a
version-qualified claim** — `NOT DETERMINED` means the gap is unknown, not absent.

## Post-Cutoff Version Timeline

<!-- timeline-unverified -->

> **⚠ THIS TABLE IS INTERNALLY INCONSISTENT AND MUST NOT BE TRUSTED FOR RISK
> JUDGEMENTS UNTIL RE-VERIFIED (flagged 2026-08-10).** It is titled
> *Post-Cutoff* and the stated cutoff above is **May 2025**, yet three of its four
> rows are dated **Oct, Nov and Dec 2024** — before that cutoff — followed by a
> twelve-month gap to 6.3 in Dec 2025. Both cannot be right. The sibling files
> `godot/VERSION.md` and `unreal/VERSION.md` list only genuinely post-cutoff
> versions, so this is a defect in this file, not in the format.
>
> **Why it matters more than a wrong date.** This table's entire job is to tell an
> agent what it does *not* already know. Rows that predate the cutoff invert that
> signal: take an installed `6000.1.0f1` (**Unity 6.1**) — this
> table dates it to Nov 2024 and labels it post-cutoff MEDIUM risk, while by its own
> cutoff a Nov 2024 release would be *inside* training data. An agent reading this
> either over-trusts the reference or mis-scopes what is new, and the `6.2+`
> boundaries cited elsewhere in this directory are anchored to these rows.
>
> **The dates are NOT corrected here, deliberately.** Real Unity release dates
> cannot be sourced from this repo — this directory *is* the source — and writing
> them from training data is the failure this whole directory exists to prevent
> (`/security-audit` makes the same call). Re-verify against Unity's
> published release notes and update the row dates, the `Release Date` field, and
> `Last Docs Verified` together. Until then, treat every version-qualified claim in
> `docs/engine-reference/unity/` as **unconfirmed**, and prefer APIs that are not
> version-boundary-dependent.

| Version | Release | Risk Level | Key Theme |
|---------|---------|------------|-----------|
| 6.0 | Oct 2024 | HIGH | Unity 6 rebrand, new rendering features, Entities 1.3, DOTS improvements |
| 6.1 | Nov 2024 | MEDIUM | Bug fixes, stability improvements |
| 6.2 | Dec 2024 | MEDIUM | Performance optimizations, new input system improvements |
| 6.3 LTS | Dec 2025 | HIGH | First LTS since 6.0, production-ready DOTS, enhanced graphics features |

## Major Changes from 2022 LTS to Unity 6.3 LTS

### Breaking Changes
- **Entities/DOTS**: Major API overhaul in Entities 1.0+, complete redesign of ECS patterns
- **Input System**: Legacy Input Manager deprecated, new Input System is default
- **Rendering**: URP/HDRP significant upgrades, SRP Batcher improvements
- **Addressables**: Asset management workflow changes
- **Scripting**: C# 9 support, new API patterns

### New Features (Post-Cutoff)
- **DOTS**: Production-ready Entity Component System (Entities 1.3+)
- **Graphics**: Enhanced URP/HDRP pipelines, GPU Resident Drawer
- **Multiplayer**: Netcode for GameObjects improvements
- **UI Toolkit**: Production-ready for runtime UI (replaces UGUI for new projects)
- **Async Asset Loading**: Improved Addressables performance
- **Web**: WebGPU support

### Deprecated Systems
- **Legacy Input Manager**: Use new Input System package
- **Legacy Particle System**: Use Visual Effect Graph
- **UGUI**: Still supported, but UI Toolkit recommended for new projects
- **Old ECS (GameObjectEntity)**: Replaced by modern DOTS/Entities

## Verified Sources

- Official docs: https://docs.unity3d.com/6000.0/Documentation/Manual/index.html
- Unity 6 release: https://unity.com/releases/unity-6
- Unity 6.3 LTS announcement: https://unity.com/blog/unity-6-3-lts-is-now-available
- Migration guide: https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- Unity 6 support: https://unity.com/releases/unity-6/support
- C# API reference: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/index.html
