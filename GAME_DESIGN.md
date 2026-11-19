# Flame and Blame: game design

Read with CLAUDE.md (rules, contract) and brief.md (spec, defaults). Numbers are config field names (ScriptableObject); defaults in section 7. No gameplay number is hardcoded.

Conventions
- Physics callbacks only forward to public methods (`ReportEnter/ReportStay/ReportExit/ReportHit`), logic lives in the public methods.
- "Unattached human" = a Human with `Cup` link null (not hanging).
- Water01 and amount01 are fractions of `CupConfig.capacity` (0..1).
- Initialize() runs only from MainEntry. Components with their own Awake/OnEnable do no injected work there.

## 1. Components

### PlayerControlSource (Heli)
Creates InputActions in code in Awake, enables in OnEnable, disposes in OnDestroy. `Read()` returns Move (raw WASD: x = D-A, y = W-S, debug only), MoveWorld (camera-relative, below), Vertical (Space +1, LeftCtrl -1), Yaw (Z -1, X +1; Q and E are removed), TugPressed (F, true only in the frame it was pressed). No Initialize.
Serialized `Transform cameraTransform` (the Main Camera driven by the CinemachineBrain). Each `Read()`: forward = camera forward flattened onto the horizontal plane and normalized; right = camera right flattened and normalized. If the flattened forward is near zero (camera looking straight down), forward = the flattened camera up. `MoveWorld = ClampMagnitude(forward * Move.y + right * Move.x, 1)`, y = 0. If `cameraTransform` is null: forward = world Z, right = world X, and one warning is logged.

### HeliController (Heli)
- Reads `IControlSource.Read()` in FixedUpdate and applies the feel model of section 5. Moves along `MoveWorld` only (never relative to its own yaw). Ignores TugPressed (Cup reads it).
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
- Slots (Phase 5, section 11): serialized `Transform[] hangSlots` (HangSlot_1, HangSlot_2 on the rim) and `Transform swimSlot` (inside, near the top). A slot holds one human. `AttachedCount` counts hanging and swimming humans together and never exceeds `maxHumans` (2).
- `TryGrab(Human h)` rules, first match wins (Phase 5, supersedes the earlier list):
  1. h is Dead, already attached, `IsFalling`, `IsStunned`, `AttachedCount >= maxHumans`, or the grab lock is active (`regrabCooldown` after a Tug) -> Rejected.
  2. `InPool` and h.State == ChillingInPool and `swimSlot` free -> `h.AttachTo(this)` (becomes SwimmingInTank at the SwimSlot) -> Grabbed. Whatever the water level: the accidental scoop.
  3. `Water01 > emptyThreshold` (tank not empty) and h.State is WaitingOnRoof or ChillingInPool (the pool case that reaches here is: bowl outside the pool fill, or no swim slot free) -> push `h.Body` away from the bowl with impulse `bumpImpulse` (plus `bumpUpBias` upward), `h.Stun()` -> BouncedFull.
  4. `IsEmpty` and h.State == WaitingOnRoof and a hang slot free -> `h.AttachTo(this)` (becomes RescuedHanging at that HangSlot) -> Grabbed.
  5. otherwise -> Rejected.
- GrabZone (trigger, tag CupGrabZone) `OnTriggerEnter/Stay` and the bowl `OnCollisionEnter` forward to TryGrab for any collider tagged Human.
- `Tug()`: if not `IsEmpty`: instantiate `dropPrefab` at the bowl, scale to diameter `2*dumpDropRadius`, `Launch(Water01, Body.linearVelocity)`, `Destroy(drop, dropLifetime)`, Water01 = 0. Then `ReleaseAll()`. Tug with an empty cup only releases. Update reads `IControlSource.TugPressed` and calls Tug (skipped in test mode).
- `ReleaseAll()`: every attached human `Detach()` and receives the bowl's `linearVelocity` (inherited momentum) and becomes Falling (`IsFalling`); slots freed, list cleared, grab lock for `regrabCooldown`. Humans keep their state while falling; the landing decides (section 3).
- Slots replace the old `HumanAnchor` offset placement (`humanSlotSpacing` is unused now). `HumanAnchor` stays as the test-mode fallback parent if a slot is missing.

### WaterDrop (Rescue)
A solid physical ball, already implemented (see section 10). Hit on a wall or on a RoofSlab extinguishes the matching FireSide.

### Human (Rescue)
Full Phase 5 behaviour (physics mode per state, attach points, landing, strength, pool drift) is in section 11 and wins over any older line.
- Initialize(HumanConfig): stores config, Strength01 = 1, IsFalling = false. HumansSystem then calls `SetState(WaitingOnRoof)`.
- `SetState(s)`: the only writer of State and the only place that picks the Rigidbody mode (table in section 11). No-op if same state or State == Dead. Calls `HumanAnimator.Apply(s)`, raises `StateChanged`.
- `AttachTo(cup)`: requires unattached, not Dead. Dynamic mode: ConfigurableJoint (RescuedHanging) or parent to the swim slot (SwimmingInTank); test mode: parent to the slot, kinematic. Strength01 = 1. From WaitingOnRoof -> RescuedHanging, from ChillingInPool -> SwimmingInTank.
- `Detach()`: removes joint or unparent, restores collisions, clears the cup link, sets `IsFalling = true`, plays the falling trigger. State is not changed. The caller (Cup) then sets the velocity.
- `Kill()` (IKillable): idempotent. Detach if attached, `SetState(Dead)`.
- `Stun()`: `IsStunned = true` for `stunDuration` (Invoke, no polling), `HumanAnimator.PlayStun()`, then back to the state's trigger. No state change. The impulse is applied by Cup.TryGrab before calling Stun.
- `TickStrength(dt, accel)`: section 11. Fires `StrengthChanged(Strength01)`.
- Landing: public `ReportCollision(Collider other)` (from OnCollisionEnter) and PoolZone / GroundFireZone / RoofSlab calls, section 11. Physics callbacks only forward.

