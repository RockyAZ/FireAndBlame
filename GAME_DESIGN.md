# Flame and Blame: game design

Read with CLAUDE.md (rules, contract) and brief.md (spec, defaults). Numbers are config field names (ScriptableObject); defaults in section 7. No gameplay number is hardcoded.

Conventions
- Physics callbacks only forward to public methods (`ReportEnter/ReportStay/ReportExit/ReportHit`), logic lives in the public methods.
- "Unattached human" = a Human with `Cup` link null (not hanging).
- Water01 and amount01 are fractions of `CupConfig.capacity` (0..1).
- Initialize() runs only from MainEntry. Components with their own Awake/OnEnable do no injected work there.

## 1. Components

### PlayerControlSource (Heli)
Creates InputActions in code in Awake, enables in OnEnable, disposes in OnDestroy. `Read()` returns Move (D-A, W-S), Vertical (Space +1, LeftCtrl -1), Yaw (Q -1, E +1), TugPressed (F, true only in the frame it was pressed). No Initialize.

### HeliController (Heli)
- Reads `IControlSource.Read()` in FixedUpdate and applies the feel model of section 5. Ignores TugPressed (Cup reads it).
- Rigidbody: gravity OFF (it hovers), mass `rigidbodyMass`, linearDamping `linearDrag`, angularDamping `angularDrag`. Initialize applies these.
- `Crash()`: idempotent. Sets IsCrashed, command becomes zero, gravity ON, linearDamping lowered to 0, fires `Crashed`. In test mode (kinematic) only the flag and event happen.
- `SetControlSource` swaps the source. Exposes Body, IsCrashed, Crashed.

### RotorHazard (Heli)
On MainRotor and TailRotor (layer Rotors, tag Rotor), non-trigger collider. `OnCollisionEnter` and `OnTriggerEnter` forward to `ReportHit(Collider other)`. ReportHit ignores: `other.isTrigger`, tags Cup, Human, Heli, WaterDrop. Anything else calls `HeliController.Crash()` (found by GetComponentInParent). The matrix already limits Rotors to Building, Ground, Obstacle; the filter is a second guard and the test entry point.

### Cup (Rescue)
- Initialize(): builds the chain from `CupConfig`: `segmentCount` segments of length `segmentLength` as Rigidbody+ConfigurableJoint (linear locked, angular limit `jointAngularLimitDeg`, spring `jointSpring`, damper `jointDamper`), top joint connected to the serialized `anchorBody` (heli Rigidbody), last segment carries the bowl. `Body` is the bowl Rigidbody (mass `bowlMass`, segments `segmentMass`). Sets GrabZone SphereCollider radius to `grabZoneRadius`.
- Water: `Water01`, `IsEmpty` (Water01 <= emptyThreshold), `IsFull` (Water01 >= fullThreshold), `WaterChanged(Water01)` fired on every change.
- `FillTick(dt)`: Water01 += fillRatePerSecond / capacity * dt, clamp 1. Called by PoolZone each physics step while the bowl is inside. Works whether or not humans are attached.
- `Acceleration`: bowl velocity derived from position delta each FixedUpdate (works kinematic and dynamic), differentiated and low-passed by `accelerationSmoothing`. Humans read it for strength.
- `InPool` / `SetInPool(bool)`: set by PoolZone enter and exit of the bowl.
- `TryGrab(Human h)` rules, first match wins:
  1. h dead, already attached, `IsStunned`, `AttachedCount >= maxHumans`, or grab lock active (`regrabCooldown` after a Tug) -> Rejected.
  2. h.State == ChillingInPool and `InPool` and not `IsFull` -> `h.AttachTo(this)` (becomes SwimmingInTank) -> Grabbed. (The accidental pool grab, also for an empty cup.)
  3. `IsFull` -> push `h.Body` away from the bowl with impulse `bumpImpulse` (plus `bumpUpBias` upward), `h.Stun()` -> BouncedFull.
  4. h.State == WaitingOnRoof and `IsEmpty` -> `h.AttachTo(this)` (becomes RescuedHanging) -> Grabbed.
  5. otherwise (partial water, other states) -> Rejected.
