# Flame and Blame: brief

Test task for a job application. Simple but complete. Unity 6000.0.65f1, URP, new Input System. See CLAUDE.md for the shared rules, layers, hierarchy and interface contract.

## Original spec
A firefighter game. Scene: a burning 5-floor square building and a swimming pool with water nearby.

The player flies a helicopter. A cup (water tank) hangs from the bottom of the helicopter on a chain of ConfigurableJoints. The cup fills with water in the pool, and the water is released over the building to extinguish burning parts. People wait for rescue on the roof.

The cup can pick up 1 or 2 humans at once when it collides with them, and only when it holds no water; they hang from the cup. If the cup is full of water, a human hit by it gets a funny collision effect and stars above the head.

All ground around the building is on fire, so the only place to drop rescued humans is the pool. While refilling, the player can accidentally pick up a human from the pool. If the player misses it, the human can end up back on the roof by a funny mistake. If the player notices, they can dump all the water and humans into the pool and refill. A water-level indicator is shown.

Humans have 5 states (the user adds animations later; code must support animation hooks with a blank default):
1. waiting for rescue on the roof
2. rescued, hanging from the edge of the cup
3. chilling in the pool
4. grabbed by mistake from the pool, swimming inside the cup
5. dead: fire touched the human, on the roof or on the burning ground

Fire moves procedurally from bottom to top on each of the four sides of the building separately. Water dropped on a side extinguishes it completely and stops it for "max stop fire time" seconds. One full cup is enough. If a side's fire reaches the top, the roof part of that side catches fire.

Main mechanic: align the hanging cup with the pool and with the humans. The player flies physically: WASD horizontal, Space up, Ctrl down. Movement must feel smooth and delayed, not instant. A human hanging from the cup has a strength indicator that drops when the helicopter moves too abruptly.

All gameplay variables live in ScriptableObjects. Interactions use physics: colliders, triggers, tags, layers, Rigidbodies. Keep it simple.

## Decisions (added after review)
- Human in state 2 or 4 is released into the pool with the F key: Tug releases the water and every human attached to the cup. Releasing over the pool puts the human in state 3 (rescued). Releasing over the roof puts the human back in state 1. Releasing over the ground fire kills the human.
- If the cup touches a human in the pool while the cup is filling, the human becomes state 4 and is attached to the cup.
- The helicopter continuously drifts in yaw (slow random torque). The player corrects it with Z (turn counterclockwise) and X (turn clockwise). Pitch and roll level themselves weakly. The tail rotor and main rotor are crash hazards, so orientation matters near the building.
- Lose: a rotor hits anything except the cup or humans. The helicopter falls, every human dies, every building side burns to the top and can no longer be extinguished.
- Win: no human is left in state 1, 2 or 4. All humans rescued gives the maximum score; some rescued gives a partial score. The final score is shown on a result panel.
- Extra defaults: the game also ends (Lost) if all four sides reach the top.
- Strength: when it reaches zero the human lets go and falls. Landing on the ground kills the human (all ground except the pool is on fire); landing in the pool makes it state 3; landing on the roof makes it state 1.
- A human on the roof is in danger only on a side whose roof is on fire.

## Decisions (Phase 3 and 4 additions)
- Fire is visible: each building side shows fire rising from the bottom (shader fill = side fire height). When a side reaches the top, the roof sector next to that side lights up separately.
- The water tank (the cube under the heli) uses the same shader in blue and visibly fills from the bottom while it hangs in the pool. The tank shading is the water indicator.
- A text hint in the top-right corner lists the controls.
- F drops the water as one physical ball. It spawns inside the tank, inherits the tank's velocity, and collides only with the building. A wall hit stops that side's fire for maxStopFireTime. A roof hit stops the side nearest to the hit. A ball that misses falls through everything else and disappears. The tank shows empty right after the drop.
- Humans come only after fire, filling and dropping work in real play.

## Decisions (Phase 4 movement)
- WASD moves the helicopter relative to the camera, not to the helicopter's facing: W moves along the camera's view direction (flattened to horizontal), S moves back, A and D move to the camera's left and right.
- Z turns the helicopter counterclockwise around its up axis, X turns it clockwise. Q and E are no longer used.

## Decisions (Phase 4 roof slabs)
- Each building side has its own roof slab: a rectangle on the roof, different sizes, no overlaps. When a side burns to the top, only its slab catches fire, and the fire covers that slab's top.
- Water dropped on a side or on its slab puts out both the side and the slab.
- Each slab has a box area on top where humans spawn at the start of the game.