### HumanAnimator (Rescue)
`Apply(HumanState)` and `PlayStun()`: trigger names come from HumanConfig (section 11), converted with `Animator.StringToHash`. If the Animator or a parameter is missing: one warning, nothing else. The user's controller `HumanAnimator.controller` is never edited.

### HumansSystem (Rescue)
`[Inject] HumanConfig, FireSystem`. Serialized `Human humanPrefab` (prefab asset) and `GameObject scenePreview`. Initialize() spawns (section 11), subscribes to each `StateChanged` and re-raises `AnyStateChanged`. `IReadOnlyList<Human> Humans`, `CountIn(s)`, `KillAll()` (Kill on every non-dead human).

### PoolZone (Rescue)
Trigger, layer Water, tag Pool, size from `PoolConfig.poolSize`. Serialized `Cup cup`, `Transform waterSurface, driftArea`; `float SurfaceY` (world Y of waterSurface), `Bounds DriftBounds` (world box of driftArea scale).
- `ReportEnter/Stay/Exit(Collider)`:
  - tag Cup (bowl): Enter -> `cup.SetInPool(true)`; Stay -> `cup.FillTick(Time.fixedDeltaTime)`; Exit -> `SetInPool(false)`.
  - tag Human, unattached and not Dead: Enter -> `SetState(ChillingInPool)` (also ends `IsFalling`). It also gives the human a pointer to this pool (SurfaceY, DriftBounds) for floating.
- Accidental grab is not here: the cup's GrabZone overlaps the pool human and Cup.TryGrab rule 2 handles it.

### RoofSlab (Fire)
Replaces RoofZone and RoofSectorMesh (both deleted). Four hand-made, non-overlapping `RoofSlab` boxes of different sizes under `Roof`, one per side. They are user-built: keep their sizes. Each is a non-trigger BoxCollider (layer Building, tag Roof, solid).
- Serialized: `FireSide fireSide`, `FillEffectView roofFireView` (on the `RoofFire` child), `Transform humanSpawnArea` (child).
- `Initialize(FireConfig, BuildingConfig)`, called by FireSystem:
  - Does NOT move or size `RoofFire` or `humanSpawnArea`; both are placed by hand in the RoofSlab prefab. `RoofFire` is a box with `M_Fire`, `FillEffectView`, a trigger BoxCollider (layer FireZone) and `RoofSlabTrigger`.
  - Sets `roofFireView.SmoothTime = FireConfig.roofIgniteTime`.
  - A human dies on the roof only by touching the RoofFire collider while the roof burns (hanging humans included). No other kill volume.
- `FireSide FireSide`, `bool OnFire`: follows `fireSide.RoofOnFire` (set when the side reaches the top, cleared by Extinguish). Its Update only forwards `roofFireView.SetFill(OnFire ? 1 : 0)` (presentation, one bool read). Only the slab of a side at the top lights up.
- `ReportStay(Collider other)`: the child trigger box forwards `OnTriggerEnter/Stay` here. While `OnFire`, an `IKillable` in the trigger is killed (`Kill()`), attached or not. Not on fire: nothing.
- `Bounds SpawnBounds`, `Vector3 GetSpawnPoint()`: world-space bounds of the spawn box; `GetSpawnPoint` returns a random point on the bottom of the spawn box. `OnDrawGizmos` draws the spawn box. Used by HumansSystem in Phase 5.
- Landing (Phase 5): RoofSlab (Fire) does not know Human. A falling human landing on a slab becomes WaitingOnRoof through the Human side: `Human.OnCollisionEnter` forwards to public `Human.ReportCollision(Collider)`, which checks `GetComponentInParent<RoofSlab>()` on the other collider (Rescue references Fire; Human collides with Building) and, when `IsFalling`, calls `SetState(WaitingOnRoof)`. If that slab is on fire, the RoofFire trigger then kills it.

### FireSide (Fire)
See section 4. Two child triggers: burn band (layer FireZone, tag BuildingSide) forwards enter/stay to `ReportEnter/Stay`: any `IKillable` -> `Kill()` (all humans, attached or not; roof humans are above the band). Water catch trigger (full height, layer FireZone) is what WaterDrop hits. Initialize(FireConfig) then `ApplyBuilding(BuildingConfig)` (total height = floors * floorHeight; band depth `burnTriggerDepth`).

### FireSystem (Fire)
Serialized FireSide[4], GroundFireZone[] and `RoofSlab[] roofSlabs`. [Inject] FireConfig, BuildingConfig. Exposes `IReadOnlyList<RoofSlab> RoofSlabs` (HumansSystem spawns humans through it in Phase 5). Initialize(): initializes sides, then slabs (`RoofSlab.Initialize(fireConfig, buildingConfig)`), then ground zones, subscribes to each `ReachedTop`. `SidesNotOnFire` = sides with IsBurning false (extinguished and paused). `SetAllLost()` calls SetLost on all. `AllSidesBurning` raised once when all four sides have Height01 == 1.

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
heli.Crashed -> GameManager. Cup.WaterChanged -> WaterGauge. Human.StateChanged -> HumansSystem.AnyStateChanged -> GameManager, HumanStrengthBar. FireSide.ReachedTop -> FireSystem (RoofSlab reads fireSide.RoofOnFire). FireSystem.AllSidesBurning -> GameManager. Nothing in a lower module holds a reference to a higher one.

## 3. Human state transition table
States: S1 WaitingOnRoof, S2 RescuedHanging, S3 ChillingInPool, S4 SwimmingInTank, S5 Dead. "Landing" = unattached human entering a zone. Stun never changes state.

