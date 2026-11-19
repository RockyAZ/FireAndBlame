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

		private bool _subscribed;

		public GameResult Result { get; private set; }
		public int Score { get; private set; }
		public int Rescued { get; private set; }
		public int Deaths { get; private set; }
		public int TotalHumans { get; private set; }
		public event Action<GameResult> ResultChanged;

		public void Initialize()
		{
			Result = GameResult.Playing;
			TotalHumans = _humans.Humans.Count;
			_heli.Crashed += OnCrashed;
			_fire.AllSidesBurning += OnAllSidesBurning;
			_humans.AnyStateChanged += OnStateChanged;
			_subscribed = true;
		}

		private void OnDestroy()
		{
			if (!_subscribed) return;
			_heli.Crashed -= OnCrashed;
			_fire.AllSidesBurning -= OnAllSidesBurning;
			_humans.AnyStateChanged -= OnStateChanged;
		}

		/// <summary>Re-checks the win rule (also called on every human state change).</summary>
		public void Evaluate()
		{
			if (Result != GameResult.Playing) return;
			int pending = _humans.CountIn(HumanState.WaitingOnRoof) + _humans.CountIn(HumanState.RescuedHanging)
				+ _humans.CountIn(HumanState.SwimmingInTank);
			if (pending > 0) return;
			End(_humans.CountIn(HumanState.ChillingInPool) >= 1 ? GameResult.Won : GameResult.Lost);
		}

		private void OnStateChanged(Human h, HumanState s) => Evaluate();

		private void OnAllSidesBurning()
		{
			if (Result == GameResult.Playing && _config.endOnAllSidesBurning) End(GameResult.Lost);
		}

		private void OnCrashed()
		{
			if (Result != GameResult.Playing) return;
			// Snapshot before KillAll, which would turn every human into a death.
			Rescued = _humans.CountIn(HumanState.ChillingInPool);
			Deaths = _humans.CountIn(HumanState.Dead);
			Result = GameResult.Lost;
			_humans.KillAll();
			_fire.SetAllLost();
			Score = ComputeScore();
			ResultChanged?.Invoke(Result);
		}

		private void End(GameResult result)
		{
			Rescued = _humans.CountIn(HumanState.ChillingInPool);
			Deaths = _humans.CountIn(HumanState.Dead);
			Result = result;
			Score = ComputeScore();
			ResultChanged?.Invoke(Result);
		}

		private int ComputeScore()
		{
			int s = Rescued * _config.scorePerRescued - Deaths * _config.penaltyPerDeath;
			if (Result == GameResult.Won) s += _fire.SidesNotOnFire * _config.bonusPerSideNotOnFire;
			return Mathf.Max(0, s);
		}
	}
}
