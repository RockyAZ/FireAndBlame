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
- The helicopter continuously drifts in yaw (slow random torque). The player corrects it with Q and E. Pitch and roll level themselves weakly. The tail rotor and main rotor are crash hazards, so orientation matters near the building.
- Lose: a rotor hits anything except the cup or humans. The helicopter falls, every human dies, every building side burns to the top and can no longer be extinguished.
- Win: no human is left in state 1, 2 or 4. All humans rescued gives the maximum score; some rescued gives a partial score. The final score is shown on a result panel.
- Extra defaults: the game also ends (Lost) if all four sides reach the top.
- Strength: when it reaches zero the human lets go and falls. Landing on the ground kills the human (all ground except the pool is on fire); landing in the pool makes it state 3; landing on the roof makes it state 1.
- A human on the roof is in danger only on a side whose roof is on fire.

## Architecture
Module-based systems, each folder with its own text-written asmdef (references by name), wired by dependency injection from a single `MainEntry` GameObject. Full rules, module table and contract are in CLAUDE.md.

## Default numbers (all live in ScriptableObjects; the user tunes them later)
HeliConfig: maxHorizontalSpeed 12, maxVerticalSpeed 6, inputRiseTime 0.6, inputFallTime 0.9, velocityResponse 1.5, maxAcceleration 6, maxYawRate 60 deg/s, yawDriftTorque 0.6, driftNoiseFrequency 0.25, levelingStrength 2, tiltVisualMaxDeg 12, linearDrag 0.3, angularDrag 1.5, rigidbodyMass 10.
CupConfig: capacity 100, fillRatePerSecond 25, emptyThreshold 0.05, fullThreshold 0.95, maxHumans 2, segmentCount 3, segmentLength 2, jointAngularLimitDeg 40, jointSpring 50, jointDamper 8, grabZoneRadius 1.2, dumpDropRadius 1.5, bumpImpulse 4.
HumanConfig: maxStrength 100, accelerationThreshold 4, strengthLossFactor 6, strengthRecoveryPerSecond 3, stunDuration 2.5, animatorTriggers per state (WaitingOnRoof, RescuedHanging, ChillingInPool, SwimmingInTank, Dead), humanCount 4.
FireConfig: spreadSecondsPerFloor 12, maxStopFireTime 20, extinguishMinWater01 0.9, groundFirePermanent true, lostSpreadSecondsPerFloor 3.
BuildingConfig: floors 5, floorHeight 3.5, footprint 12 x 12, roofHumanSpawnMargin 1.5.
PoolConfig (inside GameRulesConfig): poolSize 10 x 2.5 x 6, distanceFromBuilding 14.
GameRulesConfig: scorePerRescued 100, penaltyPerDeath 25, bonusPerSideNotOnFire 25, endOnAllSidesBurning true.