| From | Event | To | Side effects | Method that does it |
|---|---|---|---|---|
| S1 | Cup touch, tank empty, a hang slot free | S2 | attach to the HangSlot, strength 1 | `Cup.TryGrab` -> `Human.AttachTo` -> `SetState` |
| S1 | Cup touch, tank not empty (partial or full) | S1 | impulse `bumpImpulse`, `Stun()`; BouncedFull | `Cup.TryGrab`, `Human.Stun` |
| S1 | Cup touch, `AttachedCount` = maxHumans, no hang slot, or grab lock | S1 | Rejected | `Cup.TryGrab` |
| S2 | Cup touch (any) | S2 | Rejected (already attached) | `Cup.TryGrab` |
| S2 | Tug | S2 + Falling | detach, bowl velocity, falls; landing decides | `Cup.Tug` -> `ReleaseAll` -> `Human.Detach` |
| S2 | Strength reaches 0 | S2 + Falling | detach, falls (no bowl velocity push beyond the current motion) | `Human.TickStrength` -> `Detach` |
| S2 | Falling, enters PoolZone | S3 | IsFalling off, kinematic float | `PoolZone.ReportEnter` -> `SetState` |
| S2 | Falling, lands on a RoofSlab | S1 | IsFalling off; if that slab OnFire then S5 | `Human.ReportCollision` (RoofSlab in parent chain) -> `SetState`; `RoofSlab.ReportStay` -> `Kill` |
| S2 | Touches GroundFireZone (any time) | S5 | detach if attached | `GroundFireZone.ReportEnter` -> `Kill` |
| S2 | Touches burning RoofSlab fire collider (hanging or falling) | S5 | | `RoofSlab.ReportStay` -> `Kill` |
| S2 | Touches burning side band | S5 | | `FireSide.ReportEnter` -> `Kill` |
| S3 | Cup touch, bowl `InPool`, swim slot free, `AttachedCount` < maxHumans | S4 | attach at SwimSlot, strength 1 (the accident), any water level | `Cup.TryGrab` rule 2 |
| S3 | Cup touch, bowl in pool but no swim slot or no capacity, tank not empty | S3 | impulse `bumpImpulse`, Stun; BouncedFull | `Cup.TryGrab` rule 3 |
| S3 | Cup touch, bowl not in pool, tank not empty | S3 | impulse + Stun; BouncedFull | `Cup.TryGrab` rule 3 |
| S3 | Cup touch, tank empty, no slot rule applies | S3 | Rejected | `Cup.TryGrab` |
| S3 | Pool entry | S3 | none | `PoolZone.ReportEnter` |
| S3 | Roof fire / ground fire / side fire | S5 only if a zone touches it (normally unreachable) | | `Kill` |
| S4 | Cup touch | S4 | Rejected | `Cup.TryGrab` |
| S4 | Tug | S4 + Falling | detach with bowl velocity | `Cup.Tug` -> `Detach` |
| S4 | Strength | unchanged | swimming humans do not lose strength | `TickStrength` ignores S4 |
| S4 | Falling, enters PoolZone | S3 | as S2 | `PoolZone.ReportEnter` |
| S4 | Falling, lands on a RoofSlab | S1 | the funny mistake; S5 if the slab is OnFire | `Human.ReportCollision`; `RoofSlab.ReportStay` |
| S4 | Ground / side fire | S5 | | `Kill` |
| any non-S5 | Heli crash | S5 | | `HumansSystem.KillAll` -> `Kill` |
| S1 | Standing on a burning slab (OnFire) | S5 | RoofFire trigger Enter/Stay while OnFire, so a human already there dies when the fire reaches the top | `RoofSlab.ReportStay` -> `Kill` |
| S1 | Side fire | not reachable | roof humans are above the burn band | |
| S1 | Strength zero | not applicable | only RescuedHanging has strength | |
| S5 | any event | S5 | all ignored (SetState no-op) | `SetState` guard |
| S1 | Pool entry (bounced or thrown) | S3 | | `PoolZone.ReportEnter` |
| S3 | Lands on a RoofSlab (thrown) | S1 | | `Human.ReportCollision` |

"Falling" is the bool `IsFalling`, not a state: it starts in `Detach` (Tug, ReleaseAll, strength zero) and ends on landing. Landing outcomes: pool -> S3, RoofSlab -> S1, GroundFireZone or burning RoofSlab fire -> S5. A falling human keeps S2 or S4, so it still counts as pending for the win check until it lands.

## 4. Fire rules (per FireSide, 4 sides, independent)
- Height01 in 0..1; floor count = Height01 * floors. Growth rate = 1 / (`spreadSecondsPerFloor` * floors) per second, so 60 s to the top with defaults.
- Start: all sides burning from Height01 0 after `startDelaySeconds`.
- IsBurning: true while growing or at the top. False only while paused after an extinguish.
- Extinguish(amount01): ignored if `IsLost` or `amount01 < extinguishMinWater01` (a partial cup does nothing). Otherwise Height01 = 0, IsBurning = false, RoofOnFire = false, PauseTimeLeft = `maxStopFireTime`. A full cup (>= fullThreshold 0.95) always meets the 0.9 minimum.
- Pause: `Advance(dt)` counts PauseTimeLeft down; at 0 the fire restarts from the bottom (IsBurning true, Height01 0). A new Extinguish during the pause restarts the pause timer.
- Reaches top: Height01 = 1, `ReachedTop` raised once, RoofOnFire = true; fire stays at the top until extinguished. Extinguishing a side at the top clears RoofOnFire. Humans standing on that side's RoofSlab die (`RoofSlab.ReportStay`) while it is OnFire.
- Burn band: collider enabled only when IsBurning, height = Height01 * total height, depth `burnTriggerDepth`. Anything IKillable touching it is killed.
- Lost mode: `SetLost()` sets IsLost, ends any pause, makes fire resume from its current Height01 at 1 / (`lostSpreadSecondsPerFloor` * floors) per second, ignores Extinguish forever. Reaching the top still sets RoofOnFire.
- Ground fire: permanent when `groundFirePermanent` is true. All ground except the pool.
- Lose conditions: heli crash (all humans die, all sides lost mode); all four sides at the top at once (`AllSidesBurning`, if `endOnAllSidesBurning`).
- Win: no human in S1, S2, S4 and at least one in S3.

