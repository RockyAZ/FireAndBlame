using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.GameRules;
using FlameAndBlame.Heli;
using FlameAndBlame.Rescue;
using FlameAndBlame.Testing;
using FlameAndBlame.UI;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Main
{
	[DefaultExecutionOrder(-100)]
	public class MainEntry : MonoBehaviour
	{
		[Header("Configs")]
		[SerializeField] private HeliConfig heliConfig;
		[SerializeField] private CupConfig cupConfig;
		[SerializeField] private HumanConfig humanConfig;
		[SerializeField] private FireConfig fireConfig;
		[SerializeField] private BuildingConfig buildingConfig;
		[SerializeField] private GameRulesConfig gameRulesConfig;
		[SerializeField] private UIConfig uiConfig;     // optional

		[Header("Systems")]
		[SerializeField] private PlayerControlSource playerControl;
		[SerializeField] private HeliController heli;
		[SerializeField] private Cup cup;
		[SerializeField] private HeliCameraController heliCamera; // optional
		[SerializeField] private HumansSystem humans;   // Phase 3
		[SerializeField] private FireSystem fire;       // Phase 2
		[SerializeField] private GameManager game;      // Phase 4
		[SerializeField] private HudController hud;     // Phase 4
		[SerializeField] private ControlsHintView controlsHint; // optional
		[SerializeField] private TestModeController testMode; // optional

		private Context _ctx;

		private void Awake()
		{
			_ctx = new Context();
			var injector = new Injector(_ctx);
			_ctx.Install(injector);
			// Unassigned objects are skipped with a warning, so a partially built scene still starts.
			// A system that needs a skipped object throws KeyNotFoundException at ApplyInstall.
			Add(heliConfig, "heliConfig"); Add(cupConfig, "cupConfig"); Add(humanConfig, "humanConfig");
			Add(fireConfig, "fireConfig"); Add(buildingConfig, "buildingConfig"); Add(gameRulesConfig, "gameRulesConfig");
			Add(heli, "heli"); Add(cup, "cup"); Add(humans, "humans"); Add(fire, "fire"); Add(game, "game"); Add(hud, "hud");
			Add(heliCamera, "heliCamera"); Add(uiConfig, "uiConfig"); Add(controlsHint, "controlsHint");
			if (playerControl != null)
			{
				_ctx.InstallByType(playerControl, typeof(IControlSource));
				_ctx.InstallByType(playerControl, typeof(ICameraInputSource));
			}
			else Debug.LogWarning("MainEntry: playerControl is not assigned, skipped.");
			if (testMode != null) _ctx.Install(testMode);
			_ctx.ApplyInstall();

			if (fire != null) fire.Initialize();
			if (humans != null) humans.Initialize();
			if (cup != null) cup.Initialize();
			if (heli != null) heli.Initialize();
			if (heliCamera != null) heliCamera.Initialize();
			if (game != null) game.Initialize();
			if (hud != null) hud.Initialize();
			if (controlsHint != null) controlsHint.Initialize();
			if (testMode != null) testMode.Initialize();
		}

		private void Add(Object obj, string field)
		{
			if (obj != null) _ctx.Install(obj);
			else Debug.LogWarning("MainEntry: " + field + " is not assigned, skipped.");
		}

		private void OnDestroy() => _ctx?.Dispose();
	}
}
