# Flame and Blame: tasks

Paths are relative to X:\Unity\Flame and Blame. S = Assets/Scripts. Owners: gameplay-coder, scene-builder, vfx-artist, playtester, compiler. Order inside a phase follows PHASES.md. Every phase that adds a system adds its `[SerializeField]` and Install/Initialize lines to S/Main/MainEntry.cs, and scene-builder assigns the field. Test lines: `[TEST] <name> PASS`.

## Phase 0: foundation
- P0-1 asmdefs. gameplay-coder. Files: S/Core/FlameAndBlame.Core.asmdef, S/VFX/FlameAndBlame.VFX.asmdef, S/Heli/FlameAndBlame.Heli.asmdef, S/Fire/FlameAndBlame.Fire.asmdef, S/Rescue/FlameAndBlame.Rescue.asmdef, S/GameRules/FlameAndBlame.GameRules.asmdef, S/UI/FlameAndBlame.UI.asmdef, S/Testing/FlameAndBlame.Testing.asmdef, S/Main/FlameAndBlame.Main.asmdef, Assets/Editor/FlameAndBlame.EditorTools.asmdef, Assets/Tests/PlayMode/FlameAndBlame.Tests.PlayMode.asmdef. Check: references match GAME_DESIGN.md section 8, by name.
- P0-2 Core types. gameplay-coder. Files: S/Core/Enums.cs, S/Core/HeliCommand.cs, S/Core/Interfaces.cs. Check: all enums and interfaces from CLAUDE.md exist.
- P0-3 Config classes with all fields of GAME_DESIGN.md section 7. gameplay-coder. Files: S/Heli/HeliConfig.cs, S/Rescue/CupConfig.cs, S/Rescue/HumanConfig.cs, S/Fire/FireConfig.cs, S/Fire/BuildingConfig.cs, S/GameRules/GameRulesConfig.cs (with PoolConfig). Check: defaults equal the section 7 numbers.
- P0-4 Stubs for every contract class including the additions (empty bodies, public members present). gameplay-coder. Files: S/Heli/{HeliController,PlayerControlSource,RotorHazard}.cs, S/Fire/{FireSide,FireSystem,GroundFireZone}.cs, S/Rescue/{Cup,WaterDrop,Human,HumanAnimator,HumansSystem,PoolZone,RoofZone}.cs, S/GameRules/GameManager.cs, S/UI/{HudController,WaterGauge,HumanStrengthBar,ResultPanel}.cs, S/Testing/{TestModeController,ScriptedControlSource,ScenarioRunner}.cs, S/VFX/{EffectController,FireEffect,WaterSplashEffect,StunStarsEffect,SmokeEffect}.cs. Check: compiles.
- P0-5 ProjectBootstrap. gameplay-coder. Files: Assets/Editor/ProjectBootstrap.cs. Creates tags, layers 8 to 15, 6 config assets in Assets/Settings/, collision matrix including trigger pairs from CLAUDE.md "Contract additions". Check: console line `[Bootstrap] done`; layers, tags and assets exist; running twice changes nothing.
- P0-6 MainEntry. gameplay-coder. Files: S/Main/MainEntry.cs per CLAUDE.md plus BuildingConfig installed. Check: compiles.
- P0-7 Compile gate. compiler. Check: zero errors, no cyclic or missing references.

## Phase 1: helicopter, cup, test mode skeleton
- P1-1 HeliController, PlayerControlSource. gameplay-coder. Files: S/Heli/HeliController.cs, S/Heli/PlayerControlSource.cs. Check: velocity changes by less than `maxAcceleration * dt` per step.
- P1-2 RotorHazard and Crash. gameplay-coder. Files: S/Heli/RotorHazard.cs. Check: ReportHit(Ground) sets IsCrashed; ReportHit(cup collider) does not.
- P1-3 Cup chain and acceleration (no water yet). gameplay-coder. Files: S/Rescue/Cup.cs. Check: chain exists after Initialize with `segmentCount` joints; Acceleration non-zero when the heli moves.
- P1-4 Testing skeleton. gameplay-coder. Files: S/Testing/TestModeController.cs, S/Testing/ScriptedControlSource.cs, S/Testing/ScenarioRunner.cs, ITestModeAware on heli and cup. Check: EnterAll then ExitAll leaves no errors and restores isKinematic and the source.
- P1-5 MainEntry wiring. gameplay-coder. Files: S/Main/MainEntry.cs. Check: no KeyNotFoundException at start.
- P1-6 Scene. scene-builder. Files: Assets/Scenes/Main.unity. Game/Environment/Ground, Game/Actors/Heli (Body, MainRotor, TailRotor, Rigidbody, scripts), Cup (bowl, HumanAnchor, GrabZone), disabled TestHarness, assign MainEntry fields. Check: scene plays with no console errors.
- P1-7 Tests. playtester. Files: Assets/Tests/PlayMode/HeliTests.cs. `[TEST] heli_smooth_accel`, `heli_yaw_drift`, `heli_rotor_crash`, `testmode_toggle` all PASS.