## 5. Helicopter feel (HeliController)
Pipeline each FixedUpdate: raw command -> smoothed command -> target velocity -> acceleration -> force; yaw drift and leveling as torque.
- Camera-relative movement: `PlayerControlSource` converts WASD to `HeliCommand.MoveWorld` (camera forward and right flattened to the horizontal plane; if the camera looks straight down, the projected camera up is forward; with no camera, world Z and X). The heli moves along `MoveWorld` only, never relative to its own yaw, so turning the heli changes neither the move direction nor the camera (the orbit stays in world space).
- Input smoothing: each axis moves toward the raw value at 1/`inputRiseTime` per second when its magnitude grows, 1/`inputFallTime` when it shrinks (release feels floaty). It applies to `MoveWorld` as a world-space vector (x and z), and to Vertical and Yaw.
- Velocity response: wanted acceleration = (target velocity - velocity) * `velocityResponse`, clamped to `maxAcceleration`, applied as `ForceMode.Acceleration`. Target = smoothed MoveWorld * `maxHorizontalSpeed` (XZ), smoothed Vertical * `maxVerticalSpeed` (Y).
- Yaw: Z = Yaw -1 (counterclockwise seen from above), X = Yaw +1 (clockwise seen from above); Q and E are removed. Target yaw rate = smoothed Yaw * `maxYawRate`; torque pulls the angular speed toward it with the same response. Yaw drift (below) stays.
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
- maxYawRate: top turn rate with Z/X (deg/s).
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
| Human | Body kinematic in every state. `AttachTo` parents to the cup slot (HangSlot with TankAttachPoint on it, or SwimSlot with FloatPoint on it) instead of a joint; `Detach` unparents, sets `IsFalling`, and the harness then calls `PoolZone.ReportEnter`, `Human.ReportCollision(slabCollider)` or `GroundFireZone.ReportEnter` itself (a kinematic human does not fall). Pool float and drift still run (MovePosition works on kinematic bodies). Stun timer still runs. Strength still drains through `TickStrength`. |
| WaterDrop | Stays dynamic (see section 10); the harness may call `ReportHit(collider, point)` directly. |
| FireSide, zones | Not physical actors; no Rigidbody. Triggers still fire against kinematic Rigidbodies. |

Public methods a scenario calls: `HeliController.Crash`, `ScriptedControlSource.Set`, `Cup.FillTick`, `Cup.SetInPool`, `Cup.TryGrab`, `Cup.Tug`, `Cup.ReleaseAll`, `WaterDrop.ReportHit(collider, point)`, `Human.SetState/AttachTo/Detach/Kill/Stun/TickStrength`, `FireSide.Extinguish/SetLost/Advance`, `FireSystem.SetAllLost`, `HumansSystem.KillAll`, `PoolZone/GroundFireZone.ReportEnter/Stay/Exit`, `RoofSlab.ReportStay`, `RoofSlab.Initialize/GetSpawnPoint`, `ScriptedControlSource.Set` (tests set `MoveWorld` directly; if only `Move` is set, x maps to world X and y to world Z), `RotorHazard.ReportHit`. Asserts read `GameManager.Result/Score`, `Human.State`, `Cup.Water01`, `FireSide.Height01/IsBurning/RoofOnFire`.
Time-based checks (fire regrowth, stun, pause) use `FireSide.Advance(dt)` or a copied config with small values (`Object.Instantiate(config)` before Initialize). `ScenarioRunner` runs named scenarios as coroutines and prints `[TEST] <name> PASS|FAIL <reason>`. PlayMode tests reuse the same calls.

## 7. Config tables
Unit in brackets. All fields public/serialized on a ScriptableObject in Assets/Settings/. Fields marked (new) are not in brief.md.

HeliConfig: maxHorizontalSpeed 12 [m/s]; maxVerticalSpeed 6 [m/s]; inputRiseTime 0.6 [s]; inputFallTime 0.9 [s]; velocityResponse 1.5 [1/s]; maxAcceleration 6 [m/s^2]; maxYawRate 60 [deg/s]; yawDriftTorque 0.6 [rad/s^2]; driftNoiseFrequency 0.25 [Hz]; levelingStrength 2 [1/s^2]; tiltVisualMaxDeg 12 [deg]; linearDrag 0.3; angularDrag 1.5; rigidbodyMass 10 [kg].

CupConfig: capacity 100 [units]; fillRatePerSecond 25 [units/s]; emptyThreshold 0.05 [01]; fullThreshold 0.95 [01]; maxHumans 2; segmentCount 3; segmentLength 2 [m]; jointAngularLimitDeg 40 [deg]; jointSpring 50; jointDamper 8; grabZoneRadius 1.2 [m]; dumpDropRadius 1.5 [m]; bumpImpulse 4 [N s]; (new) bumpUpBias 0.5 [01]; regrabCooldown 1 [s]; humanSlotSpacing 0.8 [m]; bowlMass 2 [kg]; segmentMass 0.5 [kg]; accelerationSmoothing 0.1 [s]; dropLifetime 6 [s, legacy, unused]; (new, Phase 3) fillVisualSmoothTime 0.25 [s, smoothing of the tank water shader fill]; (new, Phase 4, section 10) waterBallRadius 0.6 [m]; waterBallMass 5 [kg]; waterBallLifetime 8 [s]; waterBallKillY -20 [m, world Y].

