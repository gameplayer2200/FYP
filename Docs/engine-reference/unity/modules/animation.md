# Unity 2022.3 — Animation Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project animation:** `Animator` + `AnimationClip`, plus **procedural** motion in code. No Timeline playback in gameplay.

---

## Two kinds of animation in this project

### 1. Procedural (dominant)

Most visible motion is computed in code, not authored in clips:

| Effect | Implementation |
|---|---|
| Door swing | `AnywhereDoor` coroutine: `spellWindup` → smoothstep rotation over `duration` → `spellOutro` |
| Portal screen content | `PortalWindow` renders a live off-axis camera into a material — not animation at all |
| Chalk arrows / procedural props | Generated at runtime |
| Factory machines | Transform rotation driven by coroutine |

This means "the animation looks wrong" is usually a **timing constant** problem
(`duration`, `spellWindup`, `openAngle`), not an Animator state problem. Check the
serialized fields first.

### 2. Authored clips (player, and the enemy)

Two different animation mechanisms are in use, and they do not mix:

- **Player — `Animator`.** `PlayerAnimatorProxy` (`FYP.Player`) drives a player
  Animator with the death/get-up chain: `Dead → GetUp → Idle`.
  > **Known constraint:** the `GetUp → Idle` transition must **not** use
  > `hasExitTime` — with it enabled the get-up stalls. This was fixed and is
  > recorded in `AGENTS.md` §8.
- **Enemy — legacy `Animation`, not `Animator`.** `EnemyGhoul`
  (`Assets/Scripts/World/EnemyGhoul.cs:14`) states it outright: *"动画是 legacy
  Animation（Ghoul.prefab 自带，不是 Animator）：Idle/Walk/Run 循环，Attack1 单次"*,
  and *"不使用 root motion——Ghoul 的动画全是 in-place，位移一律由 NavMeshAgent
  驱动"*. So: crossfade `Animation` clips by name, and **never expect root motion
  to move the ghoul** — the `NavMeshAgent` does all translation. Adding an
  `Animator` to `Ghoul.prefab` would break this and is not a migration anyone has
  asked for.
  `EnemyGhoul` also applies a **per-clip foot offset** (`footOffsetIdle/Walk/Run/
  Attack`, referenced at `footOffsetReferenceScale = 0.27` and rescaled to the
  actual body scale at runtime) because the in-place clips do not share a ground
  contact. If you swap in a new clip, expect to measure its own offset.

Animation assets live under `Assets/Art/Animations/UAL/` (Quaternius, CC0) with
`Clips` + `UAL1`/`UAL2` sets. **Preserve that internal structure** — do not
flatten it. The ghoul's own clips come from
`Assets/Prefab/Enemy/Ghoul@Animations.fbx`.

## Animator gotchas

- **`Animator` requires an `Avatar` matching the rig.** Mixing a UAL clip onto a
  non-humanoid rig silently produces no motion.
- **Parameters must exist on the controller**, or `SetTrigger`/`SetBool` logs a
  warning and does nothing. Verify the parameter name string matches exactly
  (they are stringly typed).
- **`Animator.updateMode`**: default `Normal` is fine here; `AnimatePhysics` would
  desync from the `CharacterController`.
- Domain reload does not reset Animator state; if a character is stuck in a state
  after a script recompile, stop and restart Play.

## Timeline

`com.unity.timeline 1.7.7` is installed but **no gameplay sequence uses it**.
Do not introduce Timeline for gameplay logic — this project's sequencing is
coroutine-based and the scene-loading model (`WorldStreamer`) would fight
Timeline's playable director lifetime.

## VFX Graph

`com.unity.visualeffectgraph 14.0.12` is installed. No VFX Graph assets are used
in gameplay scenes currently. Note that VFX Graph in **BiRP** has limited
support — it is primarily a URP/HDRP tool. Prefer ParticleSystem for new effects
here.
