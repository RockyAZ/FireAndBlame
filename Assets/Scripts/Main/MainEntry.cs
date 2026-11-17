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

		[Header("Systems")]
		[SerializeField] private PlayerControlSource playerControl;
		[SerializeField] private HeliController heli;
		[SerializeField] private Cup cup;
		[SerializeField] private HumansSystem humans;
		[SerializeField] private FireSystem fire;
		[SerializeField] private GameManager game;
		[SerializeField] private HudController hud;
		[SerializeField] private TestModeController testMode; // optional

		private Context _ctx;

		private void Awake()
		{
			_ctx = new Context();
			var injector = new Injector(_ctx);
			_ctx.Install(injector, heliConfig, cupConfig, humanConfig, fireConfig, buildingConfig, gameRulesConfig,
				heli, cup, humans, fire, game, hud);
			_ctx.InstallByType(playerControl, typeof(IControlSource));
			if (testMode != null) _ctx.Install(testMode);
			_ctx.ApplyInstall();

			fire.Initialize();
			humans.Initialize();
			cup.Initialize();
			heli.Initialize();
			game.Initialize();
			hud.Initialize();
			if (testMode != null) testMode.Initialize();
		}

		private void OnDestroy() => _ctx?.Dispose();
	}
}
