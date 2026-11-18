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
| Heli | FlameAndBlame.Heli | Injection, Core, Unity.InputSystem | HeliConfig, HeliController, PlayerControlSource, RotorHazard |
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
      Roof                (4 RoofZone triggers, one per side)
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
struct HeliCommand { Vector2 Move; float Vertical; float Yaw; bool TugPressed; }
interface IControlSource { HeliCommand Read(); }
interface ITestModeAware { void EnterTestMode(); void ExitTestMode(); }
interface IKillable { void Kill(); }

// Heli      (all systems below have Initialize(), called by MainEntry)
class PlayerControlSource : MonoBehaviour, IControlSource   // WASD Move, Space/LeftCtrl Vertical, Q/E Yaw, F Tug; InputActions created in code
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
- UI: `UIConfig` ScriptableObject (seventh config asset, created by ProjectBootstrap): `controlsHintText`, `hintFontSize`, `hintMargin`. `ControlsHintView : MonoBehaviour` (UI module, `[Inject] UIConfig`, `Initialize()`) shows the text in a TextMeshProUGUI anchored to the top-right corner. If TMP Essential Resources are missing and cannot be imported through an Editor command, use `UnityEngine.UI.Text` with the built-in `LegacyRuntime.ttf` and report it. MainEntry gets serialized `UIConfig uiConfig` and `ControlsHintView controlsHint`; HudController stays optional.

## Contract additions (Phase 4: physical water ball)
- `WaterDrop` keeps its name and tag but becomes a solid ball: SphereCollider (not a trigger), dynamic Rigidbody with gravity, layer `WaterBall` (16). Collision matrix: WaterBall collides with Building ONLY (ProjectBootstrap sets this). It passes through ground, pool, cup, heli and humans.
- `Cup.Tug()`: if Water01 > emptyThreshold, spawn one ball at the bowl center with radius `CupConfig.waterBallRadius`, mass `CupConfig.waterBallMass`, `linearVelocity` = bowl Rigidbody `linearVelocity` (inherited momentum), angular velocity = bowl angular velocity. Then set Water01 to 0 at once (the view drains visually through its smoothing) and call `ReleaseAll()`.
- `WaterDrop.Launch(float amount01, Vector3 velocity, Vector3 angularVelocity)`; `void ReportHit(Collider other, Vector3 point)` called from OnCollisionEnter with the first contact point. Hit on a wall (parent chain has a FireSide): `FireSide.Extinguish(amount01)`. Hit on the roof slab: extinguish the side whose roof sector contains the point (nearest roof edge). Destroy after the first hit, after `CupConfig.waterBallLifetime`, or below `CupConfig.waterBallKillY`. Fires `event Action<WaterDrop, FireSide> Hit` (null side if none).
- Extinguish resets `Height01` to 0, starts the pause (`maxStopFireTime`), clears RoofOnFire, so the wall and roof shaders fall back through FillEffectView smoothing. `WaterCatch` triggers are no longer used by water; scene-builder disables them.
- Test mode: the ball stays dynamic (it collides with static building colliders). Tests may also call `ReportHit` directly.

