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
- ~~Build Settings SampleScene at index 0~~: fixed in Phase 2 (SampleScene removed, Main.unity is index 0).
- Cup.dropPrefab and MainEntry humans/fire/game/hud are empty until Phases 2 to 4.
- The CLI rejects `--caller`/`--skill`. In Git Bash, use `MSYS_NO_PATHCONV=1` for hierarchy paths.

## Phase 2: DONE (2026-11-17)

Gate: compile clean (0 errors, 0 warnings), Main.unity plays with no errors (only the 3 expected MainEntry "humans/game/hud skipped" warnings), all 12 PlayMode tests PASS (7 FireWaterTests + 5 HeliTests). Two fix rounds were needed.

### Done
- **Camera**: package `com.unity.cinemachine` 3.1.4 added (pulls com.unity.splines 2.0.0). Core `ICameraInputSource { Vector2 Look; float Zoom; }`, implemented by `PlayerControlSource` (`<Mouse>/delta`, `<Mouse>/scroll/y`). `HeliCameraController` (Heli module, `[Inject]` HeliConfig, ICameraInputSource, HeliController) drives a `CinemachineCamera` with OrbitalFollow (Sphere, WorldSpace binding) + RotationComposer: mouse orbits yaw and pitch (clamped, above and below the heli), always looks at the heli, scroll zooms between min and max distance with smoothing. Input skipped in test mode. 11 `camera*` fields in HeliConfig. Scene: CinemachineBrain on Main Camera, `HeliCamera` object (no InputAxisController), MainEntry.heliCamera assigned, initialized after heli.
- **P2-1 Pool and water**: `Cup.FillTick/Water01/WaterChanged/SetInPool/InPool`; `Cup.Tug` spawns `WaterDrop` (scaled to dumpDropRadius, inherits bowl velocity, destroyed after dropLifetime), empties the cup, then `ReleaseAll` (still a Phase 3 stub). `WaterDrop.Launch/ReportHit` extinguishes a FireSide (via parent chain) or a GroundFireZone, dies on pool/ground/building/obstacle; test mode = kinematic. `PoolZone` forwards Enter/Stay/Exit for tag Cup.
- **P2-2 Fire**: `FireSide` per GAME_DESIGN section 4 (start delay, growth, extinguish threshold, pause `maxStopFireTime`, restart from 0, RoofOnFire + `ReachedTop` at the top, lost mode, `ApplyBuilding` sizes burn band and water catch). New `FireSideTrigger` forwards burn band child triggers to the FireSide. `FireSystem` initializes sides and ground zones, raises `AllSidesBurning` once. `GroundFireZone` kills IKillable; `Extinguish` only when not permanent.
- **P2-3 RoofZone**: `OnFire` from FireSide.RoofOnFire, subscribed to `ReachedTop`. Human branches in PoolZone, RoofZone, GroundFireZone and FireSide are `// TODO Phase 3:` (contract calls only).
- **P2-4 MainEntry**: optional `heliCamera`; PlayerControlSource also installed as `ICameraInputSource`. Contract additions for Phase 2 appended to CLAUDE.md.
- **P2-5 Scene**: Building 12x12, 5 floors x 3.5 m, at origin; Side_N/E/S/W (FireSide, Wall on Building, BurnBand trigger FireZone/BuildingSide with FireSideTrigger, WaterCatch trigger FireZone). Roof (y 17.5) with RoofSlab and 4 RoofZones (FireZone/Roof, half-roof each, corners overlap). Pool at x=25 (PoolZone trigger 10x2.5x6, Water/Pool, raised basin with Obstacle walls). GroundFire: 4 strips covering all ground outside the pool + `GroundFire_Slab` 200x200 trigger at y=-3 for falls. FireSystem sides[4], groundZones[5]. `Assets/Prefabs/WaterDrop.prefab` (Water/WaterDrop, gravity, trigger r 0.5) on Cup.dropPrefab. Heli moved to (-25,10,0). Layers and tags read back from the scene YAML.
- **P2-6 Tests** `Assets/Tests/PlayMode/FireWaterTests.cs`: `fill_in_pool` (4.02 s vs 4.00 expected), `dump_extinguishes_then_regrows`, `partial_water_no_effect`, `top_sets_roof_fire`, `lost_mode_ignores_water`, `tug_empties_cup`, `camera_follows_heli` PASS. FireSide components are disabled in tests; time moves only through `Advance(dt)`. Tests asmdef now references `Unity.Cinemachine`.

