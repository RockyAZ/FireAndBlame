# Flame and Blame: phase plan

Build the full brief in eight gated phases (0 to 7). Each phase ends with a compile check and a play-test. The next phase starts only after its gate passes.

## How every phase runs
- Start a fresh main-agent session per phase. The files carry the context: CLAUDE.md, brief.md, GAME_DESIGN.md, TASKS.md, PROGRESS.md.
- Run the steps in order. Each step names one agent. Do not combine steps.
- If a compiler or playtester step fails, send the error to the agent that owns the file, then rerun that check. Stop after 3 rounds and write the blocker into PROGRESS.md.
- Any phase that adds a system also adds its `[SerializeField]`, `Install` and `Initialize` lines to MainEntry. The scene-builder then assigns the field.
- At the end of each phase, the main agent updates PROGRESS.md with what is done, what is open, and what the next phase starts with.

---

## Phases 0 to 2: DONE
Foundation, helicopter with cup and camera, and the pool, water and fire logic are done. See PROGRESS.md.

---

## Phase 3: fire and water visuals, pool filling, controls hint

**Output:** `FillEffect` shader with `M_Fire` and `M_Water` materials, `FillEffectView`, `RoofSectorMesh`, a fire overlay on each wall that rises with the side's fire height, four separate roof fire sectors, the tank cube filling blue in the pool, a blue pool surface, `UIConfig` and a controls hint in the top-right corner.

**Gate:** in real Play mode (not test mode), the walls visibly burn upward and light their roof sector at the top, flying the tank into the pool fills it and its blue level rises, and the hint is visible. All earlier tests still pass.

**Prompt:**
```
Read CLAUDE.md (including "Contract additions (Phase 3 ...)"), brief.md, GAME_DESIGN.md, TASKS.md and PROGRESS.md. Phase 3 only. Use the subagents. Minimize usage of taking screenshots.

Step 1, game-designer: add a "Visuals and controls hint" section to GAME_DESIGN.md and a "Fire and water ball" section for Phase 4. Renumber TASKS.md: old Phase 3 becomes 5, old 4 becomes 6, old 5 becomes 7. Add task lists for the new Phases 3 and 4 from PHASES.md. Do not change the contract.

Step 2, vfx-artist: write Assets/Shaders/FillEffect.shader as specified in CLAUDE.md, then create M_Fire and M_Water in Assets/VFX/. Write FillEffectView and RoofSectorMesh in the VFX module.

Step 3, gameplay-coder: add UIConfig and its defaults to ProjectBootstrap, write ControlsHintView, connect FireSide and RoofZone to their FillEffectViews and Cup to its waterView, add the new fields to the configs and MainEntry.

Step 4, compiler: check that the project compiles and that UIConfig.asset exists.

Step 5, scene-builder: add the FireOverlay child to each side, the four RoofFire sectors on the roof, M_Water and FillEffectView on the tank cube, M_Water on the pool surface, and a Canvas with the controls hint in the top-right corner. Assign every new field and check that none is null.

Step 6, compiler: check that the project compiles.

Step 7, playtester: with test mode OFF and real physics, write and run tests showing that:
- each FireOverlay's _Fill follows its side's Height01 as the fire grows,
- a side at the top raises only its own roof sector's _Fill toward 1,
- flying the tank into the pool with ScriptedControlSource raises Water01 and the tank view's _Fill,
- the controls hint text equals UIConfig.controlsHintText.
Read shader values with Renderer.GetPropertyBlock. Rerun all earlier tests.

Step 8, main agent: update PROGRESS.md, and tell me what to check by eye in Play mode.
```

---

## Phase 4: roof slabs and camera-relative movement

The water ball drop already works (done during Phase 3). This phase reworks the roof and changes heli movement to follow the camera, with Z and X for turning.

**Current scene (made by you, keep it):** the four sides are instances of one `Building_Side` prefab. Under `Roof` there are four `RoofSlab` boxes of different sizes that do not overlap, one per side. Each RoofSlab has a `RoofFire` child that still carries `RoofSectorMesh`.

**Output:** a `RoofSlab` component on each RoofSlab object that owns the roof logic for its side. `RoofSectorMesh` is deleted. `RoofFire` is a plain fire renderer on top of its slab. Each slab has a `HumanSpawnArea` box for Phase 5. `RoofZone` is replaced by `RoofSlab`. A water ball hitting a slab puts out that slab's side. WASD moves the heli relative to the camera view, Z and X turn it, and the hint shows the new keys.

**Gate:** in real Play mode, when a side burns to the top, only its own slab catches fire, and the fire covers that slab's top exactly. Dropping water on the side or on its slab puts both out. Each slab shows a HumanSpawnArea gizmo inside its top face. W flies toward where the camera looks whatever way the heli faces, A and D slide to the camera's left and right, Z turns counterclockwise and X clockwise. All earlier tests still pass.