HumanConfig (Phase 5, see section 11 for use): humanCount 4; mass 60 [kg]; maxStrength 100; accelerationThreshold 4 [m/s^2]; strengthLossFactor 6 [strength per (m/s^2 over threshold) per s]; strengthRecoveryPerSecond 3 [strength/s]; stunDuration 2.5 [s]; hangJointAngularLimitDeg 60 [deg]; hangJointLinearLimit 0.3 [m, unused: linear motion is Locked]; hangJointSpring 200; hangJointDamper 20; floatHeight 0.0 [m, FloatPoint above SurfaceY]; bobAmplitude 0.1 [m]; bobFrequency 0.5 [Hz]; driftSpeed 0.8 [m/s]; driftRetargetTime 4 [s]; driftArriveDistance 0.3 [m, target counts as reached]; driftSeparation 1.2 [m]; deadSettleTime 2.5 [s, Dead turns kinematic]; animator trigger names (strings): waitRescueTrigger "WaitRescue", hangingTrigger "Hanging", chillPoolTrigger "ChillPool", swimInTankTrigger "ChillPool" (no separate clip yet), deadTrigger "Dead", stunnedTrigger "Stunned", fallingTrigger "Hanging". Replaced and unused now: triggerWaitingOnRoof/RescuedHanging/ChillingInPool/SwimmingInTank/Dead, poolDrag, detachDownSpeed (the bowl velocity is inherited instead). `bumpImpulse` 4 and `bumpUpBias` 0.5 stay in CupConfig (the Cup applies the push before `Human.Stun()`).

FireConfig: spreadSecondsPerFloor 12 [s]; maxStopFireTime 20 [s]; extinguishMinWater01 0.9 [01]; groundFirePermanent true; lostSpreadSecondsPerFloor 3 [s]; (new) startDelaySeconds 5 [s]; (new, Phase 3) roofIgniteTime 1.5 [s, smoothing time of the roof fire shader fill, visual only]; (new, Phase 4) roofFireHeight 1.5 [m, height of the RoofFire box above the slab top face].

UIConfig (new, Phase 3, seventh asset, lives in the UI module): controlsHintText (multi-line string, default below); hintFontSize 22 [px]; hintMargin (20, 20) [px, from the top-right corner]. Default controlsHintText: "WASD move (camera) | Space/Ctrl up/down | Z/X turn | F drop water | Mouse look | Wheel zoom". The existing UIConfig.asset still holds the old Q/E text; the Phase 4 task updates both the code default and the asset value.

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
| 5 | FlameAndBlame.Fire | Injection, Core, VFX | FireConfig, BuildingConfig, FireSide, FireSideTrigger, FireSystem, GroundFireZone, RoofSlab |
| 6 | FlameAndBlame.Rescue | Injection, Core, Fire, VFX | CupConfig, HumanConfig, Cup, WaterDrop, Human, HumanAnimator, HumansSystem, PoolZone |
| 7 | FlameAndBlame.GameRules | Injection, Core, Heli, Fire, Rescue | GameRulesConfig (+PoolConfig), GameManager |
| 8 | FlameAndBlame.UI | Injection, Core, Rescue, GameRules, UnityEngine.UI, Unity.TextMeshPro | HudController, WaterGauge, HumanStrengthBar, ResultPanel |
| 9 | FlameAndBlame.Testing | all | TestModeController, ScriptedControlSource, ScenarioRunner |
| 10 | FlameAndBlame.Main | all | MainEntry |
| - | FlameAndBlame.EditorTools | all, Editor only | ProjectBootstrap |
| - | FlameAndBlame.Tests.PlayMode | all, test runners | tests |

Phase 5: `HumansSystem` injects `FireSystem` (Rescue -> Fire, allowed) and spawns from `RoofSlab.GetSpawnPoint`, so `fire.Initialize()` must run before `humans.Initialize()` (it does). `Human` reads `RoofSlab` by `GetComponentInParent` (Rescue -> Fire, allowed); nothing in Fire references Rescue.

Initialize order in MainEntry (after ApplyInstall): fire, humans, cup, heli, game, hud, testMode (if assigned). Reasons: fire and humans have no dependencies and own their children; cup needs humans ready for TryGrab; heli needs only config; game subscribes to heli, humans and fire so they exist; hud subscribes to cup, humans, game last; testMode collects actors last. PlayerControlSource, RotorHazard, PoolZone, WaterDrop, HumanAnimator have no Initialize. RoofSlab.Initialize is called by FireSystem.Initialize (not by MainEntry).

Dependency check (all fine, no upward reference):
- Heli does not know Cup or Human: RotorHazard filters by tag and layer; Cup receives the heli only as a serialized `Rigidbody anchorBody`, and reads input through `IControlSource` (Core).
- Fire does not know Rescue: bands and zones call `IKillable` (Core); FireSide has no Human reference. Rescue references Fire (WaterDrop -> FireSide and RoofSlab; HumansSystem -> FireSystem.RoofSlabs in Phase 5). RoofSlab (Fire) holds no Human reference; it uses IKillable, and the landing to WaitingOnRoof lives on the Human side by tag Roof.
- Rescue -> VFX (stars) and Fire only. Human -> Cup is inside Rescue.
- GameRules references Heli (crash event), Fire, Rescue; none of them know GameRules. UI reads Rescue and GameRules only through events.
- Testing and Main are referenced by nobody.
- Closest risk: `PoolConfig` lives in GameRules but nothing below uses it (only the scene-builder). Keep it that way.
- Phase 3: `FillEffectView` lives in VFX (references Injection, Core only). `RoofSectorMesh` was deleted in Phase 4. Fire and Rescue may reference VFX (already in the table); `RoofSlab` (Fire) uses `FillEffectView` (VFX). `UIConfig` and `ControlsHintView` live in UI (references Core, UnityEngine.UI, TextMeshPro); `UIConfig` is installed in MainEntry like any other config.