### Fix rounds
1. Compile: `BindingMode` lives in `Unity.Cinemachine.TargetTracking`; added the using in HeliCameraController.cs.
2. Tests: `fill_in_pool` stalled at 0.9999991 because `Cup.SetWater` returned early on `Mathf.Approximately`; now an exact compare. `heli_smooth_accel` and `heli_yaw_drift` failed because the heli spawned at (0,10,0) inside the new building (Heli collides with Building); heli moved to (-25,10,0).

### Known notes
- Pool is a raised basin (Ground is one solid box). Say if a sunken pool with a hole in Ground is wanted.
- RoofZone layout (four overlapping half-roof zones) is a scene-builder choice, not from the design.
- `cameraZoomSpeed` 0.01 assumes ~120 scroll units per notch; tune in HeliConfig if zoom feels off.
- PoolZone fills per Cup-tagged collider; only Bowl carries the tag (checked).
- A side extinguished before its start delay goes straight into the pause; until it starts, `SidesNotOnFire` counts it.
- Splines package logged a one-off TypeLoadException importing its own UXML right after install; it did not recur.
- heli_smooth_accel margin still thin (0.1192 vs limit 0.127).

## Plan change (2026-11-18)
Two phases were inserted before humans (see PHASES.md). Old Phase 3 (humans) is now Phase 5, old Phase 4 (rules and UI) is Phase 6, old Phase 5 (effects) is Phase 7. TASKS.md still uses the old numbers until the Phase 3 game-designer step renumbers it.

## Phase 3: DONE (2026-11-18), pending your by-eye Play mode check
Fire and water visuals, pool filling in real play, controls hint.

Gate: compile clean (0 errors, 0 warnings in project assemblies), all 16 PlayMode tests PASS (4 new VisualsTests + 7 FireWaterTests + 5 HeliTests), test mode OFF for the new tests. No fix rounds were needed. The by-eye part of the gate (walls visibly burn, roof sector lights, tank turns blue, hint visible) is still to be confirmed by you in Play mode.