- GrabZone (trigger, tag CupGrabZone) `OnTriggerEnter/Stay` and the bowl `OnCollisionEnter` forward to TryGrab for any collider tagged Human.
- `Tug()`: if not `IsEmpty`: instantiate `dropPrefab` at the bowl, scale to diameter `2*dumpDropRadius`, `Launch(Water01, Body.linearVelocity)`, `Destroy(drop, dropLifetime)`, Water01 = 0. Then `ReleaseAll()`. Tug with an empty cup only releases. Update reads `IControlSource.TugPressed` and calls Tug (skipped in test mode).
- `ReleaseAll()`: every attached human `Detach()`, list cleared, grab lock for `regrabCooldown`. Humans keep their state while falling; the landing zone decides (section 3).
- Human slots: attached humans are placed at `HumanAnchor` offset by `humanSlotSpacing` (slot 0 left, 1 right).

### WaterDrop (Rescue)
Prefab: Rigidbody (gravity), SphereCollider trigger, layer Water, tag WaterDrop. `Launch(amount01, v)` stores amount, sets velocity = v. `OnTriggerEnter` forwards to `ReportHit(Collider)`: if other has a FireSide (parent of the water catch trigger) -> `FireSide.Extinguish(amount01)` then destroy; if other is GroundFireZone and `groundFirePermanent` false -> `GroundFireZone.Extinguish(amount01)`; on Ground, Building, Obstacle, PoolZone -> destroy (splash VFX in Phase 5). Lifetime is capped by the Cup (`dropLifetime`).