**Prompt:**
```
Read CLAUDE.md (including "Contract additions (Phase 4: roof slabs)" and "Contract additions (Phase 4: camera-relative movement and Z/X turning)"), brief.md, GAME_DESIGN.md, TASKS.md and PROGRESS.md. Phase 4 only. Use the subagents. Do not take screenshots.
The user built the roof by hand: keep the existing Building_Side prefab instances and the four RoofSlab objects and their sizes. Do not rebuild them.

Step 1, game-designer: replace the Phase 4 section of GAME_DESIGN.md and TASKS.md with this roof slab rework and the camera-relative movement. Update the helicopter feel section: movement is camera-relative, Z/X turn. In section 10, note that the ball drop is already implemented. Do not change the contract beyond what CLAUDE.md already says.

Step 2, gameplay-coder: write RoofSlab in the Fire module as specified in CLAUDE.md. Add serialized RoofSlab[] to FireSystem, initialize the slabs there, and expose RoofSlabs. Delete RoofSectorMesh and RoofZone, and move any logic they had into RoofSlab. Update WaterDrop so a hit on a RoofSlab extinguishes that slab's FireSide. Add roofFireHeight to FireConfig and its default to ProjectBootstrap. Update every test that used RoofZone or RoofSectorMesh.
Also: add MoveWorld to HeliCommand, compute it in PlayerControlSource from cameraTransform, make HeliController move along MoveWorld, replace Q/E with Z (Yaw -1) and X (Yaw +1), update ScriptedControlSource, change the UIConfig default hint text in ProjectBootstrap, and update the existing UIConfig.asset value too (ProjectBootstrap only creates missing assets). Update heli tests that relied on yaw-relative movement.

Step 3, compiler: check that the project compiles and no reference to RoofZone or RoofSectorMesh is left.

Step 4, scene-builder: on each of the four RoofSlab objects:
- add the RoofSlab component and assign its FireSide (the Building_Side instance on the same side) and its RoofFire FillEffectView,
- remove the missing-script RoofSectorMesh from the RoofFire child and make RoofFire a plain box with M_Fire and FillEffectView, no collider,
- add a child HumanSpawnArea (empty transform) and assign it,
- make sure the slab has a non-trigger BoxCollider on layer Building with tag Roof.
Then remove the old RoofZone objects if any remain, assign FireSystem.roofSlabs, assign the Main Camera to PlayerControlSource.cameraTransform, and read every new field back to confirm none is null.

Step 5, compiler: check that the project compiles.

Step 6, playtester: with test mode OFF, write and run tests showing that:
- each slab's RoofFire box covers its slab top (its X and Z bounds match the slab's top face within 0.01 m),
- a side at the top ignites only its own slab, and the other slabs stay at _Fill 0,
- extinguishing a side clears its slab fire,
- a water ball dropped onto a slab extinguishes that slab's side,
- each HumanSpawnArea lies inside its slab's top face, inset by roofHumanSpawnMargin, and GetSpawnPoint returns points inside it,
- an IKillable inside a burning slab's area is killed,
- with the camera yawed 90 degrees from the heli, W moves the heli along the flattened camera forward (angle under 5 degrees), and D along the camera right,
- turning the heli with Yaw does not change the move direction for the same camera,
- Z decreases the heli's yaw angle (counterclockwise from above) and X increases it,
- the hint text shows Z/X and no longer Q/E.
Rerun all earlier tests.

Step 7, main agent: update PROGRESS.md, and tell me what to check by eye in Play mode before humans start.
```

---

## Phase 5: humans

**Your assets (keep them):**
- Animator controller `Assets/Models/Human/Animations/HumanAnimator.controller` with 5 triggers from Any State: `Stunned`, `Hanging`, `ChillPool`, `WaitRescue`, `Dead`.
- Prefab `Assets/Prefabs/HumanPrefab.prefab` (a variant of the human model) with the Animator already set. It has a collider on its `Hips` child.
- One instance of the prefab in Main.unity, used as an editing preview.

**Output:** Human with its state machine and physics per state, attach points you can adjust by hand, HumanAnimator driving your triggers, Cup grab and release rules, strength, roof, pool and ground consequences, pool floating and drifting, and HumansSystem spawning humans from the prefab on the RoofSlab spawn areas at runtime.

**Gate:** in real Play mode, humans appear on the roof slabs, hang from the tank when grabbed with an empty tank, get stunned by a full tank, fall when released or when strength runs out, float and drift in the pool, and can be scooped into the tank by mistake. All five states are reachable and tested, and all earlier tests still pass.

