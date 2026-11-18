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

## Phase 1: DONE (2026-11-17)

Gate: compile clean (0 errors, 0 warnings), Main.unity plays with no errors (only the 4 expected MainEntry "skipped" warnings), all 5 HeliTests PASS. Two fix rounds were needed.

### Done
- **P1-1 HeliController**: clamped acceleration (|dv| <= maxAcceleration*dt per step in both modes), Perlin yaw drift (also in test mode), move relative to yaw, gravity off; mass, damping and interpolation are applied from HeliConfig in Initialize. Public API used by tests: `Velocity`, `LastAcceleration`, `IsTestMode`, `ControlSource`. Test mode integrates its own velocity and moves with MovePosition/MoveRotation, then hands the velocity back to the Rigidbody on exit. Crash: gravity on, damping 0, stops driving; in test mode it only sets the flag and fires the event.
- **PlayerControlSource / ScriptedControlSource**: unchanged from Phase 0. They were already correct.
- **P1-2 RotorHazard**: `ReportHit` ignores triggers, the tags Cup/Human/Heli/WaterDrop and anything under the same heli. Otherwise it calls `Crash()`.
- **P1-3 Cup**: Initialize builds `segmentCount` ConfigurableJoints. Runtime segments live under a `CupChain` root, and the bowl is the last segment (it is reparented there). `Joints` is exposed. `Acceleration` is the low-passed second derivative of the bowl position. In test mode the chain follows the heli rigidly. Water, grab and Tug are still stubs.
- **P1-4 Testing**: TestModeController (`EnterAll/ExitAll`, `Scripted`, `IsActive`, `Actors`), ScenarioRunner (`Register/Run/RunAll`, static `Pass/Fail/Check`).
- **P1-5 MainEntry**: installs and initializes only the assigned objects and warns for the rest. A missing dependency still throws KeyNotFoundException.
- **P1-6 Scene** `Assets/Scenes/Main.unity`: MainEntry; Game/Environment/Ground (layer Ground); Game/Systems (empty); Game/Actors/Heli (Heli layer/tag, Body, MainRotor and TailRotor on Rotors/Rotor with RotorHazard, Cup with Bowl (Cup/Cup) holding HumanAnchor and the GrabZone trigger (CupGrabZone)); camera, light; inactive TestHarness. Every MainEntry config field and the heli, cup, playerControl and testMode fields are assigned.
- **P1-7 Tests** `Assets/Tests/PlayMode/HeliTests.cs`: `[TEST] heli_smooth_accel PASS`, `heli_smooth_accel_testmode PASS`, `heli_yaw_drift PASS`, `heli_rotor_crash PASS`, `testmode_toggle PASS`.

### Fix rounds
1. heli_smooth_accel failed: on step 1 the chain jolted the heli by |dv| = 4.24 m/s. Chain segments are now built at the heli's rotation, and a new `CupConfig.heliIsolationMassScale` (default 100) was added for joint 0.
2. That made things worse (31 m/s), because `Joint.connectedMassScale` scales the INVERSE mass. Fixed to `connectedMassScale = 1 / heliIsolationMassScale`. Worst step is now 0.119 against the 0.12 limit, and the heli sags only 0.05 m in 3 s from the cup's weight.

### Known notes
- The heli_smooth_accel margin is thin (0.119 vs 0.12). Raising `heliIsolationMassScale` or lowering the chain masses would widen it.
- heli_yaw_drift depends on the random Perlin seed. Runs gave 1.2 to 25 deg over 10 s, and the test's 0.5 deg minimum could fail on a very quiet seed. Consider a fixed seed in test mode.
- Build Settings still has SampleScene at index 0 (Main.unity is index 1). Remove it or reorder.
- Cup.dropPrefab and MainEntry humans/fire/game/hud are empty until Phases 2 to 4.
- The CLI rejects `--caller`/`--skill`. In Git Bash, use `MSYS_NO_PATHCONV=1` for hierarchy paths.

## Phase 2: NEXT (pool, water, building, fire)
Start with **P2-1 Pool and water** (gameplay-coder). See TASKS.md.
