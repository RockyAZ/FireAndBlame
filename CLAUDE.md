# Flame and Blame: shared rules for every agent

Unity 6000.0.65f1, URP, Input System 1.17 ONLY (Active Input Handling = Input System, so the legacy `UnityEngine.Input` class throws at runtime; never use it).
Project path: X:\Unity\Flame and Blame. Spec: `brief.md`. Design: `GAME_DESIGN.md`. Work order: `PHASES.md`.
Do not take screenshots unless the user asks. Verify with logs, console output and `[TEST]` lines.

## Modules, assemblies, folders
Every module is a folder with its own assembly definition and namespace. `Assets/Scripts/Injection/` already exists (asmdef `Injection`, namespace `Injection`: Context, Injector, Inject attribute, Tick). Never edit it.

| Folder (Assets/Scripts/...) | Assembly = namespace | References (by name) | Contains |
|---|---|---|---|
| Injection | Injection | none | existing DI |
| Core | FlameAndBlame.Core | Injection | enums, HeliCommand, IControlSource, ITestModeAware, IKillable |
| Heli | FlameAndBlame.Heli | Injection, Core, VFX, Unity.InputSystem | HeliConfig, HeliController, PlayerControlSource, RotorHazard |
| Fire | FlameAndBlame.Fire | Injection, Core, VFX | FireConfig, BuildingConfig, FireSide, FireSystem, GroundFireZone |
| Rescue | FlameAndBlame.Rescue | Injection, Core, Fire, VFX | CupConfig, HumanConfig, Cup, WaterDrop, Human, HumanAnimator, HumansSystem, PoolZone, RoofZone |
| VFX | FlameAndBlame.VFX | Injection, Core | effect controllers (Play/Stop) |
| GameRules | FlameAndBlame.GameRules | Injection, Core, Heli, Fire, Rescue | GameRulesConfig, GameManager |
| UI | FlameAndBlame.UI | Injection, Core, Rescue, GameRules, UnityEngine.UI, Unity.TextMeshPro | HudController, WaterGauge, HumanStrengthBar, ResultPanel |
| Testing | FlameAndBlame.Testing | all modules | TestModeController, ScriptedControlSource, ScenarioRunner |
| Main | FlameAndBlame.Main | all modules | MainEntry only |
| (Assets/Editor) | FlameAndBlame.EditorTools, Editor platform only | all modules | ProjectBootstrap |
| (Assets/Tests/PlayMode) | FlameAndBlame.Tests.PlayMode | all modules, UnityEngine.TestRunner, UnityEditor.TestRunner | tests |