## 9. Visuals and controls hint (Phase 3)
Visual only. No gameplay number is read from a material; gameplay stays in the ScriptableObjects. Visuals follow gameplay state through events or per-frame setters, never the other way.

### FillEffect shader
File `Assets/Shaders/FillEffect.shader`, hand-written URP Unlit HLSL, Transparent queue, alpha blending, ZWrite off, no Shader Graph. One shader serves wall fire, roof fire, tank water and pool water.
Properties: `_Fill` (0..1, how much of the object is filled from the bottom), `_Intensity` (0..1, multiplies the filled alpha; 0 hides the fill, used for "paused" fire), `_FillColor`, `_EdgeColor` (glow band at the fill line), `_BaseColor` (color of the part above the fill line; alpha 0 for fire overlays, light grey semi-transparent for water), `_EdgeWidth` (band width in normalized Y), `_NoiseScale`, `_NoiseSpeed`, `_MinY`, `_MaxY` (object-space Y range the fill runs over, set per renderer by `FillEffectView`).
Fill line: per pixel, `y01 = saturate((objectY - _MinY) / (_MaxY - _MinY))`. A cheap value noise of (object XZ * `_NoiseScale`, time * `_NoiseSpeed`) is sampled in -0.5..0.5 and added to `_Fill` near the edge only: `line = _Fill + noise * _EdgeWidth * edgeMask`, where `edgeMask` is 1 at the edge and 0 for `_Fill` of exactly 0 or 1 so empty and full are clean. The pixel is filled when `y01 < line`. Inside the edge band (`line - _EdgeWidth < y01 < line`) the color blends to `_EdgeColor`, so the top flickers like flames. Filled pixels use `_FillColor` with alpha scaled by `_Intensity`; unfilled pixels use `_BaseColor`. Fully clipped when both alphas are 0.

### Materials (Assets/VFX/)
- `M_Fire`: `_FillColor` orange, `_EdgeColor` yellow, `_BaseColor` alpha 0 (invisible above the flame line), `_EdgeWidth` 0.08, `_NoiseScale` 4, `_NoiseSpeed` 2.
- `M_Water`: `_FillColor` blue (alpha about 0.7), `_EdgeColor` lighter blue, `_BaseColor` light grey, semi-transparent, `_EdgeWidth` 0.03, low noise scale and speed (calm surface).
These are look values; the artist may tune them freely.

### FillEffectView (VFX)
`[RequireComponent(typeof(Renderer))]`. Public: `SetFill(float fill01)`, `SetIntensity(float intensity01)`, serialized `float SmoothTime`. It stores targets, and each frame moves current values toward them with `Mathf.SmoothDamp` (smoothTime = `SmoothTime`; 0 means jump). This per-frame smoothing is presentation only and reads no gameplay distances. It writes through a `MaterialPropertyBlock` (`GetPropertyBlock`, `SetFloat`, `SetPropertyBlock`); it never touches `renderer.material`, so all views share the same material asset. In Awake it reads `renderer.localBounds` (mesh bounds in object space) and sets `_MinY` and `_MaxY` once. It only writes the block when a value changed. Tests read the same block with `Renderer.GetPropertyBlock` and check `_Fill` and `_Intensity` (the displayed, smoothed values, so tests wait or use SmoothTime 0).

### Roof fire visual (Phase 4)
`RoofSectorMesh` is deleted. The roof fire is the plain `RoofFire` box on each `RoofSlab` (see section 1, RoofSlab): `M_Fire`, `FillEffectView`, no collider, covering the slab top face. `FillEffect` fills along object Y, so `_Fill` 1 shows the flame column and `_Fill` 0 hides it.

### Wiring
- `FireSide.wallFireView` (serialized `FillEffectView` on `FireOverlay`): every frame in Update (view, not logic) `SetFill(Height01)` and `SetIntensity(IsBurning ? 1 : 0)`. So the flame climbs with the fire, fades while paused after an extinguish (Height01 is 0 then anyway) and in lost mode stays at full. `FireOverlay` is a thin box or quad 0.05 m outside the wall (local +Z), full building height, no collider, `M_Fire`. Its `SmoothTime` is small (0.1 s).
- `RoofSlab.roofFireView` (on the `RoofFire` child of each slab): driven by `OnFire` (= `FireSide.RoofOnFire`): in Update (presentation only, one bool read) `SetFill(OnFire ? 1 : 0)`. `RoofSlab.Initialize` sets `SmoothTime = FireConfig.roofIgniteTime`. Only the slab of the side that reached the top lights up.
- `Cup.waterView` (on the bowl renderer, material `M_Water`): `Cup.Initialize` sets `SmoothTime = CupConfig.fillVisualSmoothTime` and subscribes to `WaterChanged`, calling `SetFill(Water01)`. Tug sets Water01 = 0 at once and the view drains through its smoothing. `_BaseColor` of `M_Water` shows the empty part of the tank as pale grey, so an empty tank is readable.
- Pool surface: the water plane or slab uses `M_Water` with `_Fill` 1 (set statically in a FillEffectView with `SetFill(1)` and SmoothTime 0, or a material property block set by the scene-builder). No logic.

### UIConfig and ControlsHintView (UI)
`UIConfig` fields and defaults in section 7. The text lists the real controls, taken from `PlayerControlSource` and the camera: WASD move (relative to the camera), Space up, Left Ctrl down, Z/X turn, F tug (drops water and releases humans), mouse look, wheel zoom.
`ControlsHintView : MonoBehaviour` (`[Inject] UIConfig`, `Initialize()` called by MainEntry after `hud`): finds or creates (serialized) a text component on the Canvas, sets text = `controlsHintText`, font size = `hintFontSize`, anchors it to the top-right corner (anchor and pivot (1,1), anchoredPosition (-margin.x, -margin.y)), right aligned, no raycast target. Uses `TextMeshProUGUI` when TMP Essential Resources exist. If not and they cannot be imported by an Editor command (`TMP_PackageResourceImporter`/menu), fall back to `UnityEngine.UI.Text` with `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`, and report it. HudController stays optional; `ControlsHintView` does not depend on it.