### Done
- **Design**: GAME_DESIGN.md section 9 "Visuals and controls hint", section 10 "Fire and water ball (Phase 4)", new config fields in section 7. TASKS.md renumbered (humans = 5, rules/UI = 6, effects = 7) with new task lists P3-1..P3-10 and P4-1..P4-7.
- **Shader** `Assets/Shaders/FillEffect.shader` (`FlameAndBlame/FillEffect`): hand-written HLSL, URP Unlit, Transparent, Cull Off, all 10 properties in `UnityPerMaterial` (SRP Batcher). Guarded Y normalization; noise fades out at `_Fill` 0 and 1, so 0 shows nothing and 1 is full. Alpha multiplied by `_Intensity`.
- **Materials** `Assets/VFX/M_Fire.mat` (orange fill, yellow edge, base alpha 0), `M_Water.mat` (blue, base light grey alpha 0.3, `_Fill` 1). Created via Unity CLI.
- **VFX**: `FillEffectView` (MaterialPropertyBlock only, SmoothDamp, `_MinY/_MaxY` from mesh bounds, `RefreshBounds()`, read-only `CurrentFill/TargetFill/CurrentIntensity`, serialized `initialFill/initialIntensity`). `RoofSectorMesh` (serialized `side`, `sizeX`, `sizeZ`, `centerRise` 0.3; `Initialize(float, float)`).
- **UI**: `UIConfig` (controlsHintText, hintFontSize 22, hintMargin (20,20)); asset created by ProjectBootstrap (`assets=1` on that run). `ControlsHintView` (`[Inject] UIConfig`, `Initialize()`, `ShownText`, uses `tmpText` if assigned, else `legacyText`; anchors top-right).
- **Gameplay wiring**: FireSide `wallFireView` (SetFill(Height01), SetIntensity(IsBurning); updated in Update and at the end of `Advance`), read-only `Config`, `Building`. RoofZone `roofFireView`, `roofSector`, `Refresh()` (lazy one-time setup: SmoothTime = roofIgniteTime, sector sized from `FireSide.Building`). Cup `waterView` (SmoothTime = fillVisualSmoothTime, follows WaterChanged). FireConfig `roofIgniteTime` 1.5, CupConfig `fillVisualSmoothTime` 0.25. MainEntry `uiConfig`, `controlsHint` (initialized after hud, before testMode).
- **Scene**: `FireOverlay` on each side (cube, no collider, local (0, 8.75, 0.06), scale (12, 17.5, 0.02), M_Fire, SmoothTime 0.1). `RoofFire_North/East/South/West` under Roof at roof-local (0, 0.55, 0) (M_Fire, SmoothTime 1.5). Bowl uses M_Water + FillEffectView. New `Pool/PoolSurface` (top at y 2.8, 9.4 x 5.4, M_Water, fill 1). `Game/UI/Canvas` (Screen Space Overlay, scaler 1920x1080) with `ControlsHint` (legacy Text, built-in font, 500x300, top-right, -20,-20). All new fields read back non-null.
- **Tests** `Assets/Tests/PlayMode/VisualsTests.cs` (test mode OFF): `wall_overlay_follows_height` (overlay `_Fill` within 0.002 of Height01 at 4 heights, all sides), `roof_sector_only_own_side` (North 0.990, others 0.000), `tank_fills_in_pool_scripted` (Water01 0.01 → 0.51, Bowl `_Fill` 0.005 → 0.448), `controls_hint_matches_config` PASS. Tests asmdef now also references `Unity.TextMeshPro` and `UnityEngine.UI`.

### Deviations from the contract wording (additive, CLAUDE.md not edited)
- `RoofSectorMesh` takes floats (`Initialize(float sizeX, float sizeZ)`), not `BuildingConfig`: VFX cannot reference Fire. RoofZone passes `FireSide.Building` values.
- New read-only `FireSide.Config` / `FireSide.Building`, `RoofZone.Refresh()`, `RoofZone.roofSector`, `FillEffectView.RefreshBounds/CurrentFill/TargetFill/CurrentIntensity`, `ControlsHintView.ShownText/legacyText/tmpText`. Consider adding these to the CLAUDE.md contract.

### Known notes
- TMP Essential Resources are not imported, so the hint uses `UnityEngine.UI.Text` with the built-in default font (fileID 10102, the LegacyRuntime font; the CLI could not confirm it by name).
- `tank_fills_in_pool_scripted` teleports the heli and chain to (25,10,0), lets the bowl settle, then descends with ScriptedControlSource Vertical = -1 (it does not fly the 50 m from spawn).
- The roof sector rises 0.3 m toward the roof centre (fire spreads from the wall edge inward); the sector mesh uses HideAndDontSave, so it may not show in Edit mode after a scene reload until Play.
- `dumpDropRadius`/`dropLifetime` stay in CupConfig until Phase 4 replaces the drop with the ball.
- The CLI accepts `--caller/--skill` on `unity command` but not on `unity status`/test calls in this version.

## Phase 4: DONE (2026-11-18), pending your by-eye Play mode check
Roof slab rework and camera-relative movement. The water ball drop was already implemented.

Gate: compile clean (0 errors, 0 warnings in project assemblies), all 26 PlayMode tests PASS (7 RoofSlabTests + 3 HeliCameraMoveTests + 7 FireWaterTests + 5 HeliTests + 4 VisualsTests), test mode OFF for the new tests. Two fix rounds were needed.

