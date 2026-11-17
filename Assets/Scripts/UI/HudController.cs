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

		[SerializeField] private WaterGauge waterGauge;
		[SerializeField] private HumanStrengthBar[] strengthBars;
		[SerializeField] private ResultPanel resultPanel;

		public void Initialize() { }
	}
}