## 10. Water ball and roof slabs (Phase 4)
The water ball drop is ALREADY IMPLEMENTED; the description below is kept for reference. The Phase 4 work is the roof slab rework (section 1, RoofSlab) and the camera-relative movement (sections 1 and 5). The roof is four hand-made, non-overlapping RoofSlab boxes of different sizes, one per side, user-built: sizes are kept. `RoofZone` and `RoofSectorMesh` are deleted.

### Water ball, reference
The water is a real physical object.

### WaterDrop (solid ball)
- Prefab: `SphereCollider` (not a trigger, diameter 1, scaled by the Cup), dynamic `Rigidbody` with gravity, layer `WaterBall` (16), tag `WaterDrop`, `M_Water` renderer. The collision matrix (set by ProjectBootstrap) makes WaterBall collide with Building ONLY: it passes through ground, pool, cup, heli, humans and all other layers (so a miss simply falls on through the ground until the kill rules remove it).
- `Launch(float amount01, Vector3 velocity, Vector3 angularVelocity)`: stores Amount01, sets `linearVelocity` and `angularVelocity`.
- `OnCollisionEnter(Collision c)` forwards to `ReportHit(c.collider, c.GetContact(0).point)`.
- `ReportHit(Collider other, Vector3 point)`, first call only (a `_spent` flag):
  - If `other` has a `FireSide` in its parent chain (`GetComponentInParent`, a wall): `FireSide.Extinguish(Amount01)`.
  - Else if `other` has a `RoofSlab` in its parent chain (`GetComponentInParent`): `RoofSlab.FireSide.Extinguish(Amount01)`. No geometry math; the slab itself knows its side.
  - Any other Building collider (no FireSide): no extinguish.
  - Then fires `Hit(this, side)` (side null if none) and destroys the ball.
- `Extinguish` requires `Amount01 >= extinguishMinWater01` (unchanged). A partly filled tank makes a ball that destroys itself without effect. Success sets `Height01 = 0`, starts the pause (`maxStopFireTime`), and clears `RoofOnFire`, so `FillEffectView` smoothing drops both the wall overlay and the roof sector.
- Destroy rules (each by its own mechanism, no polling beyond one check): after the first hit; `Destroy(gameObject, waterBallLifetime)` set in Launch; and below `waterBallKillY` (one `FixedUpdate` compare of `transform.position.y` with the config value, the only per-frame check, which is not a distance poll between objects).
- Event: `event Action<WaterDrop, FireSide> Hit`. Phase 7 splash VFX will subscribe.
- WaterBall collides with Building only, and the RoofSlab colliders are on layer Building, so the ball lands on slabs.
- `WaterCatch` triggers on the FireSides are no longer used by water. Scene-builder disables the colliders. `FireSide` keeps the field.
- Test mode: the ball stays dynamic (it collides with static building colliders, which still work against dynamic bodies). Tests may also call `ReportHit(collider, point)` directly.

### Cup.Tug (reference)
If `Water01 > emptyThreshold`: instantiate `dropPrefab` at the bowl center (inside the tank), set scale to diameter `2*waterBallRadius`, set `Rigidbody.mass = waterBallMass`, call `Launch(Water01, Body.linearVelocity, Body.angularVelocity)`. The ball inherits the bowl's momentum so a drop made in flight travels on. Then `Water01 = 0` immediately (fires `WaterChanged`; the view drains through `fillVisualSmoothTime`), then `ReleaseAll()`. The ball ignores the cup by layer, so it falls through the tank. Tug on an empty or partly filled-below-threshold tank only releases humans. A tank filled between `emptyThreshold` and `extinguishMinWater01` still empties but the ball extinguishes nothing.

### Config fields
CupConfig: waterBallRadius 0.6 [m], waterBallMass 5 [kg], waterBallLifetime 8 [s], waterBallKillY -20 [m]. They supersede `dumpDropRadius` and `dropLifetime` (kept for compatibility, unused). Phase 4 adds FireConfig.roofFireHeight 1.5 [m] (section 7).

### Phase 4 scene (roof slabs)
Under `Roof`: `RoofSlab_North/East/South/West`, each with BoxCollider (non-trigger, layer Building, tag Roof), the `RoofSlab` component, a `RoofFire` plain box child, a `HumanSpawnArea` child (both placed by hand in the RoofSlab prefab; RoofFire has the FireZone trigger collider). `FireSystem.roofSlabs` lists the four; `PlayerControlSource.cameraTransform` is the Main Camera.

## 11. Humans (Phase 5)
Authoritative for Phase 5. Numbers are HumanConfig / CupConfig fields (section 7). The user's assets are kept: `Assets/Models/Human/Animations/HumanAnimator.controller` and `Assets/Prefabs/HumanPrefab.prefab`; scene-builder only adds components and children.

### Prefab and attach points
- Root: `Human`, `HumanAnimator`, one dynamic Rigidbody (mass `HumanConfig.mass`, interpolate), one CapsuleCollider sized to the body. Layer Human and tag Human on the root and every child. The `Hips` collider is removed or disabled. No child has a Rigidbody (animated bones never fight physics).
- Child `TankAttachPoint`: the point that hangs from the tank (hands above the head). Child `FloatPoint`: the point that sits on the water line (chest). Plain Transforms the user can move.
- Cup bowl children (plain Transforms, user-adjustable): `HangSlot_1`, `HangSlot_2` on the rim, `SwimSlot` inside near the top. Cup serialized `Transform[] hangSlots`, `Transform swimSlot`. One human per slot; `maxHumans` (2) counts hanging plus swimming humans together, so a swimming human uses one of the two places.
- Pool children (plain Transforms, user-adjustable): `WaterSurface` (world Y = water level) and `DriftArea` (its scale is the box humans drift in). `PoolZone.SurfaceY`, `PoolZone.DriftBounds`.

