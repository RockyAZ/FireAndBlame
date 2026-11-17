using System;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.Heli;
using FlameAndBlame.Rescue;
using Injection;
using UnityEngine;

namespace FlameAndBlame.GameRules
{
	public class GameManager : MonoBehaviour
	{
		[Inject] private HeliController _heli;
		[Inject] private HumansSystem _humans;
		[Inject] private FireSystem _fire;
		[Inject] private GameRulesConfig _config;

		public GameResult Result { get; private set; }
		public int Score { get; private set; }
		public event Action<GameResult> ResultChanged;

		public void Initialize() { }

		private void RaiseResultChanged() => ResultChanged?.Invoke(Result);
	}
}