Rules:
- References always go down the table; no cycles. To call upward, use an event or a Core interface. Gameplay modules never reference Testing or Main.
- Asmdef files are plain JSON text in the module root, named `<Assembly>.asmdef`. `references` lists assembly NAMES as strings (`"Injection"`, `"FlameAndBlame.Core"`), never `"GUID:..."`. Set `rootNamespace` = assembly name, `autoReferenced: false`. Do not write .meta files; Unity creates them.
- Editor asmdef: `includePlatforms: ["Editor"]`. Tests asmdef: `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
- **Text: TextMeshPro only.** Every piece of text (screen UI and world-space) uses `TextMeshProUGUI` (on a Canvas) or `TextMeshPro` (3D), with fields typed `TMP_Text`/`TextMeshProUGUI`. Never use `UnityEngine.UI.Text`, `TextMesh` or the legacy font. TMP ships in `com.unity.ugui` 2.0; its Essential Resources live in `Assets/TextMesh Pro/`. If they are missing, import them (scene-builder) before adding text.
- Compile error "type not found" usually means a missing asmdef reference by name; "cyclic dependency" means a rule above was broken.
- Other folders: Assets/Settings (ScriptableObject assets), Assets/Scenes/Main.unity, Assets/Editor, Assets/Tests/PlayMode.

## Dependency injection (composition root = MainEntry)
- Scene has one GameObject `MainEntry` with ONE component, `MainEntry` (Main module). It has a `[SerializeField]` for every system and every config asset, builds the `Context`, installs them, calls `ApplyInstall()`, then calls each system's `Initialize()` in dependency order, and disposes the context in OnDestroy.
- Systems declare needs with `[Inject] private X _x;`. Install is keyed by concrete type; interfaces use `InstallByType(obj, typeof(IFoo))`.
- Injection happens in `ApplyInstall`, so injected fields are NOT usable in Awake/Start/OnEnable. Use `Initialize()` only. A missing install throws KeyNotFoundException at startup, which is the wiring check.
- One instance per type. Things with many instances (Human, FireSide, zones) are not injected: their owning system (HumansSystem, FireSystem) holds a serialized list and calls `Initialize(config)` on each.
- Configs are serialized on MainEntry and injected into systems.

```csharp
namespace FlameAndBlame.Main {
[DefaultExecutionOrder(-100)]
public class MainEntry : MonoBehaviour {
  [SerializeField] HeliConfig heliConfig;  // plus CupConfig, HumanConfig, FireConfig, BuildingConfig, GameRulesConfig
  [SerializeField] PlayerControlSource playerControl; [SerializeField] HeliController heli; [SerializeField] Cup cup;
  [SerializeField] HumansSystem humans; [SerializeField] FireSystem fire; [SerializeField] GameManager game;
  [SerializeField] HudController hud; [SerializeField] TestModeController testMode; // testMode optional
  Context _ctx;
  void Awake() {
    _ctx = new Context(); var injector = new Injector(_ctx);
    _ctx.Install(injector, heliConfig, /* other configs */ heli, cup, humans, fire, game, hud);
    _ctx.InstallByType(playerControl, typeof(IControlSource));
    if (testMode != null) _ctx.Install(testMode);
    _ctx.ApplyInstall();
    fire.Initialize(); humans.Initialize(); cup.Initialize(); heli.Initialize(); game.Initialize(); hud.Initialize();
    if (testMode != null) testMode.Initialize();
  }
  void OnDestroy() => _ctx?.Dispose();
}}
```

## Layers and tags (created first, by Assets/Editor/ProjectBootstrap.cs)
`ProjectBootstrap` uses `[InitializeOnLoadMethod]`. It is idempotent and adds the tags and layers below if missing, creates any missing config asset with the brief.md defaults in Assets/Settings/, and sets the collision matrix. It runs by itself after the first successful compile.

| Layer (index) | Used by |
|---|---|
| Water (4, exists) | pool water trigger |
| Heli (8) | heli body |
| Rotors (9) | main and tail rotor colliders (crash hazard) |
| Cup (10) | cup segments and bowl |
| Human (11) | humans |
| Building (12) | building walls, roof |
| FireZone (13) | all fire, roof fire and ground fire triggers |
| Ground (14) | ground |
| Obstacle (15) | any other solid |
| WaterBall (16) | the physical water ball dropped by Tug (Phase 4) |

Tags: `Heli`, `Rotor`, `Cup`, `CupGrabZone`, `Human`, `Pool`, `Roof`, `GroundFire`, `BuildingSide`, `WaterDrop`.
Collision matrix: Rotors collides with Building, Ground, Obstacle only. Cup collides with Building, Ground, Obstacle, Human. Human collides with Building, Ground, Obstacle, Cup. FireZone and Water layers hold triggers only.

## Test mode (applies to ALL physical code)
Goal: test logic without flying in real time, without clashing with real physics.
1. Physics callbacks (`OnTriggerEnter`, `OnCollisionEnter`) only FORWARD to public methods. Logic lives in the public methods (`Cup.TryGrab`, `Human.SetState`, `FireSide.Extinguish`, ...). The harness calls the same methods.
2. Physical actors (HeliController, Cup, Human, WaterDrop) implement `ITestModeAware`. EnterTestMode sets the Rigidbody `isKinematic = true` and swaps input to a scripted source. ExitTestMode restores both.
3. Kinematic bodies do not collide with each other, but triggers still fire when one object has a Rigidbody. So everything that matters (fill, grab, fire contact, pool, roof) uses TRIGGER colliders. Only the rotor crash and human bump use real collisions, and both also expose a public method.
4. `Human.AttachTo(Cup)` uses a ConfigurableJoint normally and parents to `cup.HumanAnchor` in test mode. `Detach()` reverses it.
5. HeliController only knows `IControlSource`. `PlayerControlSource` reads the Input System; `ScriptedControlSource` returns commands set by the harness.
6. Test-only code lives in the Testing module. The harness is a system object `TestHarness`, disabled by default, installed into MainEntry only when assigned.
7. Tests print `[TEST] <name> PASS` or `[TEST] <name> FAIL <reason>`.

## Scene hierarchy (Assets/Scenes/Main.unity)
```
MainEntry                 (the MainEntry component only)
Game
  Environment
    Ground
    Building              (Side_North/East/South/West: FireSide + burn trigger, tag BuildingSide)
      Roof                (RoofSlab_North/East/South/West: RoofSlab component, BoxCollider on Building, tag Roof; children RoofFire, HumanSpawnArea)
    Pool                  (PoolZone trigger on layer Water, tag Pool)
    GroundFire            (GroundFireZone triggers, tag GroundFire)
  Systems                 (FireSystem, HumansSystem, GameManager, HudController)
  Actors
    Heli                  (HeliController, PlayerControlSource, Rigidbody; children Body, MainRotor, TailRotor)
      Cup                 (Cup, joint chain, HumanAnchor, GrabZone)
    Humans                (Human_01 .. Human_N)
  UI                      (Canvas: WaterGauge, ResultPanel, world-space strength bars)