**Prompt:**
```
Read CLAUDE.md (including "Contract additions (Phase 5: humans)"), brief.md, GAME_DESIGN.md, TASKS.md and PROGRESS.md. Phase 5 only. Use the subagents. Minimize screenshots.
The user made the animator controller and HumanPrefab by hand. Keep them; only add components and child objects to the prefab.

Step 1, game-designer: rewrite the Phase 5 parts of GAME_DESIGN.md and TASKS.md from "Contract additions (Phase 5: humans)": physics mode per state, attach points, pool floating and drifting, animator trigger mapping, spawning. Do not change the contract.

Step 2, gameplay-coder: write Human, HumanAnimator, HumansSystem, the HumanConfig fields, Cup.TryGrab, Cup.ReleaseAll, the Cup hang and swim slots, the human branches in Cup.Tug, PoolZone, GroundFireZone and the roof landing, and the PoolZone surface and drift area. Spawn humans with Instantiate from the prefab asset. Add HumansSystem to MainEntry if it is not already initialized there.

Step 3, compiler: check that the project compiles.

Step 4, scene-builder: edit Assets/Prefabs/HumanPrefab.prefab:
- on the root, add Human, HumanAnimator (Animator assigned), a dynamic Rigidbody and a CapsuleCollider sized to the body; set layer Human and tag Human on the root and all children,
- remove the Hips collider or disable it (the root capsule replaces it); make sure no child has a Rigidbody,
- add the children TankAttachPoint (at the hands, above the head) and FloatPoint (at chest height) and assign them.
On the Cup bowl, add HangSlot_1, HangSlot_2 (on the rim, opposite sides) and SwimSlot (inside, near the top) and assign them. On the Pool, add the WaterSurface and DriftArea children and assign them to PoolZone. Assign the prefab asset to HumansSystem.humanPrefab and the scene instance to HumansSystem.scenePreview. Read every new field back to confirm none is null.

Step 5, compiler: check that the project compiles.

Step 6, playtester: with test mode OFF and real physics where possible, write and run tests showing that:
- HumansSystem spawns humanCount humans inside the RoofSlab spawn areas, and the scene preview is disabled,
- an empty tank touching a WaitingOnRoof human attaches it at a HangSlot (RescuedHanging), and a third human is rejected,
- a tank with water bounces a human and stuns it, and the Stunned trigger fires, then the human returns to its state,
- a tank in the pool touching a ChillingInPool human puts it in SwimmingInTank at SwimSlot,
- Tug releases every attached human, they inherit the tank velocity, and they land as: pool gives ChillingInPool, roof slab gives WaitingOnRoof, ground fire gives Dead,
- hanging strength drops while tank acceleration is above the threshold and recovers below it, and at zero the human falls,
- a pool human stays with its FloatPoint at the water surface plus floatHeight, moves horizontally, and stays inside DriftArea,
- a human on a burning slab dies,
- each state change fires the matching animator trigger.
Rerun all earlier tests.

Step 7, main agent: update PROGRESS.md, and tell me what to check by eye and which Transforms to move to adjust hanging and floating.
```

---

## Phase 6: game rules and UI

**Output:** GameManager (win, lose, score), HumanStrengthBar and ResultPanel.

**Gate:** win and lose paths are tested.

**Prompt:**
```
Read CLAUDE.md, GAME_DESIGN.md, TASKS.md and PROGRESS.md. Phase 6 only. Use the subagents.

Step 1, gameplay-coder: Change heli fly - it auto-aligns with camera view so the face of heli is looking along with camera look vector, heli makes rotation to align with camera only around up axis.  write GameManager, HumanStrengthBar and ResultPanel. WaterGauge: the tank shading is the water indicator and near it there is also a 0-100% world-space percentage indication. Win loose condition. Win score (depends on how many humans survived). Separate indicator showing how fast cup is moving so player understands why humans looses strength.

Step 2, compiler: check that the project compiles.

Step 3, scene-builder: build the result panel on the existing Canvas, add a world-space strength bar to each human, and wire GameManager. Assign the fields. WaterGauge: the tank shading is the water indicator and near it there is also a 0-100% world-space percentage indication. Separate indicator showing how fast cup is moving so player understands why humans looses strength.

Step 4, compiler: check that the project compiles.

Step 5, playtester: write and run tests showing that:
- a heli crash gives Lost, kills every human and sets every side to lost mode,
- all humans resolved gives Won,
- the score matches GameRulesConfig.

Step 6, main agent: update PROGRESS.md.
```

---

## Phase 7: effects and polish

**Output:** ground fire particles, splash when the water ball hits, stars over stunned humans, optional smoke and flame particles on top of the shader fire.

**Gate:** the full PlayMode suite passes. Then you play the game and tune the numbers in Assets/Settings/.

