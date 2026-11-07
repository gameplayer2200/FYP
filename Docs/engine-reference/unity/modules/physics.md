# Unity 2022.3 — Physics Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project physics:** 3D PhysX (`com.unity.modules.physics`). 2D physics unused.

---

## What this project actually uses

| Concern | Implementation |
|---|---|
| Player locomotion | `CharacterController` in `PlayerMovement` — **not** a Rigidbody |
| Portal crossing detection | **Mathematical plane test** in `PortalWindow.LateUpdate` (signed distance along the door normal), **not** a trigger collider |
| Interaction picking | `InteractionRunner` distance/angle test against registered `IInteractable`s — not raycast-based |
| Environment collision | MeshColliders and BoxColliders authored per scene/prefab |

## The portal is not a collider — do not "fix" it with one

`PortalWindow.TryCross()` computes the player's signed distance along the door
plane and fires when it crosses `crossPlaneLead` (0.25 m toward the approach side).
It also checks direction (`inward` only), tolerance (`crossPlaneTolerance` 1.5 m)
and cooldown (0.6 s).

If a player passes through without teleporting, the cause is one of:

1. the door is closed (`AnywhereDoor` state gates crossing),
2. the cooldown has not elapsed,
3. the approach was not inward,
4. the paired door is missing/unpaired (`PortalWindow.autoPair` derives the pair
   from the door name — `anywhere-door-X-A` ↔ `anywhere-door-X`).

Adding a trigger collider does not help and will create double-fire bugs.

## CharacterController consequences

Because the player is a `CharacterController` and not a Rigidbody:

- **No `OnCollisionEnter` on the player.** Use `CharacterController.collisionFlags`
  or `OnControllerColliderHit` instead.
- Gravity, slopes and step offset are handled by `PlayerMovement`, not by PhysX.
- Pushing rigidbodies requires explicit `OnControllerColliderHit` handling.
- Teleporting the player must move the transform and then
  `Physics.SyncTransforms()` (or wait a frame) if immediate collision queries
  matter — `CharacterController` caches its own state.

## Layer collision matrix

Worlds are isolated by layer (`WorldLightIsolation` handles lights; the collision
matrix in `ProjectSettings/DynamicsManager.asset` handles bodies). If two
streamed worlds' colliders start interacting, check the layer assignments before
changing physics settings.

## Query gotchas

- `Physics.Raycast` against a `MeshCollider` with `convex = false` does not work
  for moving/kinematic cases and is slower. Mark non-convex MeshColliders as
  static.
- Raycasts starting inside a collider return `hit.distance == 0` — start the ray
  slightly outside, or use `Physics.queriesHitBackfaces` deliberately.
- `Physics.SyncTransforms()` is required after moving colliders by transform if
  you query in the same frame.

## Physics settings worth knowing

`ProjectSettings/DynamicsManager.asset` holds gravity, default solver iterations
and the layer matrix. The project does not run custom physics steps or use
`Physics.simulationMode` overrides.
