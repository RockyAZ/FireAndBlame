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

## Phase 4: dropping water as a physical ball
- P4-1 WaterBall layer. gameplay-coder. Files: Assets/Editor/ProjectBootstrap.cs (layer WaterBall 16, collides with Building only). Check: layer exists; WaterBall row of the matrix is Building only; idempotent.
- P4-2 Ball and Tug. gameplay-coder. Files: S/Rescue/WaterDrop.cs (solid ball, Launch with angular velocity, ReportHit(other, point), Hit event, destroy rules), S/Rescue/Cup.cs (Tug per GAME_DESIGN.md section 10), S/Rescue/CupConfig.cs (waterBallRadius, waterBallMass, waterBallLifetime, waterBallKillY). Check: compiles; defaults equal section 7.
- P4-3 Update old tests. gameplay-coder. Files: Assets/Tests/PlayMode/FireWaterTests.cs. Check: tests that used the trigger drop use the new API and compile.
- P4-4 Compile gate. compiler. Check: zero errors; WaterBall layer exists with the right collision row.
- P4-5 Prefab and scene. scene-builder. Files: Assets/Prefabs/WaterDrop.prefab (layer WaterBall, non-trigger SphereCollider, M_Water, tag WaterDrop), Assets/Scenes/Main.unity (WaterCatch colliders disabled). Check: prefab still assigned to the Cup; no missing references.
- P4-6 Compile gate. compiler. Check: zero errors.
- P4-7 Tests (test mode OFF, real physics). playtester. Files: Assets/Tests/PlayMode/WaterBallTests.cs. `ball_spawns_in_tank_with_velocity`, `ball_extinguishes_wall`, `ball_roof_hit_nearest_side`, `ball_miss_falls_through_ground`, `tank_empties_after_tug`, `partial_tank_no_extinguish` PASS. All earlier tests still PASS.

## Phase 5: humans
- P5-1 Human and animator. gameplay-coder. Files: S/Rescue/Human.cs, S/Rescue/HumanAnimator.cs, S/Rescue/HumansSystem.cs. Check: transition table in GAME_DESIGN.md section 3.
- P5-2 Cup TryGrab, ReleaseAll. gameplay-coder. Files: S/Rescue/Cup.cs (Tug already done in Phase 4). Check: rules 1 to 5.
- P5-3 Human branches in zones. gameplay-coder. Files: S/Rescue/PoolZone.cs, S/Rescue/RoofZone.cs, S/Fire/GroundFireZone.cs, S/Fire/FireSide.cs. Check: compiles, no upward reference.
- P5-4 MainEntry. gameplay-coder. Files: S/Main/MainEntry.cs (HumansSystem, HumanConfig).
- P5-5 Scene. scene-builder. Files: Assets/Scenes/Main.unity. 4 humans (layer and tag Human, Rigidbody, capsule, HumanAnimator) on the roof by `roofHumanSpawnMargin`, one per side, filled into HumansSystem; RoofZone FireSide refs. Check: humans start in S1.
- P5-6 Tests. playtester. Files: Assets/Tests/PlayMode/HumanTests.cs. `grab_empty_two`, `grab_third_rejected`, `full_cup_bounce_stun`, `pool_grab_state4`, `release_pool_state3`, `release_roof_state1`, `release_ground_fire_dead`, `roof_fire_kills`, `side_fire_kills`, `strength_drops_and_falls` PASS.

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