**Prompt:**
```
Read CLAUDE.md, GAME_DESIGN.md, TASKS.md and PROGRESS.md. Phase 7 only. Use the subagents.

Step 0, gameplay-coder: Fix heli crash - it doesn't work. When rotor collides with building nothing happens. I want on crash loosing screen and heli breaks by parts and falls on the ground. Player looses and have to restart level. 

Step 0.1, compiler: check that the project compiles and no error appears.

Step 0.2, gameplay-coder: Change calculation method for cup shake, make it less rapid and more safe. SOmethimes it shows red and max shake when it isn't shaking much and when human is picked up it may imidiately waste all his strength and fall because cup is too shaky, it needs to be fixed. 

Step 0.3, compiler: check that the project compiles and no error appears.

Step 0.4, vfx-artist: create shader with toon like lighting model that slightly highlights mesh wireframe of rendered models, adds outline for models. Don't use geometry shaders.  In Building_Side prefab add child cubes without colliders that will look like windows. Create different materials for windows, building, roof, helicopter etc.

Step 1, vfx-artist: create the effects listed in TASKS.md. Each one exposes Play() and Stop() only.

Step 2, gameplay-coder: call those methods from FireSide, GroundFireZone, WaterDrop.Hit, Human.Stun and Cup.Tug.

Step 3, compiler: check that the project compiles.

Step 4, scene-builder: assign the effect prefabs.

Step 5, compiler: check that the project compiles.

Step 6, playtester: run the full PlayMode suite and report the results.

Step 7, main agent: update PROGRESS.md with a short tuning guide that points to the ScriptableObjects.
```

---

## Phase 8: water drop look, proportional extinguishing, rotor blur

**Output:** a transparent blue material on the water drop, fire put out in proportion to how full the tank was, and a rotor blur shader that makes the main and tail rotors look like they spin fast while their colliders stay still.

**Gate:** in real Play mode, the drop looks like clear blue water, a 20% tank removes about 20% of a side's fire and a full tank removes all of it, any water at all has a visible effect, the rotors look like fast-spinning blurred blades, and the blades still crash the heli on contact. All earlier tests still pass.

**Prompt:**
```
Read CLAUDE.md (including "Contract additions (Phase 8)"), brief.md, GAME_DESIGN.md, TASKS.md and PROGRESS.md. Phase 8 only. Use the subagents. Minimize screenshots.

Step 1, game-designer: add a Phase 8 section to GAME_DESIGN.md and TASKS.md from "Contract additions (Phase 8)": proportional extinguishing (side and roof), the drop material and the rotor blur. Do not change the contract.

Step 2, vfx-artist: create Assets/Materials/M_WaterDrop.mat (transparent blue water look as in CLAUDE.md) and assign it to the WaterDrop prefab renderer. Keep the existing trail. Then write Assets/Shaders/RotorBlur.shader, create M_RotorBlur_Main and M_RotorBlur_Tail, and write RotorBlurView in the VFX module.

Step 3, gameplay-coder: make FireSide.Extinguish and the roof fire proportional to the water amount as specified, scale the pause by the amount, stop using extinguishMinWater01, and make sure WaterDrop and Cup.Tug pass the tank's Water01 at the moment of the drop. Add the Heli to VFX asmdef reference, add the rotor blur fields to HeliConfig and HeliController, and hide or show the blade renderers as specified. Update every test that relied on the old minimum-water rule.

Step 4, compiler: check that the project compiles.

Step 5, scene-builder: add a RotorBlur disc child to MainRotor and to TailRotor (no collider, the matching material, RotorBlurView), sized to the blade span and lying in the plane the blades turn in. Assign them to HeliController. Do not move, resize or remove the rotor colliders. Read the new fields back to confirm none is null.

Step 6, compiler: check that the project compiles.

Step 7, playtester: with test mode OFF where possible, write and run tests showing that:
- the WaterDrop prefab renderer uses M_WaterDrop and its material is transparent,
- a drop with amount 1 sets a side's Height01 to 0; with 0.2 it removes 20% of the side's current fire (within 0.01); with 0.01 it still lowers Height01,
- the pause equals maxStopFireTime times the amount,
- a burning roof slab's fire level drops by the same share, and the slab stops killing only below roofFireKillThreshold,
- a lost side still ignores water,
- the rotor blur views spin while the heli flies, the blade mesh renderers are hidden, and the rotor colliders have not moved,
- after a crash the blur discs are hidden and the blade renderers are shown again on the debris,
- a rotor hit still crashes the heli.
Rerun all earlier tests.

Step 8, main agent: update PROGRESS.md with what to check by eye and which material and HeliConfig values tune the drop look and the rotor blur.
```

