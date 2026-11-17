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
