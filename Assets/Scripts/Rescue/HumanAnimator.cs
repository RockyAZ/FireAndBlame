using System;
using FlameAndBlame.Core;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>Sets Animator triggers named in HumanConfig. No Animator or missing parameter: one warning, no-op.</summary>
	public class HumanAnimator : MonoBehaviour
	{
		private HumanConfig _config;
		private Animator _animator;
		private bool _warned;

		/// <summary>Name of the last trigger requested (recorded even without an Animator, for tests).</summary>
		public string LastTrigger { get; private set; }
		public event Action<string> TriggerFired;

		public void Initialize(HumanConfig config)
		{
			_config = config;
			_animator = GetComponentInChildren<Animator>();
		}

		public void Apply(HumanState state)
		{
			if (_config == null) return;
			switch (state)
			{
				case HumanState.WaitingOnRoof: Fire(_config.waitRescueTrigger); break;
				case HumanState.RescuedHanging: Fire(_config.hangingTrigger); break;
				case HumanState.ChillingInPool: Fire(_config.chillPoolTrigger); break;
				case HumanState.SwimmingInTank: Fire(_config.swimInTankTrigger); break;
				case HumanState.Dead: Fire(_config.deadTrigger); break;
			}
		}

		public void PlayStun() { if (_config != null) Fire(_config.stunnedTrigger); }
		public void PlayFalling() { if (_config != null) Fire(_config.fallingTrigger); }

		private void Fire(string triggerName)
		{
			LastTrigger = triggerName;
			TriggerFired?.Invoke(triggerName);
			if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
			{
				Warn("no active Animator with a controller");
				return;
			}
			int hash = Animator.StringToHash(triggerName);
			foreach (var p in _animator.parameters)
				if (p.nameHash == hash) { _animator.SetTrigger(hash); return; }
			Warn("Animator has no parameter '" + triggerName + "'");
		}

		private void Warn(string reason)
		{
			if (_warned) return;
			_warned = true;
			Debug.LogWarning("HumanAnimator: " + reason + ", animation skipped.", this);
		}
	}
}
