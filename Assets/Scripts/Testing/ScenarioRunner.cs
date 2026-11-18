using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FlameAndBlame.Testing
{
	/// <summary>Runs named scenarios as coroutines and prints [TEST] lines. Scenarios are registered by tests or later phases.</summary>
	public class ScenarioRunner : MonoBehaviour
	{
		[SerializeField] private TestModeController testMode;

		private readonly Dictionary<string, Func<IEnumerator>> _scenarios = new Dictionary<string, Func<IEnumerator>>();

		public TestModeController TestMode => testMode;
		public IReadOnlyCollection<string> ScenarioNames => _scenarios.Keys;

		public void Register(string name, Func<IEnumerator> scenario) { _scenarios[name] = scenario; }

		public Coroutine Run(string name)
		{
			if (!_scenarios.TryGetValue(name, out var s)) { Fail(name, "unknown scenario"); return null; }
			return StartCoroutine(s());
		}

		public IEnumerator RunAll()
		{
			foreach (var s in new List<Func<IEnumerator>>(_scenarios.Values)) yield return StartCoroutine(s());
		}

		public static void Pass(string name) => Debug.Log("[TEST] " + name + " PASS");
		public static void Fail(string name, string reason) => Debug.Log("[TEST] " + name + " FAIL " + reason);

		public static void Check(string name, bool ok, string reason)
		{
			if (ok) Pass(name); else Fail(name, reason);
		}
	}
}
