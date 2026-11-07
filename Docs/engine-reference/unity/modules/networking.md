# Unity 2022.3 — Networking Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project networking:** None. Single-player only.

---

## Current state — verified

- **No networking package is installed.** `Packages/manifest.json` contains no
  `com.unity.netcode.gameobjects`, no `com.unity.multiplayer.tools`, no Mirror, no
  Photon.
- **No networking code exists.** A search for `NetworkBehaviour`, `Mirror`,
  `Netcode` across `Assets/Scripts/` returns only false positives from
  `PortalWindow`'s `mirrorU` field (which is about mirroring the portal image, not
  networking).
- The game is a single-player first-person horror title. `FYP_Game_Proposal.md`
  describes no multiplayer mode.

## If networking is ever added

This project is unusually hostile to naive netcode adoption. The blockers are
architectural, not incidental:

1. **`WorldStreamer` unloads and reloads whole scenes** based on local player
   proximity (`preloadRadius = 15`, `MaxLoadedWorlds = 5`). Clients in different
   worlds would have different scene graphs loaded. Any networked design must
   decide explicitly what is replicated and what is client-local.
2. **Portal teleportation is a transform swap** across two loaded scenes. Networked
   teleports need authority handling and a reconciliation path that does not exist.
3. **Heavy use of static singletons** (`PlayerRef`, `BatterySystem.Instance`,
   `Inventory.Instance`, `EmfStorm`, `BillingLedger`, `WorldPresence`,
   `ExtractionClock`, `FactoryShift`). Static state is per-process and does not
   replicate.
4. **The player is a `CharacterController`.** Netcode character movement typically
   wants either client-authoritative transforms or a predicted controller; both are
   a rewrite of `PlayerMovement`.
5. **Domain reload clears every static registry.** The project already needs a
   `repair_singletons` workaround in tests; adding networked session state to that
   mix is fragile.

## Recommendation

If multiplayer is ever genuinely required, it should be treated as a **new
project**, not a retrofit. For the current scope, keep the single-player
assumption explicit — it simplifies the streaming and portal systems considerably.