TestHarness               (disabled; TestModeController, ScriptedControlSource, ScenarioRunner)
```

## Interface contract (names fixed; change only by editing this file)
Enums (Core): `HumanState { WaitingOnRoof, RescuedHanging, ChillingInPool, SwimmingInTank, Dead }`, `Side { North, East, South, West }`, `GameResult { Playing, Won, Lost }`, `GrabResult { Grabbed, BouncedFull, Rejected }`.
Configs (ScriptableObject, `[CreateAssetMenu]`, assets in Assets/Settings/, defaults in brief.md): `HeliConfig`, `CupConfig`, `HumanConfig`, `FireConfig`, `BuildingConfig`, `GameRulesConfig`. Gameplay code reads numbers only from these.

```csharp
// Core
struct HeliCommand { Vector2 Move; Vector3 MoveWorld; float Vertical; float Yaw; bool TugPressed; }   // MoveWorld added in Phase 4
interface IControlSource { HeliCommand Read(); }
interface ITestModeAware { void EnterTestMode(); void ExitTestMode(); }
interface IKillable { void Kill(); }

// Heli      (all systems below have Initialize(), called by MainEntry)
class PlayerControlSource : MonoBehaviour, IControlSource   // WASD Move (camera-relative, see Phase 4), Space/LeftCtrl Vertical, Z/X Yaw, F Tug; InputActions created in code
class HeliController : MonoBehaviour, ITestModeAware        // [Inject] HeliConfig, IControlSource
    Rigidbody Body; void SetControlSource(IControlSource s); void Crash(); bool IsCrashed; event Action Crashed;
class RotorHazard : MonoBehaviour   // Rotors layer; collision/trigger -> ReportHit(Collider other): ignores Cup and Human, else HeliController.Crash()

// Fire
class FireSide : MonoBehaviour      // Initialize(FireConfig)
    Side Side; float Height01; bool IsBurning; bool RoofOnFire; bool IsLost;
    void Extinguish(float amount01);  // needs amount01 >= FireConfig.extinguishMinWater01; Height01 = 0, paused FireConfig.maxStopFireTime
    void SetLost(); event Action<FireSide> ReachedTop;      // burn trigger calls IKillable.Kill() on contact
class FireSystem : MonoBehaviour    // [Inject] FireConfig; serialized FireSide[4]
    IReadOnlyList<FireSide> Sides; int SidesNotOnFire; void SetAllLost(); event Action AllSidesBurning;
class GroundFireZone : MonoBehaviour // trigger; IKillable entering -> Kill()

// Rescue
class Cup : MonoBehaviour, ITestModeAware   // [Inject] CupConfig
    Rigidbody Body; Transform HumanAnchor; float Water01; bool IsEmpty, IsFull; int AttachedCount; Vector3 Acceleration;
    void FillTick(float dt); GrabResult TryGrab(Human h); void Tug();   // Tug = WaterDrop launch + ReleaseAll
    void ReleaseAll(); event Action<float> WaterChanged;
class WaterDrop : MonoBehaviour, ITestModeAware   // tag WaterDrop; Launch(float amount01, Vector3 inheritedVelocity); trigger on FireSide -> Extinguish
class Human : MonoBehaviour, ITestModeAware, IKillable   // Initialize(HumanConfig)
    HumanState State; float Strength01; Rigidbody Body;
    void SetState(HumanState s);      // the only place state changes; calls HumanAnimator.Apply
    void AttachTo(Cup cup); void Detach(); void Kill(); void Stun();
    event Action<Human, HumanState> StateChanged;
class HumanAnimator : MonoBehaviour   // Apply(HumanState): SetTrigger(name from HumanConfig) only if an Animator exists, else no-op
class HumansSystem : MonoBehaviour    // [Inject] HumanConfig; serialized List<Human>
    IReadOnlyList<Human> Humans; int CountIn(HumanState s); void KillAll(); event Action<Human, HumanState> AnyStateChanged;
class PoolZone : MonoBehaviour        // trigger: Cup inside -> FillTick; Human entering unattached -> SetState(ChillingInPool); grab by mistake via Cup.TryGrab (state 4)
class RoofZone : MonoBehaviour        // trigger per side: released human -> SetState(WaitingOnRoof); human inside while FireSide.RoofOnFire -> Kill()

// GameRules
class GameManager : MonoBehaviour     // [Inject] HeliController, HumansSystem, FireSystem, GameRulesConfig
    GameResult Result; int Score; event Action<GameResult> ResultChanged;
    // Heli crash -> Lost: HumansSystem.KillAll(), FireSystem.SetAllLost(). All sides burning -> Lost.
    // Won when CountIn(WaitingOnRoof)+CountIn(RescuedHanging)+CountIn(SwimmingInTank) == 0.

// UI (read events only): HudController [Inject] Cup, HumansSystem, GameManager; WaterGauge; HumanStrengthBar; ResultPanel