## Phase 2: pool, water, building, fire
- P2-1 Pool and water. gameplay-coder. Files: S/Rescue/PoolZone.cs, S/Rescue/WaterDrop.cs, S/Rescue/Cup.cs (FillTick, Water01, WaterChanged, SetInPool, Tug drop without humans). Check: FillTick x N raises Water01 to 1.
- P2-2 Fire. gameplay-coder. Files: S/Fire/FireSide.cs, S/Fire/FireSystem.cs, S/Fire/GroundFireZone.cs. Human calls only through IKillable. Check: growth, extinguish, pause, restart, lost mode per GAME_DESIGN.md section 4.
- P2-3 RoofZone fire flag (human branch left as TODO through the contract). gameplay-coder. Files: S/Rescue/RoofZone.cs. Check: compiles.
- P2-4 MainEntry. gameplay-coder. Files: S/Main/MainEntry.cs (FireSystem, FireConfig, BuildingConfig, CupConfig). Check: injects.
- P2-5 Scene. scene-builder. Files: Assets/Scenes/Main.unity, Assets/Prefabs/WaterDrop.prefab. Building (5 floors from BuildingConfig, Side_N/E/S/W with burn band and water catch trigger), Roof with 4 RoofZones, Pool (layer Water, tag Pool) placed from PoolConfig, GroundFire triggers plus slab below ground, assign FireSystem. Check: layers and tags correct, no errors.
- P2-6 Tests. playtester. Files: Assets/Tests/PlayMode/FireWaterTests.cs. `fill_in_pool`, `dump_extinguishes_then_regrows`, `partial_water_no_effect`, `top_sets_roof_fire`, `lost_mode_ignores_water` PASS.

## Phase 3: fire and water visuals, pool filling, controls hint
- P3-1 Shader and materials. vfx-artist. Files: Assets/Shaders/FillEffect.shader, Assets/VFX/M_Fire.mat, Assets/VFX/M_Water.mat. Check: shader compiles with no console errors; materials use the shader; all properties of GAME_DESIGN.md section 9 exist.
- P3-2 FillEffectView and RoofSectorMesh. vfx-artist. Files: S/VFX/FillEffectView.cs, S/VFX/RoofSectorMesh.cs. Check: compiles; SetFill(0.5) with SmoothTime 0 gives `_Fill` 0.5 in GetPropertyBlock; no material instance created; four sectors do not overlap.
- P3-3 UIConfig and ProjectBootstrap. gameplay-coder. Files: S/UI/UIConfig.cs, Assets/Editor/ProjectBootstrap.cs (creates UIConfig.asset with the section 7 defaults). Check: `[Bootstrap] done`, UIConfig.asset exists, second run changes nothing.
- P3-4 Config fields. gameplay-coder. Files: S/Rescue/CupConfig.cs (fillVisualSmoothTime), S/Fire/FireConfig.cs (roofIgniteTime). Check: defaults equal section 7.
- P3-5 View wiring. gameplay-coder. Files: S/Fire/FireSide.cs (wallFireView), S/Rescue/RoofZone.cs (roofFireView, SetIgniteTime), S/Fire/FireSystem.cs (passes roofIgniteTime), S/Rescue/Cup.cs (waterView, WaterChanged). Check: views are null-safe; FireSide drives SetFill(Height01) and SetIntensity(IsBurning ? 1 : 0).
- P3-6 ControlsHintView and MainEntry. gameplay-coder. Files: S/UI/ControlsHintView.cs, S/Main/MainEntry.cs (uiConfig, controlsHint, Initialize after hud). Check: no KeyNotFoundException; TMP used, or LegacyRuntime.ttf fallback reported.
- P3-7 Compile gate. compiler. Check: zero errors; UIConfig.asset exists.
- P3-8 Scene. scene-builder. Files: Assets/Scenes/Main.unity. FireOverlay under each Side_X (0.05 m outside, M_Fire, FillEffectView, no collider); RoofFire_North/East/South/West under Roof (RoofSectorMesh, M_Fire, FillEffectView, 0.05 m above the slab); cup bowl with M_Water and FillEffectView; pool surface M_Water _Fill 1; Canvas with the hint text top-right; assign every new field. Check: no null fields, no console errors.
- P3-9 Compile gate. compiler. Check: zero errors.
- P3-10 Tests (test mode OFF, real physics). playtester. Files: Assets/Tests/PlayMode/VisualsTests.cs. `overlay_fill_follows_height`, `roof_sector_only_own_side`, `tank_fills_in_pool_view`, `hint_text_matches_config` PASS (values read with Renderer.GetPropertyBlock). All earlier tests still PASS.

