using System;
using FlameAndBlame.Core;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Heli
{
	public class HeliController : MonoBehaviour, ITestModeAware
	{
		[Inject] private HeliConfig _config;
		[Inject] private IControlSource _source;

		private Rigidbody _body;

		public Rigidbody Body => _body != null ? _body : (_body = GetComponent<Rigidbody>());
		public bool IsCrashed { get; private set; }
		public event Action Crashed;

		public void Initialize() { }

		public void SetControlSource(IControlSource s) { _source = s; }

		public void Crash()
		{
			if (IsCrashed) return;
			IsCrashed = true;
			Crashed?.Invoke();
		}

		public void EnterTestMode() { }
		public void ExitTestMode() { }
	}
}
