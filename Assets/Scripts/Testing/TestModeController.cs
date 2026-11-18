using System.Collections.Generic;
using FlameAndBlame.Core;
using FlameAndBlame.Heli;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Testing
{
	public class TestModeController : MonoBehaviour
	{
		[Inject] private HeliController _heli;

		[Tooltip("The Game root object; every ITestModeAware under it is switched.")]
		[SerializeField] private Transform gameRoot;
		[SerializeField] private ScriptedControlSource scripted;

		private readonly List<ITestModeAware> _actors = new List<ITestModeAware>();

		public ScriptedControlSource Scripted => scripted;
		public bool IsActive { get; private set; }
		public IReadOnlyList<ITestModeAware> Actors => _actors;

		public void Initialize()
		{
			_actors.Clear();
			if (gameRoot == null) { Debug.LogWarning("TestModeController: gameRoot is not assigned."); return; }
			gameRoot.GetComponentsInChildren(true, _actors);
		}

		public void EnterAll()
		{
			if (IsActive) return;
			IsActive = true;
			foreach (var a in _actors) a.EnterTestMode();
			scripted.Set(default);
			_heli.SetControlSource(scripted);
		}

		public void ExitAll()
		{
			if (!IsActive) return;
			IsActive = false;
			// The heli restores the source it had before EnterTestMode.
			foreach (var a in _actors) a.ExitTestMode();
		}
	}
}