### Physics mode per state (set only inside `Human.SetState`)
| State | Rigidbody | Placement |
|---|---|---|
| WaitingOnRoof | dynamic, rotation frozen, upright | stands on a slab |
| RescuedHanging | dynamic, rotation free, `Physics.IgnoreCollision` with its own cup's colliders | ConfigurableJoint from TankAttachPoint to its HangSlot (connected body = bowl); linear x/y/z motion **Locked** (hands stay on the slot, the human cannot fly away), angular limit `hangJointAngularLimitDeg`, spring `hangJointSpring`, damper `hangJointDamper` |
| SwimmingInTank | kinematic, colliders that touch the cup disabled | parented to the cup's swim slot so FloatPoint sits on the slot |
| ChillingInPool | kinematic, upright | moved with `MovePosition` in FixedUpdate: see Pool floating |
| Falling (`IsFalling`, state unchanged) | dynamic, rotation free | after Detach (Tug, ReleaseAll, strength 0); ends on landing |
| Dead | dynamic, then kinematic after `deadSettleTime` (Invoke, no polling) | stays where it died |
Test mode: every body is kinematic and `AttachTo` parents to the slot (section 6). The Falling mode is applied by `Detach` through the same private mode switch that SetState uses.

### Cup.TryGrab results
Rules in section 1 (Cup). Summary: Grabbed (empty tank, WaitingOnRoof, hang slot free, or in-pool ChillingInPool with swim slot free), BouncedFull (tank not empty, WaitingOnRoof or ChillingInPool: push + Stun), Rejected (everything else, including Dead, attached, falling, stunned, capacity reached, lock active).
Stun: the Cup applies `bumpImpulse` away from the bowl, then `Human.Stun()`: `IsStunned` for `stunDuration`, `HumanAnimator.PlayStun()` (Stunned trigger), afterwards the trigger of the human's current state (or the falling trigger if `IsFalling`) plays again. The state does not change.

### Tug and release
`Cup.Tug()` launches the water ball (unchanged, section 10) and calls `ReleaseAll()`. `ReleaseAll()` calls `Detach()` on each attached human and sets its `Body.linearVelocity` to the bowl's `linearVelocity` (angular velocity too). Each becomes Falling. Landing:
- Enters a PoolZone: `PoolZone.ReportEnter` -> `SetState(ChillingInPool)`, `IsFalling` false.
- Collides with a collider whose parent chain has a RoofSlab: `Human.ReportCollision(other)` -> `SetState(WaitingOnRoof)`, stands up upright, `IsFalling` false.
- Touches a GroundFireZone or the fire collider of a burning RoofSlab: `Kill()`.
- All ground outside the pool is GroundFireZone (permanent), so a fall to the ground always ends in Dead.

### Strength (RescuedHanging only)
`TickStrength(dt, accel)`, called from the human's FixedUpdate with `cup.Acceleration.magnitude` (`a`). If `a > accelerationThreshold`: `Strength01 -= (a - accelerationThreshold) * strengthLossFactor * dt / maxStrength`. Otherwise `Strength01 += strengthRecoveryPerSecond / maxStrength * dt`. Clamp 0..1. Strength01 resets to 1 on attach. At 0: cup frees the slot, `Detach()`, Falling. `StrengthChanged` fires only when the value changes. Swimming humans do not drain.

### Pool floating and drifting (ChillingInPool)
In FixedUpdate, `MovePosition` to a position where `FloatPoint.y = SurfaceY + floatHeight + bobAmplitude * sin(2*pi*bobFrequency*t)` (the human root is offset by the FloatPoint local height). Horizontal: wander toward a random target inside `DriftBounds` at `driftSpeed`. A new target is chosen when the human is within `driftArriveDistance` of its target (own target only) or after `driftRetargetTime`. Separation: `Physics.OverlapSphereNonAlloc(driftSeparation)` on layer Human each FixedUpdate steers away from other pool humans; there is no scan over a list of humans. The target is clamped to `DriftBounds`, so the human never leaves DriftArea. Human yaw turns toward the move direction. Works in test mode.

### Animator trigger mapping
`HumanAnimator.Apply(state)` sets a trigger by hash from HumanConfig: WaitingOnRoof -> `waitRescueTrigger` ("WaitRescue"), RescuedHanging -> `hangingTrigger` ("Hanging"), ChillingInPool -> `chillPoolTrigger` ("ChillPool"), SwimmingInTank -> `swimInTankTrigger` ("ChillPool"), Dead -> `deadTrigger` ("Dead"). `PlayStun()` -> `stunnedTrigger` ("Stunned"). Falling plays `fallingTrigger` ("Hanging") from `Detach`. Names are converted with `Animator.StringToHash` once at Initialize. Missing Animator or parameter: one warning, no-op. The Animator is not required.

### Spawning (HumansSystem.Initialize)
1. Disable `scenePreview` (the scene instance of the prefab).
2. For i in 0..`humanCount`-1: slab = `FireSystem.RoofSlabs[i % count]`; `Instantiate(humanPrefab)` at `slab.GetSpawnPoint()`, random yaw, parent `Game/Actors/Humans`; `Initialize(config)`; `SetState(WaitingOnRoof)`.
3. Subscribe to every `StateChanged`, re-raise `AnyStateChanged`. `Humans` lists the spawned instances.
`FireSystem.Initialize` (which initializes the slabs) runs first.
