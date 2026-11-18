# Flame and Blame: phase plan

## Recommendation on scope
Build the full brief, but in 6 gated phases. One giant prompt makes the main agent lose track of the contract, and a tiny slice would throw away work. Each phase ends with a compile check and a test run, and only then does the next phase start. Phase 0 is done once and fixes the design, the contract and the order (layers and tags, configs, input). Later phases only add code.

Rules for every phase:
- Start a fresh main-agent conversation per phase if context gets long. CLAUDE.md, brief.md, GAME_DESIGN.md and PROGRESS.md carry everything the agent needs.
- The agent updates PROGRESS.md at the end of each phase (done, open bugs, next phase).
- Order inside a phase: gameplay-coder, compiler, scene-builder, vfx-artist if listed, compiler, playtester. Every phase that adds a system also adds its [SerializeField] and Install/Initialize lines to MainEntry, and scene-builder assigns the new field.
- If compiler or playtester reports a failure, send it back to the right agent and repeat, at most 3 rounds. After that, stop and write the blocker into PROGRESS.md.

## Phase 0: foundation
Output: GAME_DESIGN.md, TASKS.md, one asmdef per module (JSON text, references by name), ProjectBootstrap (layers, tags, default config assets, collision matrix), all config classes, all enums and interfaces from CLAUDE.md, stub classes for every system, and MainEntry that compiles.
Prompt:
```
Read CLAUDE.md and brief.md. Phase 0 only.
1. game-designer: write GAME_DESIGN.md (behaviour of each component, state transition table for humans, fire rules, test mode) and TASKS.md split into phases 1 to 5 with the files each task touches. Do not rename anything in the interface contract in CLAUDE.md; if you must change it, edit CLAUDE.md and say so.
2. gameplay-coder: create every asmdef from the module table in CLAUDE.md (Injection already exists, do not touch it), Assets/Editor/ProjectBootstrap.cs, all config ScriptableObject classes with the brief.md defaults, the Core enums and interfaces, stub classes for every class in the contract, and Main/MainEntry.cs following the example in CLAUDE.md.
3. compiler: confirm it compiles with no cyclic or missing assembly references, and that tags, layers and the assets in Assets/Settings/ now exist.
4. Write PROGRESS.md.
```
Gate: compiles, all asmdefs resolve by name, layers and tags exist, 6 config assets exist.

## Phase 1: helicopter and cup, plus test mode skeleton
Output: HeliController (smoothed input, drift, leveling), PlayerControlSource, ScriptedControlSource, RotorHazard, Cup chain behaviour (joints from CupConfig), TestModeController, scene with ground, heli and cup.
Prompt:
```
Read CLAUDE.md, GAME_DESIGN.md, TASKS.md, PROGRESS.md. Phase 1 only.
gameplay-coder: HeliController, PlayerControlSource (Input System, actions created in code), ScriptedControlSource, RotorHazard, Cup (joint chain and acceleration), TestModeController with ITestModeAware on heli and cup.
compiler. Then scene-builder: create Assets/Scenes/Main.unity with the MainEntry object (MainEntry component only) and Game/Environment/Ground, Game/Actors/Heli (body, rotors, tail rotor, cup chain, HumanAnchor, GrabZone) and a disabled TestHarness; assign every MainEntry field that exists so far. Then compiler. Then playtester: PlayMode tests for smooth acceleration (velocity does not jump), yaw drift, rotor crash, kinematic test mode switch.
Update PROGRESS.md.
```
Gate: heli flies smoothly in the Editor, rotor hit sets IsCrashed, test mode toggles without errors, MainEntry injects without KeyNotFoundException.

## Phase 2: pool, water, building, fire
Output: PoolZone, Cup.FillTick, WaterDrop, Cup.Tug dumping water, FireSide (growth, extinguish, pause, lost mode), RoofZone fire flag, GroundFireZone, scene objects for building, pool, ground fire.
Prompt:
```
Read CLAUDE.md, GAME_DESIGN.md, TASKS.md, PROGRESS.md. Phase 2 only.
gameplay-coder: PoolZone, WaterDrop, Cup water and Tug, FireSide, RoofZone, GroundFireZone (without human logic yet; leave a TODO that calls Human only through the contract).
compiler. scene-builder: Building (5 floors, 4 sides each with FireSide and a burn trigger), Roof with 4 RoofZones, Pool with PoolZone on the Water layer, GroundFire triggers. compiler. playtester: fill in pool, dump on a side extinguishes it for maxStopFireTime then fire restarts, partial water does not extinguish, a side at the top sets RoofOnFire.
Update PROGRESS.md.
```
Gate: all fire and water tests pass.

## Phase 3: humans
Output: Human (state machine, attach/detach, stun, strength), HumanAnimator (blank default), Cup.TryGrab rules, pool grab by mistake (state 4), roof and ground consequences, scene humans.
Prompt:
```
Read CLAUDE.md, GAME_DESIGN.md, TASKS.md, PROGRESS.md. Phase 3 only.
gameplay-coder: Human, HumanAnimator, HumanStrength logic, Cup.TryGrab/ReleaseAll, and the human branches in PoolZone, RoofZone, GroundFireZone.
compiler. scene-builder: add 4 humans on the roof (Human layer and tag, Rigidbody, capsule). compiler. playtester: empty cup grabs up to 2 humans, a full cup bounces and stuns, pool grab gives state 4, release over the pool gives state 3, release over the roof gives state 1, release over ground fire gives state 5, roof fire kills, strength drops above the acceleration threshold and the human falls at zero.
Update PROGRESS.md.
```
Gate: all five states reachable and tested.

## Phase 4: game rules and UI
Output: GameManager (lose, win, score; injected systems, no Register calls), WaterGauge, HumanStrengthBar, ResultPanel.
Prompt:
```
Read CLAUDE.md, GAME_DESIGN.md, TASKS.md, PROGRESS.md. Phase 4 only.
gameplay-coder: GameManager, WaterGauge, HumanStrengthBar, ResultPanel.
compiler. scene-builder: Canvas with the water gauge and result panel, world-space strength bar on each human, GameManager wiring. compiler. playtester: crash gives Lost, all humans dead, all sides burning and sides at lost mode; all humans resolved gives Won; score matches GameRulesConfig.
Update PROGRESS.md.
```
Gate: win and lose paths tested.

## Phase 5: effects and polish
Output: fire on sides, ground fire, water drop and splash, stars on stunned humans, smoke (optional), basic materials.
Prompt:
```
Read CLAUDE.md, GAME_DESIGN.md, TASKS.md, PROGRESS.md. Phase 5 only.
vfx-artist: effects listed in TASKS.md, exposing Play()/Stop() only. gameplay-coder: hook those calls from FireSide, WaterDrop, Human.Stun, Cup.Tug. compiler. scene-builder: assign effect prefabs. compiler. playtester: rerun the full PlayMode suite and report.
Update PROGRESS.md with a short tuning guide pointing at the ScriptableObjects.
```
Gate: full suite passes. Then you play it and tune the numbers in Assets/Settings/.
