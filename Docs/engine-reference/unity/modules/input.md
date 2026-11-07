# Unity 2022.3 — Input Module Reference

**Project version:** 2022.3.62f3c1 · **Last verified:** 2026-11-01
**Project input stack:** Legacy Input Manager. `com.unity.inputsystem` is NOT installed.

---

## The rule for this project

Use the legacy `UnityEngine.Input` class. It is supported in 2022.3 and is what
all 27 call sites in `Assets/Scripts/` use.

```csharp
// CORRECT for this project
if (Input.GetKeyDown(KeyCode.E)) { /* interact */ }
float h = Input.GetAxisRaw("Horizontal");
if (Input.GetMouseButton(0)) { /* ... */ }
```

```csharp
// WRONG for this project — com.unity.inputsystem is not installed,
// these do not compile
// if (Keyboard.current[Key.E].wasPressedThisFrame) { }
// InputAction action = ...;
```

## Input axes this project relies on

Unity's default axes (`Horizontal`, `Vertical`, `Mouse X`, `Mouse Y`) come from
`ProjectSettings/InputManager.asset`. `PlayerMovement` reads movement input and
`Mouse X/Y` for look. If you change input handling, confirm the axis names still
exist in that file — a missing axis throws at runtime.

## Key bindings currently in use

| Key | Action | Where |
|---|---|---|
| `W A S D` | Move | `PlayerMovement` |
| Mouse | Look | `PlayerMovement` |
| `E` | Interact (single owner) | `InteractionRunner` |
| `F` | Flashlight toggle | `Flashlight` |
| `Tab` | Order board detail expand | `OrderBoard` |
| Space | Jump (charged super-jump costs 1 battery) | `PlayerMovement` |

## Architectural constraint: one E-key owner

`InteractionRunner` (`Assets/Scripts/Interaction/`) is the **only** component that
*dispatches* interaction from the E key. It picks the nearest `IInteractable`
within range and invokes it. New interactables implement `IInteractable` and
register via `InteractionRegistry` — they do not poll input themselves.

**One deliberate exception, with its own guard:**
`ChargeStation.Update()` (`Assets/Scripts/World/ChargeStation.cs:44`) also reads
`Input.GetKeyDown(KeyCode.E)`, purely as a **global cancel** while charging. The
reason is recorded in the code: while charging the player stands still, so the
nearest interactable may well be a *door*, not the charging post — routing the
cancel through `InteractionRunner` would open the door instead. It is safe because
it only runs while `BatterySystem.IsCharging` is true, it also checks its own
distance, and it sets `cancelLockUntil = Time.time + 0.25f` to swallow the same
key press so `Interact()` cannot immediately re-enable charging.

That is the bar for a second E reader: **a documented reason, a state guard, and
a same-press lock.** An unguarded second `KeyCode.E` poll is still a defect.

## Gotcha: input in `FixedUpdate`

`GetKeyDown` is true for exactly one frame. Polling it in `FixedUpdate` (which may
run 0 or several times per frame) drops or repeats presses. Poll in `Update`;
buffer the intent if a fixed-step consumer needs it.

## Gotcha: input while the editor is unfocused

When the Unity Editor loses focus during Play, the player loop stops advancing
regardless of `runInBackground`. Automated tests that drive gameplay through
input must instead call the relevant `Update`/`LateUpdate` reflectively. This is
documented in `AGENTS.md` §6 and has bitten the portal-crossing tests specifically.