### Done
- **Design**: GAME_DESIGN.md: section 10 is now "Water ball and roof slabs" (ball drop marked done). RoofZone is replaced by RoofSlab everywhere, including the Phase 5 human rules. Heli feel is camera-relative with Z/X turning, and the old "move relative to yaw" decision is replaced. TASKS.md: P4-1..P4-7; P5 tasks spawn through `FireSystem.RoofSlabs` / `GetSpawnPoint`.
- **RoofSlab** (Fire): `fireSide`, `roofFireView`, `humanSpawnArea`. `Initialize` only sets the roof fire smoothing; RoofFire and HumanSpawnArea are placed by hand in `Assets/Prefabs/RoofSlab.prefab` (edit the prefab to change all four roofs). RoofFire has a trigger BoxCollider (layer FireZone) + `RoofSlabTrigger`: while the roof burns, any IKillable touching the fire dies (hanging humans too); no other kill volume. Also `OnFire`, `SpawnBounds`, `GetSpawnPoint()`, `RoofFireView`, gizmo. FireSystem `roofSlabs` / `RoofSlabs`. WaterDrop: a hit in a RoofSlab parent chain extinguishes `RoofSlab.FireSide`. FireConfig `roofFireHeight` 1.5 exists but is now unused.
- **Deleted** RoofZone.cs and RoofSectorMesh.cs (no code references remain).
- **Movement**: `HeliCommand.MoveWorld`; `PlayerControlSource.cameraTransform` (flattened camera forward and right, falls back to the camera's up when looking straight down, falls back to world axes + 1 warning with no camera); HeliController moves along MoveWorld only; Z = Yaw -1, X = Yaw +1, Q/E removed; ScriptedControlSource maps Move to world X/Z when MoveWorld is unset. UIConfig hint default and UIConfig.asset now: "WASD move (camera) | Space/Ctrl up/down | Z/X turn | F drop water | Mouse look | Wheel zoom".
- **Scene**: RoofSlab on the 4 hand-made slabs (transforms and sizes unchanged), mapped by position: `RoofSlab`=North, `RoofSlab (1) east`=East, `RoofSlab (2)`=South, `RoofSlab (1)`=West. HumanSpawnArea children were added. All four slabs are on layer Building with tag Roof; East's BoxCollider was a trigger and is now solid. FireSystem.roofSlabs is in Side order. `cameraTransform` = Main Camera. RoofFire MeshFilters were overridden to null in the instances and are now the Cube mesh.
- **Tests**: `RoofSlabTests.cs` (roof_fire_covers_slab_top, roof_only_own_slab_ignites, roof_extinguish_clears_slab, ball_on_slab_extinguishes_side, spawn_area_inset, burning_slab_kills_killable, hint_shows_z_x), `HeliCameraMoveTests.cs` (move_camera_relative, move_ignores_heli_yaw, yaw_keys_z_x; virtual Keyboard via InputSystem.AddDevice). The Tests asmdef also references `Unity.InputSystem`. HeliTests and VisualsTests were updated for MoveWorld and RoofSlabs.

### Fix rounds
1. roof_fire_covers_slab_top: the RoofFire MeshFilters were null (instance overrides; the old RoofSectorMesh had built the mesh). Set to Cube. spawn_area_inset: `SpawnBounds` ignored slab rotation (North/South slabs rotY 270/90). Now the AABB of the 8 rotated corners.
2. Compile: CS0136 duplicate local `b` in that fix; renamed.

### Follow-up (user request, same day)
- Roof slabs are now 4 instances of `Assets/Prefabs/RoofSlab.prefab` ("RoofSlab north/east/south/west"); FireSystem.roofSlabs rewired in Side order. RoofSlab no longer moves its children; the generated 2 m TopTrigger is gone. Tests replaced: `roof_children_not_moved_and_fire_collider`, `spawn_points_in_area`, `burning_slab_kills_killable` (inside the fire collider dies, 1.5 m above does not). All 26 PlayMode tests PASS, no missing-script warnings. CLAUDE.md, GAME_DESIGN.md and TASKS.md updated to match.

### Open issues
- **Yaw drift is strong**: up to ~64 deg in 1.5 s with no input (yawDriftTorque 0.6, maxYawRate 60). Command lag ~2 s. Retune if it feels wrong.
- Test-runner temp scene `Assets/InitTestScene<guid>.unity` may be left in Assets (untracked); safe to delete.
- Phase 5: "released human lands on slab -> WaitingOnRoof" is checked on the Human side (tag Roof), since Fire cannot reference Rescue.

## Phase 5: NEXT (humans)
