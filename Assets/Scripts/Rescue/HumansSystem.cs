using System;
using System.Collections.Generic;
using FlameAndBlame.Core;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class HumansSystem : MonoBehaviour
	{
		[Inject] private HumanConfig _config;

		[SerializeField] private List<Human> humans = new List<Human>();

		public IReadOnlyList<Human> Humans => humans;
		public event Action<Human, HumanState> AnyStateChanged;

		public void Initialize() { }

		public int CountIn(HumanState s)
		{
			int n = 0;
			foreach (var h in humans) if (h != null && h.State == s) n++;
			return n;
		}

		public void KillAll() { foreach (var h in humans) if (h != null) h.Kill(); }

		private void RaiseAnyStateChanged(Human h, HumanState s) => AnyStateChanged?.Invoke(h, s);
	}
}
