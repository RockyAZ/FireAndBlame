using System;
using FlameAndBlame.Core;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class Human : MonoBehaviour, ITestModeAware, IKillable
	{
		private Rigidbody _body;
		private HumanConfig _config;

		public HumanState State { get; private set; }
		public float Strength01 { get; private set; } = 1f;
		public bool IsStunned { get; private set; }
		public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());
		public event Action<Human, HumanState> StateChanged;
		public event Action<float> StrengthChanged;

		public void Initialize(HumanConfig config) { _config = config; }

		public void SetState(HumanState s)
		{
			if (s == State || State == HumanState.Dead) return;
			State = s;
			StateChanged?.Invoke(this, s);
		}

		public void AttachTo(Cup cup) { }
		public void Detach() { }
		public void Kill() { SetState(HumanState.Dead); }
		public void Stun() { }
		public void TickStrength(float dt, float acceleration) { }

		public void EnterTestMode() { }
		public void ExitTestMode() { }

		private void RaiseStrengthChanged() => StrengthChanged?.Invoke(Strength01);
	}
}
