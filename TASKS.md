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

## Phase 3: humans
- P3-1 Human and animator. gameplay-coder. Files: S/Rescue/Human.cs, S/Rescue/HumanAnimator.cs, S/Rescue/HumansSystem.cs. Check: transition table in GAME_DESIGN.md section 3.
- P3-2 Cup TryGrab, ReleaseAll, Tug. gameplay-coder. Files: S/Rescue/Cup.cs. Check: rules 1 to 5.
- P3-3 Human branches in zones. gameplay-coder. Files: S/Rescue/PoolZone.cs, S/Rescue/RoofZone.cs, S/Fire/GroundFireZone.cs, S/Fire/FireSide.cs. Check: compiles, no upward reference.
- P3-4 MainEntry. gameplay-coder. Files: S/Main/MainEntry.cs (HumansSystem, HumanConfig).
- P3-5 Scene. scene-builder. Files: Assets/Scenes/Main.unity. 4 humans (layer and tag Human, Rigidbody, capsule, HumanAnimator) on the roof by `roofHumanSpawnMargin`, one per side, filled into HumansSystem; RoofZone FireSide refs. Check: humans start in S1.
- P3-6 Tests. playtester. Files: Assets/Tests/PlayMode/HumanTests.cs. `grab_empty_two`, `grab_third_rejected`, `full_cup_bounce_stun`, `pool_grab_state4`, `release_pool_state3`, `release_roof_state1`, `release_ground_fire_dead`, `roof_fire_kills`, `side_fire_kills`, `strength_drops_and_falls` PASS.

## Phase 4: game rules and UI
- P4-1 GameManager. gameplay-coder. Files: S/GameRules/GameManager.cs. Check: rules and score per GAME_DESIGN.md.
- P4-2 UI. gameplay-coder. Files: S/UI/HudController.cs, S/UI/WaterGauge.cs, S/UI/HumanStrengthBar.cs, S/UI/ResultPanel.cs. Check: no polling, events only.
- P4-3 MainEntry. gameplay-coder. Files: S/Main/MainEntry.cs (GameManager, GameRulesConfig, HudController).
- P4-4 Scene. scene-builder. Files: Assets/Scenes/Main.unity. Canvas with WaterGauge and ResultPanel, world-space strength bar per human, HudController arrays, GameManager under Systems. Check: no errors at start.
- P4-5 Tests. playtester. Files: Assets/Tests/PlayMode/GameRulesTests.cs. `crash_lost`, `crash_kills_all_and_sides_lost`, `all_sides_burning_lost`, `all_dead_lost`, `all_resolved_won`, `score_matches_config` PASS.

## Phase 5: effects and polish
- P5-1 VFX controllers (Play/Stop only). vfx-artist. Files: S/VFX/*.cs, Assets/Prefabs/VFX/{Fire,GroundFire,WaterDrop,Splash,StunStars,Smoke}.prefab, simple materials in Assets/Materials/. Check: Play and Stop run in the Editor without errors.
- P5-2 Hooks. gameplay-coder. Files: S/Fire/FireSide.cs, S/Fire/GroundFireZone.cs, S/Rescue/WaterDrop.cs, S/Rescue/Human.cs (Stun), S/Rescue/Cup.cs (Tug). Check: compiles, effects null-safe.
- P5-3 Assign prefabs. scene-builder. Files: Assets/Scenes/Main.unity. Check: no missing references.
- P5-4 Full suite. playtester. Files: all Assets/Tests/PlayMode/*.cs. Check: every `[TEST]` line PASS; list in PROGRESS.md with a tuning guide pointing at Assets/Settings/.
