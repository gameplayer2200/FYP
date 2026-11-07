# Unity 2022.3 — Navigation Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project navigation:** Runtime-baked `NavMesh` for one enemy type (`EnemyGhoul`). No baked NavMesh assets, no `NavMeshSurface` package.

---

## Current state — verified

- `com.unity.modules.ai` **1.0.0** is in `Packages/manifest.json` — the legacy
  built-in navigation module (not the `com.unity.ai.navigation` package).
- **No NavMesh asset is committed.** All seven scenes' `NavMeshSettings` blocks
  point at a missing asset — **the project has never been baked in the editor.**
  The `EnemyGhoul` header comment states this explicitly.
- **NavMesh is baked at runtime instead**, by `Assets/Scripts/World/EnemyGhoul.cs`:
  - `BuildNavMeshOnce()` (`EnemyGhoul.cs:829`) calls
    `NavMeshBuilder.CollectSources(...)` (`:867`) then
    `NavMeshBuilder.BuildNavMeshData(...)` (`:869`) and registers the result with
    `NavMesh.AddNavMeshData(data)` (`:874`), stored in
    `s_bakedData[sceneKey]`.
  - **One NavMesh per scene**, keyed by `gameObject.scene.name`
    (`SceneKey()`, `:161`). All `EnemyGhoul` instances in the same scene share
    that single bake (`s_bakedScenes`, `:146`).
  - The bake area is the union of the scene's enemy patrol points, padded by
    `bakePadding = 8`, with a floor of `minBakeFootprint = 40` and a default roam
    footprint of `roamAreaSize = 150`.
  - **Re-bake on demand**: `ReBakeToInclude(Vector3 target)` (`:181`) rebuilds a
    box covering both the agent and its target when the target leaves the baked
    area — clamped to `maxRebakeSize = 400`, margin `rebakeMargin = 60`,
    throttled by `rebakeCooldown = 2`. It removes the old `NavMeshDataInstance`
    first (`old.Remove()`), because two overlapping navmeshes would fight.
  - A freshly added `NavMeshData` is **not queryable until the next navigation
    update**, so the agent re-snaps on the following frame
    (`snapPending` declared `:223`, set `:208`/`:264`, cleared `:348`).
- **Where enemies actually exist:** `Assets/Prefab/Enemy/GhoulEnemy.prefab` is
  referenced by **`FactoryDay.unity`** and **`Flooded_Grounds.unity`** only.
  `Assets/Prefab/Enemy/GhoulScene.unity` is the enemy's own test scene.
  Metro, SpecimenLab, FactoryNight, OfficePack and Laboratory have none.
- `EnemyGhoul` is `[RequireComponent(typeof(NavMeshAgent))]` and drives movement
  **entirely through the agent** — the animations are in-place legacy `Animation`
  clips, no root motion (`:14`).
- **The player is a `CharacterController`, never a `NavMeshAgent`.**

## Baking parameters that matter (from the serialized fields)

| Field | Default | Why it matters |
|---|---|---|
| `bakeAgentRadius` | 0.35 | **Must be ≤ 0.35 for a 0.9 m doorway**; 0.45 works for a 1.35 m door. Too large and the player hides in a room the enemy cannot enter. |
| `bakeAgentHeight` | 2.0 | Must be **below the scene's door height**, or doors become impassable. |
| `bakeAgentClimb` | 0.6 | Step height. A threshold taller than this splits the navmesh into two disconnected islands — the classic "it gets stuck in the doorway" symptom. Wooden-house thresholds are commonly 0.3–0.6 m. |
| `bakeHeight` | 14 | Vertical extent of the bake volume. |
| `roamAreaSize` | 150 | Roam footprint when `roamWholeScene` is on (the default). |

## Pitfalls specific to this project

1. **Worlds are streamed, not loaded as one scene.** `WorldStreamer` loads and
   unloads worlds at runtime (`MaxLoadedWorlds = 5`, `preloadRadius = 15`). The
   per-scene bake keying is what makes this survivable: each world bakes its own
   NavMesh when it loads. **Cross-world pathfinding is not possible and must not
   be attempted** — the enemy in FactoryDay cannot chase the player into
   FactoryNight.
2. **Worlds occupy far-apart coordinate regions** (5000 m slot grid; `AGENTS.md`
   §4). A single global NavMesh over all worlds would be enormous and almost
   entirely empty. Never bake globally.
3. **Metro is raised +93.9 m** and Flooded Grounds +2134 m. Any hardcoded
   navigation bounds must account for the world's own Y offset — which is why the
   bake volume is computed from live `transform.position`, not from constants.
4. **Runtime baking is not free.** `NavMeshBuilder.BuildNavMeshData` is a
   synchronous CPU bake; a 150 m × 150 m footprint costs real frame time on the
   first frame the enemy appears in a newly streamed world. Do not add many
   enemies per scene without measuring.
5. **`com.unity.ai.navigation` (NavMeshSurface / NavMeshModifier) is not
   installed.** Do not write code against it. The only available API is the legacy
   `UnityEngine.AI` surface used above.
6. **A rebake is a visible hitch and a state reset.** `ReBakeToInclude` removes
   and rebuilds the navmesh; the agent must re-snap, and any in-flight path is
   invalid. Do not shorten `rebakeCooldown` to "make chasing better" — it turns
   into a rebake loop.

## Recommendation

Do not add editor-time NavMesh baking to this project. Runtime per-scene baking
is the design that survives `WorldStreamer`; an editor-baked asset would be
unloaded with its scene and would have to be re-added on load anyway. If more
enemy types arrive, factor the bake/re-bake logic out of `EnemyGhoul` into a
shared per-scene baker rather than duplicating it.