## Phase 4: roof slabs and camera-relative movement
The water ball drop (WaterBall layer, solid WaterDrop, Cup.Tug, WaterBallTests) is ALREADY IMPLEMENTED; see GAME_DESIGN.md section 10. Remaining work:
- P4-1 RoofSlab and Fire changes. gameplay-coder. Files: S/Fire/RoofSlab.cs (new, per GAME_DESIGN.md section 1), S/Fire/FireSystem.cs (serialized `RoofSlab[] roofSlabs`, `RoofSlabs`, initializes slabs), S/Fire/FireConfig.cs (roofFireHeight 1.5), delete S/Rescue/RoofZone.cs and S/VFX/RoofSectorMesh.cs (and their references in Cup, FireSide, tests, MainEntry), S/Rescue/WaterDrop.cs (hit with RoofSlab in the parent chain extinguishes `RoofSlab.FireSide`; no nearest-edge math), Assets/Editor/ProjectBootstrap.cs (FireConfig.roofFireHeight default, and sets it on an existing FireConfig.asset only if the field is 0). Check: compiles; no `RoofZone` or `RoofSectorMesh` left in Assets; FireConfig.asset roofFireHeight 1.5; second Bootstrap run changes nothing.
- P4-2 Camera-relative movement. gameplay-coder. Files: S/Core/HeliCommand.cs (MoveWorld, already in the contract), S/Heli/PlayerControlSource.cs (serialized `cameraTransform`, MoveWorld per section 1, Z = Yaw -1, X = Yaw +1, Q and E removed, one warning when the camera is null), S/Heli/HeliController.cs (moves along MoveWorld only, smoothing on MoveWorld in world space, no yaw-relative move), S/Testing/ScriptedControlSource.cs (tests set MoveWorld; only Move set maps x to world X, y to world Z), S/UI/UIConfig.cs (controlsHintText default "WASD move (camera) | Space/Ctrl up/down | Z/X turn | F drop water | Mouse look | Wheel zoom"), Assets/Settings/UIConfig.asset (same value; the old Q/E text is replaced, by ProjectBootstrap or by editing the asset). Check: compiles; asset text equals the default; HeliCameraController still ignores input in test mode.
- P4-3 Update tests that used RoofZone or the nearest-edge rule. gameplay-coder. Files: Assets/Tests/PlayMode/*.cs. Check: compile; `ball_roof_hit_nearest_side` is renamed `ball_roof_hit_slab_side` in P4-6; tests that used `RoofZone.ReportEnter` use RoofSlab.
- P4-4 Compile gate. compiler. Check: zero errors; no `RoofZone` or `RoofSectorMesh` symbols.
- P4-5 Scene wiring. scene-builder. Files: Assets/Scenes/Main.unity. On the four existing hand-made slabs under Roof (keep their sizes): add the `RoofSlab` component, BoxCollider non-trigger layer Building tag Roof, a plain-box child `RoofFire` (no collider, `M_Fire`, `FillEffectView`), a `HumanSpawnArea` child, assign `fireSide` (the matching Side_X), `roofFireView` and `humanSpawnArea`; remove the old RoofZone and RoofSectorMesh objects and components (RoofFire_North/East/South/West of Phase 3); assign `FireSystem.roofSlabs` (4 entries); assign `PlayerControlSource.cameraTransform` to the Main Camera. Check: no null serialized fields, no missing scripts, no console errors on play; slabs do not overlap each other.
- P4-6 Compile gate. compiler. Check: zero errors.
- P4-7 Tests (test mode OFF, real physics). playtester. Files: Assets/Tests/PlayMode/RoofSlabTests.cs, Assets/Tests/PlayMode/HeliControlTests.cs. `[TEST]` lines, all PASS:
  - `roof_children_not_moved_and_fire_collider`: Initialize does not move RoofFire or HumanSpawnArea; RoofFire has a trigger BoxCollider on FireZone with RoofSlabTrigger; no TopTrigger.
  - `roof_only_own_slab_ignites`: when one side reaches the top, only that slab's OnFire and view fill are 1; the other three stay 0.
  - `roof_extinguish_clears_slab`: Extinguish on that side sets OnFire false and the view fill falls to 0.
  - `ball_on_slab_extinguishes_side`: a real water ball dropped on a burning slab extinguishes that slab's FireSide; a ball on another slab does not.
  - `spawn_points_in_area`: 200 `GetSpawnPoint` results lie on the bottom face of the hand-placed spawn box and inside SpawnBounds.
  - `killable_on_burning_slab_dies`: an IKillable test object in the slab trigger is killed while OnFire, and not killed when not on fire.
  - `move_camera_relative`: with the camera yawed 90 degrees from the heli, pressing W moves the heli along the camera forward within 5 degrees, and D along the camera right within 5 degrees.
  - `heli_yaw_does_not_change_move`: after turning the heli, the same camera-relative key gives the same world move direction.
  - `yaw_keys_direction`: Z decreases heli yaw angle (counterclockwise from above), X increases it.
  - `hint_shows_zx`: the hint text contains "Z/X" and does not contain "Q/E".
  All earlier tests still PASS.

## Phase 5: humans
- P5-1 Human and animator. gameplay-coder. Files: S/Rescue/Human.cs, S/Rescue/HumanAnimator.cs, S/Rescue/HumansSystem.cs. Check: transition table in GAME_DESIGN.md section 3.
- P5-2 Cup TryGrab, ReleaseAll. gameplay-coder. Files: S/Rescue/Cup.cs (Tug already done in Phase 4). Check: rules 1 to 5.
- P5-3 Human branches in zones. gameplay-coder. Files: S/Rescue/PoolZone.cs, S/Rescue/Human.cs (collision report: landing on a collider tagged Roof, unattached, in RescuedHanging or SwimmingInTank -> WaitingOnRoof), S/Fire/RoofSlab.cs (ReportStay kills IKillable while OnFire), S/Fire/GroundFireZone.cs, S/Fire/FireSide.cs. Check: compiles, no upward reference (Fire does not reference Rescue).
- P5-4 MainEntry. gameplay-coder. Files: S/Main/MainEntry.cs (HumansSystem, HumanConfig). HumansSystem spawns or places humans on slabs through FireSystem.RoofSlabs and `RoofSlab.GetSpawnPoint()`, one per slab, so humans.Initialize runs after fire.Initialize (already the order). Check: injects without KeyNotFoundException.
- P5-5 Scene. scene-builder. Files: Assets/Scenes/Main.unity. 4 humans (layer and tag Human, Rigidbody, capsule, HumanAnimator) filled into HumansSystem, placed at start on the RoofSlabs (one per slab, inside HumanSpawnArea via GetSpawnPoint). Check: humans start in S1 and each stands on its slab.
- P5-6 Tests. playtester. Files: Assets/Tests/PlayMode/HumanTests.cs. `grab_empty_two`, `grab_third_rejected`, `full_cup_bounce_stun`, `pool_grab_state4`, `release_pool_state3`, `release_roof_state1` (released human lands on a RoofSlab), `release_ground_fire_dead`, `roof_fire_kills` (human on a burning slab dies), `side_fire_kills`, `strength_drops_and_falls` PASS.

## Phase 6: game rules and UI
- P6-1 GameManager. gameplay-coder. Files: S/GameRules/GameManager.cs. Check: rules and score per GAME_DESIGN.md.
- P6-2 UI. gameplay-coder. Files: S/UI/HudController.cs, S/UI/WaterGauge.cs (optional, the tank shading is the indicator), S/UI/HumanStrengthBar.cs, S/UI/ResultPanel.cs. Check: no polling, events only.
- P6-3 MainEntry. gameplay-coder. Files: S/Main/MainEntry.cs (GameManager, GameRulesConfig, HudController).
- P6-4 Scene. scene-builder. Files: Assets/Scenes/Main.unity. Result panel on the existing Canvas, world-space strength bar per human, HudController arrays, GameManager under Systems. Check: no errors at start.
- P6-5 Tests. playtester. Files: Assets/Tests/PlayMode/GameRulesTests.cs. `crash_lost`, `crash_kills_all_and_sides_lost`, `all_sides_burning_lost`, `all_dead_lost`, `all_resolved_won`, `score_matches_config` PASS.

## Phase 7: effects and polish
- P7-1 VFX controllers (Play/Stop only). vfx-artist. Files: S/VFX/*.cs, Assets/Prefabs/VFX/{Fire,GroundFire,WaterDrop,Splash,StunStars,Smoke}.prefab, simple materials in Assets/Materials/. Check: Play and Stop run in the Editor without errors.
- P7-2 Hooks. gameplay-coder. Files: S/Fire/FireSide.cs, S/Fire/GroundFireZone.cs, S/Rescue/WaterDrop.cs (Hit event), S/Rescue/Human.cs (Stun), S/Rescue/Cup.cs (Tug). Check: compiles, effects null-safe.
- P7-3 Assign prefabs. scene-builder. Files: Assets/Scenes/Main.unity. Check: no missing references.
- P7-4 Full suite. playtester. Files: all Assets/Tests/PlayMode/*.cs. Check: every `[TEST]` line PASS; list in PROGRESS.md with a tuning guide pointing at Assets/Settings/.
