using FlameAndBlame.Core;
using FlameAndBlame.GameRules;
using FlameAndBlame.Rescue;
using Injection;
using UnityEngine;

namespace FlameAndBlame.UI
{
	public class HudController : MonoBehaviour
	{
		[Inject] private Cup _cup;
		[Inject] private HumansSystem _humans;
		[Inject] private GameManager _game;
		[Inject] private HumanConfig _humanConfig;

		[SerializeField] private WaterGauge waterGauge;
		[SerializeField] private CupMotionIndicator motionIndicator;
		[SerializeField] private ResultPanel resultPanel;
		[Tooltip("Optional. Esc pause menu; also owns the cursor (locked in play, free on menus).")]
		[SerializeField] private PauseMenu pauseMenu;

		public void Initialize()
		{
			if (waterGauge != null) waterGauge.Bind(_cup);
			else Debug.LogWarning("HudController: waterGauge not assigned.", this);
			if (motionIndicator != null) motionIndicator.Bind(_cup, _humanConfig.accelerationThreshold);
			else Debug.LogWarning("HudController: motionIndicator not assigned.", this);
			// Humans are spawned at runtime, so each carries its own bar (child of the prefab).
			foreach (var h in _humans.Humans)
				if (h != null) h.GetComponentInChildren<HumanStrengthBar>(true)?.Bind(h);
			if (pauseMenu != null) pauseMenu.Bind(_game);
			else Debug.LogWarning("HudController: pauseMenu not assigned.", this);
			if (resultPanel != null) _game.ResultChanged += OnResult;
			else Debug.LogWarning("HudController: resultPanel not assigned.", this);
		}

		private void OnDestroy()
		{
			if (_game != null) _game.ResultChanged -= OnResult;
		}

		private void OnResult(GameResult result) =>
			resultPanel.Show(result, _game.Score, _game.Rescued, _game.TotalHumans, _game.Deaths);
	}
}