## Decisions (Phase 5 humans)
- The user made the human animator (triggers Stunned, Hanging, ChillPool, WaitRescue, Dead) and the HumanPrefab. Humans are created at runtime with Instantiate; the prefab instance in the scene is only a preview and is disabled at start.
- Hanging and floating are adjustable by hand: the human prefab has a TankAttachPoint (where it holds the tank) and a FloatPoint (its water line). The tank has hang slots and a swim slot. The pool has a water surface marker and a drift area.
- Rescued humans in the pool float on the surface and drift around slowly, so the player has to dodge them when refilling.
- A human scooped into the tank swims inside it (state 4) and uses the ChillPool animation for now.

## Decisions (Phase 8)
- The water drop looks like clear blue water (transparent material).
- Water puts out fire in proportion to how full the tank was: a full tank removes all of a side's fire, 20% removes 20% of the current fire, and even 1% has some effect. The pause before the fire grows again is scaled the same way. Roof fire works the same way: it has its own fire level, and it only kills above a small threshold.
- The rotors show a fast-spin blur made by a shader. The real blades and their colliders do not move; the blur is only visual. On a crash the blur disappears and the real blades fall off with the debris.

## Architecture
Module-based systems, each folder with its own text-written asmdef (references by name), wired by dependency injection from a single `MainEntry` GameObject. Full rules, module table and contract are in CLAUDE.md.

## Default numbers (all live in ScriptableObjects; the user tunes them later)
HeliConfig: maxHorizontalSpeed 12, maxVerticalSpeed 6, inputRiseTime 0.6, inputFallTime 0.9, velocityResponse 1.5, maxAcceleration 6, maxYawRate 60 deg/s, yawDriftTorque 0.6, driftNoiseFrequency 0.25, levelingStrength 2, tiltVisualMaxDeg 12, linearDrag 0.3, angularDrag 1.5, rigidbodyMass 10.
CupConfig: capacity 100, fillRatePerSecond 25, emptyThreshold 0.05, fullThreshold 0.95, maxHumans 2, segmentCount 3, segmentLength 2, jointAngularLimitDeg 40, jointSpring 50, jointDamper 8, grabZoneRadius 1.2, dumpDropRadius 1.5, bumpImpulse 4.
HumanConfig: maxStrength 100, accelerationThreshold 4, strengthLossFactor 6, strengthRecoveryPerSecond 3, stunDuration 2.5, animatorTriggers per state (WaitingOnRoof, RescuedHanging, ChillingInPool, SwimmingInTank, Dead), humanCount 4, mass 60, hangJointLinearLimit 0.3, hangJointSpring 200, hangJointDamper 20, hangJointAngularLimitDeg 60, floatHeight 0.0, bobAmplitude 0.1, bobFrequency 0.5, driftSpeed 0.8, driftRetargetTime 4, driftSeparation 1.2, standUpSpeed 4.
FireConfig: spreadSecondsPerFloor 12, maxStopFireTime 20, extinguishMinWater01 0.9 (unused since Phase 8), roofFireKillThreshold 0.1, groundFirePermanent true, lostSpreadSecondsPerFloor 3.
BuildingConfig: floors 5, floorHeight 3.5, footprint 12 x 12, roofHumanSpawnMargin 1.5.
PoolConfig (inside GameRulesConfig): poolSize 10 x 2.5 x 6, distanceFromBuilding 14.
FireConfig visuals: wallFireSmoothTime 0.4, roofIgniteTime 1.5, roofFireHeight 1.5.
CupConfig water ball and visuals: waterBallRadius 0.8, waterBallMass 20, waterBallLifetime 6, waterBallKillY -5, fillVisualSmoothTime 0.3.
UIConfig: controlsHintText "WASD move (camera) | Space/Ctrl up/down | Z/X turn | F drop water | Mouse look | Wheel zoom", hintFontSize 22, hintMargin 20.
GameRulesConfig: scorePerRescued 100, penaltyPerDeath 25, bonusPerSideNotOnFire 25, endOnAllSidesBurning true.
HeliConfig rotor blur: mainRotorTurnsPerSecond 6, tailRotorTurnsPerSecond 12, rotorIdleFactor 0.8, rotorLoadFactor 1.2.
