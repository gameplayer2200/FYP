# `upgrade-path/` — retained Unity 6.3 material (NOT this project)

**This directory does not describe the project. Do not read it as reference.**

The live engine reference is the parent directory:
[`../VERSION.md`](../VERSION.md) — **Unity 2022.3.62f3c1 LTS**.

## What this is

Before 2026-11-01 the reference set in `docs/engine-reference/unity/` was pinned to
**Unity 6.3 LTS**. The project's `ProjectSettings/ProjectVersion.txt`, `AGENTS.md`
and every shipped scene were all on **2022.3.62f3c1**, so the whole set was
mis-pinning the project.

At re-pin time the Unity 6.3 files were **moved here rather than deleted**. They
are the only sourced record in this repository of what changed after the LLM
training cutoff, so they retain real research value — but only for a future
upgrade that nobody has asked for.

## The rule

| Question | Answer |
|---|---|
| Is the project on Unity 6? | **No.** 2022.3.62f3c1, no upgrade target. |
| May I use anything in this directory? | **Not for this project.** Not APIs, not packages, not "best practices". |
| When would this apply? | Only after an explicit, written decision to upgrade the engine — which would also require re-running the ADRs and re-verifying every scene. |
| What if I only want one idea from here? | Treat it as a hypothesis, then verify it against the **2022.3** manual and this repository before proposing it. |

## Contents

| File | What it holds |
|---|---|
| `VERSION-unity6.md` | The Unity 6.3 version pin and its stack table, plus a chronology of the re-pin |
| `breaking-changes.md` | Changes landing *after* 2022.3 (the 6.x line) |
| `deprecated-apis.md` | APIs deprecated in the 6.x line |
| `current-best-practices.md` | Unity 6 idioms — URP-first, UI Toolkit, Input System, DOTS |
| `PLUGINS.md` | The Unity 6.3 package picture |
| `modules/*.md` | Per-subsystem Unity 6 notes |
| `plugins/*.md` | `addressables.md`, `dots-entities.md`, and a Unity 6 `cinemachine.md` |

> **Note the trap in `deprecated-apis.md`.** The Unity 6 version tells you to
> replace `Input.GetKey()` with `Keyboard.current[...]`. That is correct for Unity
> 6 and **wrong here** — `com.unity.inputsystem` is not installed, and all 27
> input call sites in this project use the legacy `Input` class, which is fully
> supported in 2022.3. The live file at `../deprecated-apis.md` says so explicitly.
