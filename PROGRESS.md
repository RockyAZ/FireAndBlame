# Flame and Blame: progress

## Phase 0: DONE (2026-11-17)

Gate: compile clean (0 errors, 0 warnings), bootstrap ran, tags, layers and config assets present. One fix round was needed.

### Done
- **Design**: `GAME_DESIGN.md` (component behaviour, human state transition table, fire rules, test mode, module map, config fields) and `TASKS.md` (Phases 0 to 5, files and checks per task).
- **Contract**: no renames. A "Contract additions" section was appended to `CLAUDE.md`. It adds `Cup [Inject] IControlSource` + `InPool/SetInPool`, `WaterDrop.ReportHit`, Human `IsStunned/StrengthChanged/TickStrength`, FireSide `Advance/PauseTimeLeft/ApplyBuilding`, FireSystem `[Inject] BuildingConfig` + `GroundFireZone[]`, zone `ReportEnter/Stay/Exit` forwarders, extra trigger layer pairs, and a Won rule that also needs at least one human in the pool.
- **Assemblies**: Core, VFX, Heli, Fire, Rescue, GameRules, UI, Testing, Main, EditorTools (Editor only) and Tests.PlayMode asmdefs, all referenced by name. No cycles. Gameplay modules do not reference Testing or Main. Injection is untouched.
- **Core**: enums, `HeliCommand`, `IControlSource`, `ITestModeAware`, `IKillable`.
- **Configs**: all six ScriptableObject classes with defaults. The assets are created in `Assets/Settings/`.
- **Stubs** for every contract class. Already working: `PlayerControlSource` (Input System), `ScriptedControlSource`, `Human.SetState` guard and event, `HeliController.Crash`, `HumansSystem.CountIn/KillAll`, `FireSystem.SidesNotOnFire/SetAllLost`.
- **MainEntry**: builds the Context, installs configs and systems (`IControlSource` by type, testMode optional) and initializes in order fire, humans, cup, heli, game, hud, testMode.
- **ProjectBootstrap**: idempotent. It adds 10 tags and layers 8 to 15 and sets the collision matrix. Rotors collides only with Building, Ground and Obstacle. Cup and Human ignore the built-in layers. The trigger pairs for Water and FireZone are set. Last log line: `[Bootstrap] tags=0 layers=0 matrix=12 assets=0`; subsequent runs report all zeros.

### Fix rounds
1. Collision matrix: Rotors, Cup and Human still collided with the built-in layers (Default, TransparentFX, Ignore Raycast, UI). Fixed in `ProjectBootstrap.cs`. Re-check passed.

### Decisions to confirm (chosen defaults, see GAME_DESIGN.md)
- Crash: humans already in the pool still score.
- All humans dead with none in the pool counts as Lost.
- Extinguishing a side clears its RoofOnFire.
- Move input is relative to the heli's yaw.
- The heli Rigidbody has gravity off.
- In test mode the heli moves via `MovePosition`, and the cup follows rigidly.
- Config fields are public (not private `[SerializeField]`).
- Extra layer pairs: Heli collides with Building, Ground and Obstacle. Water collides with Water.

### Known notes
- The third-party package `com.wooshii.hierarchydecorator` logged older CS0246 `EntityId` errors in Editor.log. It is not part of the project asmdefs and did not appear in the current compile.
- Stubs not in the contract that scene-builder must assign: `Cup.humanAnchor`, `Cup.bowlBody`, `FireSide.side`, `RoofZone.fireSide`, and the HUD references.

## Phase 1: NEXT (helicopter, cup, test mode skeleton)
Start with **P1-1 HeliController + PlayerControlSource** (gameplay-coder). Then P1-2 RotorHazard/Crash, P1-3 Cup chain + acceleration, P1-4 Testing skeleton, P1-5 MainEntry wiring check, P1-6 scene (scene-builder) and P1-7 HeliTests (playtester: `heli_smooth_accel`, `heli_yaw_drift`, `heli_rotor_crash`, `testmode_toggle`). See TASKS.md for files and checks.