// Testing: TestModeController (Initialize, EnterAll/ExitAll over ITestModeAware under Game), ScriptedControlSource : IControlSource (Set(HeliCommand)), ScenarioRunner
```

## Contract additions (Phase 0 design; the contract above is unchanged, these only add members)
Full behaviour in GAME_DESIGN.md.
- Cup: `[Inject] IControlSource` (reads TugPressed in Update, ignored in test mode; Heli cannot call Rescue); `bool InPool; void SetInPool(bool)` (set by PoolZone); serialized `Rigidbody anchorBody` (heli body), `WaterDrop dropPrefab`; `Initialize()` builds the joint chain from CupConfig.
- WaterDrop: `void ReportHit(Collider other)`. Human: `bool IsStunned; event Action<float> StrengthChanged; void TickStrength(float dt, float acceleration)`.
- FireSide: `void Advance(float dt)` (called by its own Update; tests call it), `float PauseTimeLeft`, `void ApplyBuilding(BuildingConfig)`; two child colliders: burn trigger band (kills) and water catch trigger (full height).
- FireSystem: `[Inject] BuildingConfig`, serialized `GroundFireZone[]`; GroundFireZone: `Initialize(FireConfig)`, `void Extinguish(float amount01)` (only used when groundFirePermanent is false).
- Zones forward physics via public `ReportEnter(Collider)`, `ReportStay(Collider)`, `ReportExit(Collider)` where relevant (PoolZone, RoofZone, GroundFireZone, FireSide burn trigger, Cup grab zone).
- Config classes also hold extra fields listed in GAME_DESIGN.md section 7; `PoolConfig` is a [Serializable] class inside GameRulesConfig.
- Layer interactions needed beyond the matrix above (triggers): Human x FireZone, Human x Water, Cup x Water, Cup x Human (grab zone), Water(drop) x FireZone/Building/Ground/Obstacle. WaterDrop uses layer Water.
- GameManager: Won also requires at least one human in ChillingInPool, otherwise Lost.

## Contract additions (Phase 1 camera and Phase 2)
- Core: `ICameraInputSource { Vector2 Look; float Zoom; }` (mouse delta pixels, scroll delta). Implemented by `PlayerControlSource` (also installed by type in MainEntry). `HeliCommand` unchanged.
- Heli: asmdef also references `Unity.Cinemachine`. `HeliCameraController` ([Inject] HeliConfig, ICameraInputSource, HeliController; serialized `CinemachineCamera`; `Initialize()`; read-only `Yaw`, `Pitch`, `Distance`). Needs CinemachineOrbitalFollow + CinemachineRotationComposer on the CinemachineCamera and NO InputAxisController. Ignores input while heli is in test mode. MainEntry field `heliCamera` is optional; initialized after heli.
- HeliConfig camera fields: cameraYawSensitivity, cameraPitchSensitivity, cameraMinPitch, cameraMaxPitch, cameraDefaultPitch, cameraMinDistance, cameraMaxDistance, cameraDefaultDistance, cameraZoomSpeed, cameraZoomSmoothTime, cameraFollowDamping.
- Rescue: `PoolZone` has serialized `Cup cup` (the bowl is reparented at runtime, so it cannot be found from the collider). `RoofZone.OnFire`. `WaterDrop.Amount01`; prefab sphere trigger diameter 1 (Cup scales it to 2*dumpDropRadius).
- Fire: `FireSideTrigger` (on the burn band child, serialized FireSide, forwards to ReportEnter/Stay). FireSide serialized `BoxCollider burnBand, waterCatch`; local X along the wall, local +Z outward, origin at wall base center.

## Contract additions (Phase 3: fire and water visuals, pool filling, controls hint)
- One URP shader for fire and water: `Assets/Shaders/FillEffect.shader` (hand-written HLSL, URP Unlit, Transparent, no Shader Graph). Properties: `_Fill` (0..1), `_Intensity` (0..1), `_FillColor`, `_EdgeColor`, `_BaseColor` (color above the fill line; alpha 0 for fire overlays), `_EdgeWidth`, `_NoiseScale`, `_NoiseSpeed`, `_MinY`, `_MaxY` (object-space Y range the fill runs over). A pixel is "filled" when its normalized object-space Y is below `_Fill` plus animated noise near the edge, so the fill line flickers like flames.
- Materials in `Assets/VFX/`: `M_Fire` (orange to yellow, `_BaseColor` alpha 0), `M_Water` (blue, `_BaseColor` light grey, semi-transparent). Look values live on the materials; gameplay numbers stay in the ScriptableObjects.
- VFX module: `FillEffectView : MonoBehaviour` ([RequireComponent(Renderer)]). `void SetFill(float fill01)`, `void SetIntensity(float intensity01)`, `float SmoothTime` (serialized). Uses a MaterialPropertyBlock (never instantiates materials), smooths toward the target with SmoothDamp, and sets `_MinY`/`_MaxY` from the mesh bounds on Awake. This overrides the vfx-artist "Play/Stop only" rule for this class.
- VFX module: `RoofSectorMesh : MonoBehaviour`. Builds a triangle mesh in Awake (and OnValidate) from the roof center to the two roof corners of its `Side`, using `BuildingConfig` passed in serialized or `Initialize(BuildingConfig)`. The four sectors do not overlap.
- Fire: `FireSide` serialized `FillEffectView wallFireView`, drives `SetFill(Height01)` every frame and `SetIntensity(IsBurning ? 1 : 0)`. `RoofZone` serialized `FillEffectView roofFireView`, drives `SetFill(OnFire ? 1 : 0)` with `FireConfig.roofIgniteTime` as the smoothing time.
- Scene: each `Side_X` gets a child `FireOverlay` (thin box or quad, no collider, `M_Fire`, `FillEffectView`) 0.05 m outside its wall (local +Z), full building height. `Roof` gets 4 children `RoofFire_North/East/South/West` (no collider, `RoofSectorMesh`, `M_Fire`, `FillEffectView`) 0.05 m above the slab.
- Rescue: the cup bowl renderer uses `M_Water` and a `FillEffectView`. `Cup` serialized `FillEffectView waterView`, calls `SetFill(Water01)` from `WaterChanged`, smoothing `CupConfig.fillVisualSmoothTime`. The pool water surface uses `M_Water` with `_Fill` 1.
- UI: `UIConfig` ScriptableObject (seventh config asset, created by ProjectBootstrap): `controlsHintText`, `hintFontSize`, `hintMargin`. `ControlsHintView : MonoBehaviour` (UI module, `[Inject] UIConfig`, `Initialize()`) shows the text in a TextMeshProUGUI anchored to the top-right corner. (TMP only; see the Text rule at the top.) MainEntry gets serialized `UIConfig uiConfig` and `ControlsHintView controlsHint`; HudController stays optional.

## Contract additions (Phase 4 water ball: already implemented, kept for reference)
- `WaterDrop` keeps its name and tag but becomes a solid ball: SphereCollider (not a trigger), dynamic Rigidbody with gravity, layer `WaterBall` (16). Collision matrix: WaterBall collides with Building ONLY (ProjectBootstrap sets this). It passes through ground, pool, cup, heli and humans.
- `Cup.Tug()`: if Water01 > emptyThreshold, spawn one ball at the bowl center with radius `CupConfig.waterBallRadius`, mass `CupConfig.waterBallMass`, `linearVelocity` = bowl Rigidbody `linearVelocity` (inherited momentum), angular velocity = bowl angular velocity. Then set Water01 to 0 at once (the view drains visually through its smoothing) and call `ReleaseAll()`.
- `WaterDrop.Launch(float amount01, Vector3 velocity, Vector3 angularVelocity)`; `void ReportHit(Collider other, Vector3 point)` called from OnCollisionEnter with the first contact point. Hit on a wall (parent chain has a FireSide): `FireSide.Extinguish(amount01)`. Hit on the roof slab: extinguish the side whose roof sector contains the point (nearest roof edge). Destroy after the first hit, after `CupConfig.waterBallLifetime`, or below `CupConfig.waterBallKillY`. Fires `event Action<WaterDrop, FireSide> Hit` (null side if none).
- Extinguish resets `Height01` to 0, starts the pause (`maxStopFireTime`), clears RoofOnFire, so the wall and roof shaders fall back through FillEffectView smoothing. `WaterCatch` triggers are no longer used by water; scene-builder disables them.
- Test mode: the ball stays dynamic (it collides with static building colliders). Tests may also call `ReportHit` directly.

## Contract additions (Phase 4: roof slabs). These replace RoofZone and RoofSectorMesh everywhere above.
- Scene: the four sides are instances of one `Building_Side` prefab. Under `Roof` there are four hand-made, non-overlapping `RoofSlab` boxes of different sizes, one per side. Keep their sizes.
- `RoofSectorMesh` is deleted. `RoofZone` is deleted; read `RoofSlab` wherever the contract says RoofZone.
- Fire module: `RoofSlab : MonoBehaviour, on the RoofSlab object` (non-trigger BoxCollider, layer Building, tag Roof).
  - Serialized: `FireSide fireSide`, `FillEffectView roofFireView` (on the RoofFire child), `Transform humanSpawnArea`.
  - `void Initialize(FireConfig fire, BuildingConfig building)`, called by FireSystem. It only sets `roofFireView.SmoothTime = roofIgniteTime`. It never moves or sizes RoofFire or `humanSpawnArea`: the user places them by hand in the `Assets/Prefabs/RoofSlab` prefab (edit the prefab to change all four roofs at once).
  - `FireSide FireSide; bool OnFire;` OnFire follows `fireSide.RoofOnFire` (set when the side reaches the top, cleared by Extinguish). Drives `roofFireView.SetFill(OnFire ? 1 : 0)`.
  - `void ReportStay(Collider other)`: while OnFire, an `IKillable` touching the RoofFire collider is killed (attached/hanging humans too). There is no other kill volume; `RoofSlabTrigger` on RoofFire forwards OnTriggerEnter/Stay to it.
  - `Bounds SpawnBounds; Vector3 GetSpawnPoint()` returns a random point on the bottom of the spawn box, in world space. Draws the spawn box with `OnDrawGizmos`.
  - `RoofFire` child: box with `M_Fire`, `FillEffectView`, a trigger BoxCollider (layer FireZone) and `RoofSlabTrigger` (finds its RoofSlab with GetComponentInParent). The four slabs are instances of `Assets/Prefabs/RoofSlab.prefab`.
- `FireSystem`: serialized `RoofSlab[] roofSlabs`, initializes them, exposes `IReadOnlyList<RoofSlab> RoofSlabs` (HumansSystem uses it in Phase 5 for spawning).
- `WaterDrop.ReportHit`: a hit on a collider whose parent chain has a `RoofSlab` extinguishes `RoofSlab.FireSide` (this replaces "nearest roof edge").
- `FireConfig.roofFireHeight` default 1.5.
- Phase 5 human rules that mentioned RoofZone use RoofSlab: a released human landing on a slab becomes WaitingOnRoof; a human on a burning slab dies.

## Contract additions (Phase 4: camera-relative movement and Z/X turning)
- `HeliCommand.MoveWorld`: the horizontal move direction in world space (y = 0, magnitude 0..1). `Move` stays the raw WASD vector (x = A/D, y = W/S) for debugging. HeliController moves along `MoveWorld` only, never relative to its own yaw.
- `PlayerControlSource` has a serialized `Transform cameraTransform` (the Main Camera that the CinemachineBrain drives). Each Read(): forward = camera forward projected on the horizontal plane and normalized; right = camera right projected and normalized. If forward is near zero (camera looking straight down), use the projected camera up. `MoveWorld = ClampMagnitude(forward * Move.y + right * Move.x, 1)`. If cameraTransform is null, use world Z as forward and world X as right, and log one warning.
- Turning keys: Z = Yaw -1 (counterclockwise seen from above), X = Yaw +1 (clockwise seen from above). Q and E are removed. Yaw drift stays.
- `ScriptedControlSource`: tests set `MoveWorld` directly. If only `Move` is set, it maps x to world X and y to world Z.
- Input smoothing (inputRiseTime, inputFallTime) applies to MoveWorld in world space. The camera orbit stays in world space, so turning the heli does not turn the camera or change the move direction.
- UIConfig default `controlsHintText`: "WASD move (camera) | Space/Ctrl up/down | Z/X turn | F drop water | Mouse look | Wheel zoom".

## Contract additions (Phase 5: humans). These override earlier human lines where they differ.
User assets (keep them, only add to them): `Assets/Models/Human/Animations/HumanAnimator.controller` (triggers from Any State: `Stunned`, `Hanging`, `ChillPool`, `WaitRescue`, `Dead`) and `Assets/Prefabs/HumanPrefab.prefab` (model variant, Animator set, Apply Root Motion off, collider on child `Hips`). One preview instance of the prefab is in Main.unity.

Prefab setup:
- Root: `Human`, `HumanAnimator`, one dynamic `Rigidbody` (mass `HumanConfig.mass`, interpolate), one `CapsuleCollider` sized to the body. Layer Human, tag Human on the root and all children.
- The Hips collider is removed or disabled. No child has a Rigidbody, so the animated bones never fight the physics.
- Child `TankAttachPoint`: the point on the human that hangs from the tank (hands above the head). Child `FloatPoint`: the point that sits on the water line (chest). Both are plain Transforms the user moves by hand to adjust the look.

Cup bowl children (plain Transforms, user-adjustable): `HangSlot_1`, `HangSlot_2` on the rim, `SwimSlot` inside near the top. Cup serialized `Transform[] hangSlots`, `Transform swimSlot`. A slot holds at most one human; `maxHumans` (2) counts hanging and swimming humans together.

Pool children (plain Transforms, user-adjustable): `WaterSurface` (its world Y is the water level), `DriftArea` (its scale is the box humans drift inside). PoolZone serialized `Transform waterSurface, driftArea`; `float SurfaceY`; `Bounds DriftBounds`.

Physics mode per state (set only inside `Human.SetState`):
| State | Rigidbody | Placement |
|---|---|---|
| WaitingOnRoof | dynamic, rotation frozen, upright | stands on a slab |
| RescuedHanging | dynamic, rotation free, collisions with its own cup ignored (Physics.IgnoreCollision) | ConfigurableJoint from TankAttachPoint to its HangSlot; limits, spring and damper from HumanConfig |
| SwimmingInTank | kinematic, colliders that hit the cup disabled | parented to the cup's free slot so FloatPoint sits on the slot |
| ChillingInPool | kinematic, upright | moved with MovePosition in FixedUpdate: FloatPoint height = SurfaceY + floatHeight + bob (sin, bobAmplitude, bobFrequency); wanders toward random targets inside DriftBounds at driftSpeed, picks a new target when close or after driftRetargetTime, keeps driftSeparation from other pool humans |
| Falling (no enum value, `bool IsFalling`) | dynamic, rotation free | after release or strength zero; ends on landing |
| Dead | dynamic, then kinematic when it comes to rest | stays where it died |
Test mode keeps the earlier rule: kinematic everywhere, attach by parenting to the slot.

Transitions (all physics callbacks only forward to public methods):
- Cup grab zone touches a human (`Cup.TryGrab`): tank empty (Water01 <= emptyThreshold), human WaitingOnRoof, a hang slot free gives RescuedHanging. Tank not empty and human WaitingOnRoof or ChillingInPool outside the pool fill: `Stun()` (impulse bumpImpulse away from the cup, Stunned trigger for stunDuration, then the state's trigger again) and returns BouncedFull. Tank inside the pool (`Cup.InPool`) and human ChillingInPool and a slot free gives SwimmingInTank. Otherwise Rejected.
- `Cup.Tug()` / `ReleaseAll()`: every attached human detaches, becomes Falling, and gets the bowl's velocity.
- Landing while Falling: inside a PoolZone gives ChillingInPool; collision with a collider whose parent chain has a RoofSlab gives WaitingOnRoof (stand up upright); touching a GroundFireZone or a burning RoofSlab fire collider gives Dead (Kill()).
- Strength (RescuedHanging only): if |Cup.Acceleration| > accelerationThreshold, Strength01 -= (|a| - threshold) * strengthLossFactor * dt / maxStrength; otherwise it recovers by strengthRecoveryPerSecond / maxStrength. At 0 the human detaches and becomes Falling. Strength resets to 1 on attach.

Animator (`HumanAnimator.Apply(HumanState)` and `PlayStun()`): HumanConfig holds the trigger names: waitRescueTrigger "WaitRescue", hangingTrigger "Hanging", chillPoolTrigger "ChillPool", swimInTankTrigger "ChillPool" (no separate clip yet), deadTrigger "Dead", stunnedTrigger "Stunned", fallingTrigger "Hanging". Uses Animator.StringToHash. If the Animator or a parameter is missing, it logs one warning and does nothing.

Spawning: `HumansSystem` ([Inject] HumanConfig, FireSystem) serialized `Human humanPrefab` (the prefab ASSET) and `GameObject scenePreview` (the scene instance, disabled in Initialize). Initialize instantiates `humanCount` humans round-robin over `FireSystem.RoofSlabs` at `GetSpawnPoint()` with a random yaw, parents them under `Game/Actors/Humans`, calls `Initialize(config)` and sets WaitingOnRoof. `IReadOnlyList<Human> Humans`.

## Contract additions (Phase 6: rules, UI, auto-align). Z/X turning from Phase 4 is replaced by auto-align when `autoAlignToCamera` is true.
- Core: `HeliCommand.FaceWorld` (horizontal world direction the nose should face; zero = no request). `PlayerControlSource` sets it to the flattened camera forward.
- HeliConfig: `autoAlignToCamera` (true), `alignMaxYawRate` (90 deg/s), `alignGain` (3 1/s), `alignSmoothTime` (0.25 s). HeliController yaws only around world up; manual Z/X ignored while aligning; yaw drift stays as a disturbance.
- GameManager: `int Rescued, Deaths, TotalHumans` (read-only), `void Evaluate()`. Score and result as GAME_DESIGN.md.
- UI: `HumanStrengthBar.Bind(Human)` (one per HumanPrefab, bound by HudController), `WaterGauge.Bind(Cup)` (world-space, TextMeshProUGUI), `CupMotionIndicator.Bind(Cup, float threshold)` (world-space shake meter), `ResultPanel.Show(GameResult, int score, int rescued, int total, int deaths)`. HudController: [Inject] Cup, HumansSystem, GameManager, HumanConfig; serialized `waterGauge`, `motionIndicator`, `resultPanel`.
- UIConfig default hint: "WASD move (camera) | Space/Ctrl up/down | Heli faces camera | F drop water | Mouse look | Wheel zoom".

## Contract additions (Phase 7: crash, shake, effects)
- Heli: `HeliController` forwards rotor contacts from its own OnCollisionEnter (`contact.thisCollider` with a RotorHazard) to `RotorHazard.ReportHit`. `Crash()` breaks the heli meshes into dynamic debris (not in test mode). HeliConfig: `breakImpulse`, `breakSpin`, `breakPartMass`, `debrisLifetime`.
- Rescue: `Cup.ShakeMagnitude` (smoothed shake, used for the strength drain and the shake meter), `Cup.ResetShake()`. CupConfig: `shakeVelocitySmoothing`, `shakeMaxRawAcceleration`, `shakeRiseTime`, `shakeFallTime`, `shakeTeleportSpeed`, `grabGraceTime`. HumanConfig: `strengthGraceTime`. `WaterDrop`: `event Action<WaterDrop, FireSide> Hit` (null side if nothing was extinguished).
- VFX: `EffectController` base with `Play()`/`Stop()` only; FireEffect, SmokeEffect, GroundFireEffect, WaterDropEffect, SplashEffect, StunStarsEffect. Optional serialized refs: FireSide `fireEffect`/`smokeEffect`, RoofSlab `roofFireEffect`/`roofSmokeEffect`, GroundFireZone `groundFireEffect`, WaterDrop `trailEffect`/`splashPrefab`, Human `stunStars`, Cup `tugSplashPrefab`.
- Look: `Assets/Shaders/ToonOutline.shader` (toon lighting, UV-border wire, inverted-hull outline, no geometry shader) for the environment and heli materials in `Assets/Materials/`. Fire and water keep FillEffect.

## Contract additions (Phase 8). These override earlier lines where they differ.
Water drop look:
- `Assets/Materials/M_WaterDrop.mat`: URP Lit, Surface Type Transparent, Blend Alpha, base color blue (about 0.2, 0.55, 1.0) with alpha 0.45, smoothness 0.9, metallic 0, emission off. It is assigned to the WaterDrop prefab's renderer. Look values live on the material.

Proportional extinguishing (replaces the extinguishMinWater01 rule):
- The drop carries `amount01` = the tank's Water01 at the moment of the drop (Cup.Tug passes it to WaterDrop.Launch).
- `FireSide.Extinguish(float amount01)`: if the side is lost, do nothing. Otherwise `Height01 = Height01 * (1 - amount01)`, so a full tank (1) removes all fire, 0.2 removes 20% of the current fire, and any amount above 0 has an effect. Start a pause of `maxStopFireTime * amount01` (keep the longer one if a pause is already running). After the pause the fire grows again from the new height.
- `FireConfig.extinguishMinWater01` is no longer read. Keep the field so the asset still loads; mark it `[Obsolete]` in a comment or tooltip.
- Roof: `RoofSlab` gets `float Fire01` (0..1). While its side has reached the top and is not paused, Fire01 grows toward 1 over `roofIgniteTime`. Water on the side or on the slab sets `Fire01 *= (1 - amount01)`. `OnFire` means `Fire01 > FireConfig.roofFireKillThreshold` (default 0.1); only then does the slab kill. `roofFireView.SetFill(Fire01)`, and the roof fire and smoke effects play while OnFire.
- New FireConfig field: `roofFireKillThreshold` 0.1.

Rotor blur (visual only):
- `Assets/Shaders/RotorBlur.shader`: hand-written HLSL, URP Unlit, Transparent, Cull Off, ZWrite Off, drawn on a flat disc mesh (a Quad, UV center at 0.5, 0.5). Properties: `_BladeCount` (int, default 4), `_Speed` (turns per second, set by script), `_Angle` (current angle in turns, set by script), `_BladeColor`, `_DiscColor` (faint, alpha about 0.08), `_BladeWidth`, `_Smear` (how far each blade's streak trails, in turns), `_HubRadius`, `_TipFade`. In polar UV coordinates it draws `_BladeCount` streaks that trail behind each blade (exponential falloff over `_Smear`), plus the faint disc, fading near the hub and the tip, so it reads as fast-spinning blurred blades.
- Materials `M_RotorBlur_Main` and `M_RotorBlur_Tail` in `Assets/Materials/`.
- VFX module: `RotorBlurView : MonoBehaviour` ([RequireComponent(Renderer)]). `void SetSpeed(float turnsPerSecond)`, `void SetVisible(bool)`. It accumulates `_Angle` in Update with a MaterialPropertyBlock (no material instances), so the speed can change without the pattern jumping.
- HeliController: serialized `RotorBlurView mainRotorBlur, tailRotorBlur` and `Renderer[] bladeRenderers` (the real blade meshes). In Initialize it hides the blade renderers and shows the blur discs. It sets the speed every frame from HeliConfig: `mainRotorTurnsPerSecond` (default 6), `tailRotorTurnsPerSecond` (default 12), `rotorIdleFactor` (0.8, used when there is no input) and `rotorLoadFactor` (1.2, used at full climb input). On Crash it hides the blur discs and shows the blade renderers again, before the break-apart, so the debris shows real blades.
- The rotor colliders and RotorHazard do not change. The blur discs have no colliders.