### Human (Rescue)
- Initialize(HumanConfig): Strength01 = 1, State = WaitingOnRoof (via SetState, no event noise at start), poolDrag stored.
- `SetState(s)`: the only writer of State. No-op if same state or State == Dead. Applies state side effects (below), calls `HumanAnimator.Apply`, raises `StateChanged`.
- `AttachTo(cup)`: requires unattached, not dead. Unparent-free in dynamic mode: ConfigurableJoint to `cup.Body`, anchored at the cup slot; `Physics.IgnoreCollision` between human and cup colliders while attached; Strength01 = 1; state becomes RescuedHanging (from WaitingOnRoof) or SwimmingInTank (from ChillingInPool). Test mode: parent to `cup.HumanAnchor` slot, kinematic.
- `Detach()`: removes the joint (or unparents), restores collisions, non-kinematic, small downward speed `detachDownSpeed`, clears the cup link. State is not changed.
- `Kill()` (IKillable): idempotent. Detach if attached, `SetState(Dead)`. Dead humans stay where they lie.
- `Stun()`: IsStunned = true for `stunDuration` (timer via coroutine or Invoke, no polling), stars VFX later. No state change.
- `TickStrength(dt, accel)`: only while attached. If accel > `accelerationThreshold`: strength -= (accel - threshold) * `strengthLossFactor` * dt; else strength += `strengthRecoveryPerSecond` * dt. Clamp 0..`maxStrength`. Fires `StrengthChanged(Strength01)` on change. FixedUpdate calls it with `cup.Acceleration.magnitude`. At 0: `cup.ReleaseOne` equivalent = `Detach()` plus removal from the cup list (Human calls the cup's internal remove; the human stays in the same state while falling).
- Zone callbacks (trigger enters on the human's side are not used; zones forward to Human methods).
- State side effects: ChillingInPool: gravity off, velocity zero, linearDamping `poolDrag`. WaitingOnRoof and others: gravity on, damping normal. Dead: collider stays, no zone reaction.

### HumanAnimator (Rescue)
`Apply(HumanState)`: if an Animator component exists on the human, `SetTrigger(HumanConfig.trigger<State>)`; otherwise nothing. No required controller.

### HumansSystem (Rescue)
Serialized `List<Human>`. Initialize(): warns if count != `humanCount`, calls `Initialize(config)` on every human, subscribes to each `StateChanged` and re-raises `AnyStateChanged`. `CountIn(s)`, `KillAll()` (Kill on every non-dead human).

### PoolZone (Rescue)
Trigger, layer Water, tag Pool, size from `PoolConfig.poolSize`.
- `ReportEnter/Stay/Exit(Collider)`:
  - tag Cup (bowl): Enter -> `cup.SetInPool(true)`; Stay -> `cup.FillTick(Time.fixedDeltaTime)`; Exit -> `SetInPool(false)`.
  - tag Human, unattached and not Dead: Enter -> `SetState(ChillingInPool)`.
- Accidental grab is not here: the cup's GrabZone overlaps the pool human and Cup.TryGrab rule 2 handles it.

### RoofZone (Rescue)
Trigger per roof side, serialized `FireSide` for that side, no Initialize. Tracks humans inside in a set (Enter adds, Exit removes).
- Human Enter, unattached, not Dead: `SetState(WaitingOnRoof)`, then if `FireSide.RoofOnFire` -> `Kill()`.
- Subscribes to `FireSide.ReachedTop` (in OnEnable, unsubscribes in OnDisable; FireSide is a plain serialized ref, not injected): kills every human in the set that is unattached.
- Attached (hanging) humans are never affected by the roof.

### FireSide (Fire)
See section 4. Two child triggers: burn band (layer FireZone, tag BuildingSide) forwards enter/stay to `ReportEnter/Stay`: any `IKillable` -> `Kill()` (all humans, attached or not; roof humans are above the band). Water catch trigger (full height, layer FireZone) is what WaterDrop hits. Initialize(FireConfig) then `ApplyBuilding(BuildingConfig)` (total height = floors * floorHeight; band depth `burnTriggerDepth`).

### FireSystem (Fire)
Serialized FireSide[4] and GroundFireZone[]. [Inject] FireConfig, BuildingConfig. Initialize(): initializes sides and ground zones, subscribes to each `ReachedTop`. `SidesNotOnFire` = sides with IsBurning false (extinguished and paused). `SetAllLost()` calls SetLost on all. `AllSidesBurning` raised once when all four sides have Height01 == 1.

### GroundFireZone (Fire)
Trigger, layer FireZone, tag GroundFire. Enter/Stay -> any IKillable -> `Kill()` (a human hanging low over it dies too). If `groundFirePermanent` is false, `Extinguish(amount01)` (needs `extinguishMinWater01`) disables the collider for `maxStopFireTime`, then re-enables. A slab under the ground also catches humans that fall off the map.

### GameManager (GameRules)
Initialize(): subscribes to `heli.Crashed`, `fire.AllSidesBurning`, `humans.AnyStateChanged`. Result starts Playing; terminal once changed.
- Heli crash: snapshot rescued count, `Result = Lost`, `humans.KillAll()`, `fire.SetAllLost()`, then `ResultChanged`.
- AllSidesBurning and `endOnAllSidesBurning`: `Lost` (no KillAll).
- On each state change: pending = CountIn(WaitingOnRoof)+CountIn(RescuedHanging)+CountIn(SwimmingInTank). If pending == 0: Won if CountIn(ChillingInPool) >= 1, else Lost.
- Score at end: `rescued * scorePerRescued - deaths * penaltyPerDeath`, plus `fire.SidesNotOnFire * bonusPerSideNotOnFire` only on Won, minimum 0. rescued = CountIn(ChillingInPool) (snapshot taken before KillAll on a crash), deaths = humans that were Dead before the end.

### UI (UI)
Read events only, no polling.
- `HudController`: [Inject] Cup, HumansSystem, GameManager. Initialize(): hands WaterGauge to `cup.WaterChanged`, pairs serialized `HumanStrengthBar[]` with `humans.Humans` by index, passes `ResultChanged` to ResultPanel.
- `WaterGauge`: Image fill = Water01, optional "FULL" tint at `IsFull`.
- `HumanStrengthBar`: world-space, follows its human (parented under it), visible only while State is RescuedHanging or SwimmingInTank, updated from `Human.StrengthChanged` and `StateChanged`.
- `ResultPanel`: hidden at start; on Won/Lost shows result, score, rescued/total, and a Restart button (reloads the active scene).

### Testing (Testing)
See section 6.

## 2. Event and call flow (summary)
heli.Crashed -> GameManager. Cup.WaterChanged -> WaterGauge. Human.StateChanged -> HumansSystem.AnyStateChanged -> GameManager, HumanStrengthBar. FireSide.ReachedTop -> FireSystem, RoofZone. FireSystem.AllSidesBurning -> GameManager. Nothing in a lower module holds a reference to a higher one.

## 3. Human state transition table
States: S1 WaitingOnRoof, S2 RescuedHanging, S3 ChillingInPool, S4 SwimmingInTank, S5 Dead. "Landing" = unattached human entering a zone. Stun never changes state.

| From | Event | To | Side effects | Method that does it |
|---|---|---|---|---|
| S1 | Cup touch, cup empty | S2 | attach to slot, strength 1 | `Cup.TryGrab` -> `Human.AttachTo` -> `SetState` |
| S1 | Cup touch, cup partial (not empty, not full) | S1 | Rejected | `Cup.TryGrab` |
| S1 | Cup touch, cup full | S1 | impulse `bumpImpulse`, stun `stunDuration` | `Cup.TryGrab`, `Human.Stun` |
| S1 | Cup touch, 2 already attached or grab lock | S1 | Rejected | `Cup.TryGrab` |
| S2 | Cup touch (any) | S2 | Rejected (already attached) | `Cup.TryGrab` |
| S2 | Tug | S2 (falling) | detach, falls; landing decides | `Cup.Tug` -> `ReleaseAll` -> `Human.Detach` |
| S2 | Strength reaches 0 | S2 (falling) | detach, falls | `Human.TickStrength` -> `Detach` |
| S2 | Falling, enters PoolZone | S3 | gravity off, drag `poolDrag` | `PoolZone.ReportEnter` -> `SetState` |
| S2 | Falling, enters RoofZone | S1 | if side RoofOnFire then S5 | `RoofZone.ReportEnter` -> `SetState`, `Kill` |
| S2 | Touches GroundFire | S5 | detach if attached | `GroundFireZone.ReportEnter` -> `Kill` |
| S2 | Touches burning side band | S5 | same | `FireSide.ReportEnter` -> `Kill` |
| S3 | Cup touch, cup empty or partial, bowl in pool | S4 | attach, strength 1 (accident) | `Cup.TryGrab` rule 2 |
| S3 | Cup touch, cup full, bowl in pool | S3 | bump + stun | `Cup.TryGrab` |
| S3 | Cup touch, bowl not in pool | S3 | Rejected | `Cup.TryGrab` |
| S3 | Pool entry | S3 | none | `PoolZone.ReportEnter` |
| S3 | Roof / ground fire / side fire | S5 only if the zone touches it (normally unreachable) | | `Kill` |
| S4 | Cup touch | S4 | Rejected | `Cup.TryGrab` |
| S4 | Tug | S4 (falling) | detach | `Cup.Tug` -> `Detach` |
| S4 | Strength 0 | S4 (falling) | detach | `Human.TickStrength` |
| S4 | Falling, enters PoolZone | S3 | as S2 | `PoolZone.ReportEnter` |
| S4 | Falling, enters RoofZone | S1 | the funny mistake; S5 if roof on fire | `RoofZone.ReportEnter` |
| S4 | Ground / side fire | S5 | | `Kill` |
| any non-S5 | Heli crash | S5 | | `HumansSystem.KillAll` -> `Kill` |
| S1 | Roof fire (RoofOnFire true for its side) | S5 | on ReachedTop for humans inside, on enter otherwise | `RoofZone` -> `Kill` |
| S1 | Side fire | not reachable | roof humans are above the burn band | |
| S5 | any event | S5 | all ignored (SetState no-op) | `SetState` guard |
| S1 | Pool entry (thrown or bounced) | S3 | | `PoolZone.ReportEnter` |
| S3 | Roof entry (thrown) | S1 | | `RoofZone.ReportEnter` |

Landing outcomes summary (from Tug or strength zero): pool -> S3, roof -> S1, ground fire -> S5, building side band -> S5. A falling human still counts as pending for the win check, because it keeps S2 or S4 until landing.

## 4. Fire rules (per FireSide, 4 sides, independent)
- Height01 in 0..1; floor count = Height01 * floors. Growth rate = 1 / (`spreadSecondsPerFloor` * floors) per second, so 60 s to the top with defaults.
- Start: all sides burning from Height01 0 after `startDelaySeconds`.
- IsBurning: true while growing or at the top. False only while paused after an extinguish.
- Extinguish(amount01): ignored if `IsLost` or `amount01 < extinguishMinWater01` (a partial cup does nothing). Otherwise Height01 = 0, IsBurning = false, RoofOnFire = false, PauseTimeLeft = `maxStopFireTime`. A full cup (>= fullThreshold 0.95) always meets the 0.9 minimum.
- Pause: `Advance(dt)` counts PauseTimeLeft down; at 0 the fire restarts from the bottom (IsBurning true, Height01 0). A new Extinguish during the pause restarts the pause timer.
- Reaches top: Height01 = 1, `ReachedTop` raised once, RoofOnFire = true; fire stays at the top until extinguished. Extinguishing a side at the top clears RoofOnFire. Humans on that roof side die (RoofZone).
- Burn band: collider enabled only when IsBurning, height = Height01 * total height, depth `burnTriggerDepth`. Anything IKillable touching it is killed.
- Lost mode: `SetLost()` sets IsLost, ends any pause, makes fire resume from its current Height01 at 1 / (`lostSpreadSecondsPerFloor` * floors) per second, ignores Extinguish forever. Reaching the top still sets RoofOnFire.
- Ground fire: permanent when `groundFirePermanent` is true. All ground except the pool.
- Lose conditions: heli crash (all humans die, all sides lost mode); all four sides at the top at once (`AllSidesBurning`, if `endOnAllSidesBurning`).
- Win: no human in S1, S2, S4 and at least one in S3.

## 5. Helicopter feel (HeliController)
Pipeline each FixedUpdate: raw command -> smoothed command -> target velocity -> acceleration -> force; yaw drift and leveling as torque.
- Input smoothing: each axis moves toward the raw value at 1/`inputRiseTime` per second when its magnitude grows, 1/`inputFallTime` when it shrinks (release feels floaty).
- Velocity response: wanted acceleration = (target velocity - velocity) * `velocityResponse`, clamped to `maxAcceleration`, applied as `ForceMode.Acceleration`. Move is relative to the heli yaw (W goes where the nose points). Target = smoothed Move * `maxHorizontalSpeed` (XZ), smoothed Vertical * `maxVerticalSpeed` (Y).
- Yaw: target yaw rate = smoothed Yaw * `maxYawRate`; torque pulls the angular speed toward it with the same response.
- Yaw drift: extra yaw torque = (Perlin(time * `driftNoiseFrequency`, random seed) * 2 - 1) * `yawDriftTorque`. Always on, slow, random.
- Leveling: torque = cross(transform.up, world up) * `levelingStrength`; weakly corrects pitch and roll.
- Visual tilt: the Body child leans toward the current acceleration, capped at `tiltVisualMaxDeg`. No physical effect.
- Drag: `linearDrag` and `angularDrag` go onto the Rigidbody; `rigidbodyMass` too.
Config values:
- maxHorizontalSpeed: top XZ speed.
- maxVerticalSpeed: top climb and descent speed.
- inputRiseTime: seconds for input to reach full from zero.
- inputFallTime: seconds for input to return to zero.
- velocityResponse: how aggressively velocity chases the target (1/s).
- maxAcceleration: acceleration cap, protects humans and the cup swing.
- maxYawRate: top turn rate with Q/E (deg/s).
- yawDriftTorque: strength of random yaw push (rad/s^2).
- driftNoiseFrequency: how fast the drift changes (Hz).
- levelingStrength: pull of pitch and roll back to level.
- tiltVisualMaxDeg: cosmetic lean cap.
- linearDrag, angularDrag, rigidbodyMass: Rigidbody damping and mass.

## 6. Test mode
Goal: set up situations and call the same public methods the physics callbacks call, deterministically.
- `TestModeController.Initialize()` collects ITestModeAware under the Game root. `EnterAll()` calls EnterTestMode on each, then `heli.SetControlSource(scripted)`. `ExitAll()` calls ExitTestMode and restores the previous source. Enabled by activating the TestHarness object; MainEntry installs it only when assigned.

| Actor | In test mode |
|---|---|
| HeliController | Body kinematic. Same smoothing model runs, but velocity is integrated by the controller (`MovePosition/MoveRotation`) from `ScriptedControlSource`. Saves and restores the previous source and kinematic flag. Crash sets flag and event, no fall. |
| Cup | Segments and bowl kinematic; the chain hangs straight and follows `anchorBody` rigidly (no swing). Acceleration still derived from the position delta, so moving the heli makes humans lose strength. Ignores the F key; the harness calls `Tug`. |
| Human | Body kinematic. `AttachTo` parents to `cup.HumanAnchor` slot instead of a joint; `Detach` unparents and the harness places the human into a zone or calls the zone's `ReportEnter` (it does not fall). Timers (stun) still run. |
| WaterDrop | Kinematic, no gravity; `Launch` just stores the amount. The harness moves it into a trigger, or calls `ReportHit(collider)`. |
| FireSide, zones | Not physical actors; no Rigidbody. Triggers still fire against kinematic Rigidbodies. |

Public methods a scenario calls: `HeliController.Crash`, `ScriptedControlSource.Set`, `Cup.FillTick`, `Cup.SetInPool`, `Cup.TryGrab`, `Cup.Tug`, `Cup.ReleaseAll`, `WaterDrop.ReportHit`, `Human.SetState/AttachTo/Detach/Kill/Stun/TickStrength`, `FireSide.Extinguish/SetLost/Advance`, `FireSystem.SetAllLost`, `HumansSystem.KillAll`, `PoolZone/RoofZone/GroundFireZone.ReportEnter/Stay/Exit`, `RotorHazard.ReportHit`. Asserts read `GameManager.Result/Score`, `Human.State`, `Cup.Water01`, `FireSide.Height01/IsBurning/RoofOnFire`.
Time-based checks (fire regrowth, stun, pause) use `FireSide.Advance(dt)` or a copied config with small values (`Object.Instantiate(config)` before Initialize). `ScenarioRunner` runs named scenarios as coroutines and prints `[TEST] <name> PASS|FAIL <reason>`. PlayMode tests reuse the same calls.

## 7. Config tables
Unit in brackets. All fields public/serialized on a ScriptableObject in Assets/Settings/. Fields marked (new) are not in brief.md.

HeliConfig: maxHorizontalSpeed 12 [m/s]; maxVerticalSpeed 6 [m/s]; inputRiseTime 0.6 [s]; inputFallTime 0.9 [s]; velocityResponse 1.5 [1/s]; maxAcceleration 6 [m/s^2]; maxYawRate 60 [deg/s]; yawDriftTorque 0.6 [rad/s^2]; driftNoiseFrequency 0.25 [Hz]; levelingStrength 2 [1/s^2]; tiltVisualMaxDeg 12 [deg]; linearDrag 0.3; angularDrag 1.5; rigidbodyMass 10 [kg].

CupConfig: capacity 100 [units]; fillRatePerSecond 25 [units/s]; emptyThreshold 0.05 [01]; fullThreshold 0.95 [01]; maxHumans 2; segmentCount 3; segmentLength 2 [m]; jointAngularLimitDeg 40 [deg]; jointSpring 50; jointDamper 8; grabZoneRadius 1.2 [m]; dumpDropRadius 1.5 [m]; bumpImpulse 4 [N s]; (new) bumpUpBias 0.5 [01]; regrabCooldown 1 [s]; humanSlotSpacing 0.8 [m]; bowlMass 2 [kg]; segmentMass 0.5 [kg]; accelerationSmoothing 0.1 [s]; dropLifetime 6 [s].

HumanConfig: maxStrength 100; accelerationThreshold 4 [m/s^2]; strengthLossFactor 6 [strength per m/s^2 over threshold per s]; strengthRecoveryPerSecond 3; stunDuration 2.5 [s]; humanCount 4; animator trigger names (strings) triggerWaitingOnRoof "WaitingOnRoof", triggerRescuedHanging "RescuedHanging", triggerChillingInPool "ChillingInPool", triggerSwimmingInTank "SwimmingInTank", triggerDead "Dead"; (new) poolDrag 4; detachDownSpeed 1 [m/s].

FireConfig: spreadSecondsPerFloor 12 [s]; maxStopFireTime 20 [s]; extinguishMinWater01 0.9 [01]; groundFirePermanent true; lostSpreadSecondsPerFloor 3 [s]; (new) startDelaySeconds 5 [s].

BuildingConfig: floors 5; floorHeight 3.5 [m]; footprintX 12, footprintZ 12 [m]; roofHumanSpawnMargin 1.5 [m]; (new) burnTriggerDepth 1.5 [m].

GameRulesConfig: scorePerRescued 100; penaltyPerDeath 25; bonusPerSideNotOnFire 25; endOnAllSidesBurning true; pool (PoolConfig): poolSize (10, 2.5, 6) [m], distanceFromBuilding 14 [m, wall to nearest pool edge].

## 8. Module map
Order is bottom to top; each module may reference only those above it.

| # | Assembly (= namespace) | References | Classes |
|---|---|---|---|
| 1 | Injection | none | Context, Injector, Inject, Tick (existing) |
| 2 | FlameAndBlame.Core | Injection | HumanState, Side, GameResult, GrabResult, HeliCommand, IControlSource, ITestModeAware, IKillable |
| 3 | FlameAndBlame.VFX | Injection, Core | EffectController base, FireEffect, WaterSplashEffect, StunStarsEffect, SmokeEffect |
| 4 | FlameAndBlame.Heli | Injection, Core, Unity.InputSystem | HeliConfig, HeliController, PlayerControlSource, RotorHazard |
| 5 | FlameAndBlame.Fire | Injection, Core, VFX | FireConfig, BuildingConfig, FireSide, FireSystem, GroundFireZone |
| 6 | FlameAndBlame.Rescue | Injection, Core, Fire, VFX | CupConfig, HumanConfig, Cup, WaterDrop, Human, HumanAnimator, HumansSystem, PoolZone, RoofZone |
| 7 | FlameAndBlame.GameRules | Injection, Core, Heli, Fire, Rescue | GameRulesConfig (+PoolConfig), GameManager |
| 8 | FlameAndBlame.UI | Injection, Core, Rescue, GameRules, UnityEngine.UI, Unity.TextMeshPro | HudController, WaterGauge, HumanStrengthBar, ResultPanel |
| 9 | FlameAndBlame.Testing | all | TestModeController, ScriptedControlSource, ScenarioRunner |
| 10 | FlameAndBlame.Main | all | MainEntry |
| - | FlameAndBlame.EditorTools | all, Editor only | ProjectBootstrap |
| - | FlameAndBlame.Tests.PlayMode | all, test runners | tests |

Initialize order in MainEntry (after ApplyInstall): fire, humans, cup, heli, game, hud, testMode (if assigned). Reasons: fire and humans have no dependencies and own their children; cup needs humans ready for TryGrab; heli needs only config; game subscribes to heli, humans and fire so they exist; hud subscribes to cup, humans, game last; testMode collects actors last. PlayerControlSource, RotorHazard, PoolZone, RoofZone, WaterDrop, HumanAnimator have no Initialize.

Dependency check (all fine, no upward reference):
- Heli does not know Cup or Human: RotorHazard filters by tag and layer; Cup receives the heli only as a serialized `Rigidbody anchorBody`, and reads input through `IControlSource` (Core).
- Fire does not know Rescue: bands and zones call `IKillable` (Core); FireSide has no Human reference. Rescue references Fire (WaterDrop -> FireSide, RoofZone -> FireSide).
- Rescue -> VFX (stars) and Fire only. Human -> Cup is inside Rescue.
- GameRules references Heli (crash event), Fire, Rescue; none of them know GameRules. UI reads Rescue and GameRules only through events.
- Testing and Main are referenced by nobody.
- Closest risk: `PoolConfig` lives in GameRules but nothing below uses it (only the scene-builder). Keep it that way.
