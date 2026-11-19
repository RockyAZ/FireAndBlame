using System;
using System.Collections.Generic;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class HumansSystem : MonoBehaviour
	{
		[Inject] private HumanConfig _config;
		[Inject] private FireSystem _fire;

		[Tooltip("The Human prefab ASSET that is instantiated humanCount times.")]
		[SerializeField] private Human humanPrefab;
		[Tooltip("Preview instance placed in the scene; disabled in Initialize.")]
		[SerializeField] private GameObject scenePreview;
		[Tooltip("Parent of spawned humans (Game/Actors/Humans). Looked up by path if empty.")]
		[SerializeField] private Transform humansRoot;

		private readonly List<Human> _humans = new List<Human>();

		public IReadOnlyList<Human> Humans => _humans;
		public event Action<Human, HumanState> AnyStateChanged;

		/// <summary>Needs fire.Initialize() first (roof slabs are ready).</summary>
		public void Initialize()
		{
			if (scenePreview != null) scenePreview.SetActive(false);
			var slabs = _fire.RoofSlabs;
			if (humanPrefab == null || slabs == null || slabs.Count == 0)
			{
				Debug.LogWarning("HumansSystem: humanPrefab or roof slabs missing, no humans spawned.", this);
				return;
			}
			Transform root = ResolveRoot();
			for (int i = 0; i < _config.humanCount; i++)
			{
				var slab = slabs[i % slabs.Count];
				var h = Instantiate(humanPrefab, slab.GetSpawnPoint(), Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), root);
				h.name = "Human_" + (i + 1).ToString("00");
				h.Initialize(_config);
				h.SetState(HumanState.WaitingOnRoof);
				h.StateChanged += RaiseAnyStateChanged;
				_humans.Add(h);
			}
		}

		public int CountIn(HumanState s)
		{
			int n = 0;
			foreach (var h in _humans) if (h != null && h.State == s) n++;
			return n;
		}

		public void KillAll() { foreach (var h in _humans) if (h != null) h.Kill(); }

		private Transform ResolveRoot()
		{
			if (humansRoot != null) return humansRoot;
			var found = GameObject.Find("Game/Actors/Humans");
			return found != null ? found.transform : null;
		}

		private void RaiseAnyStateChanged(Human h, HumanState s) => AnyStateChanged?.Invoke(h, s);

		private void OnDestroy()
		{
			foreach (var h in _humans) if (h != null) h.StateChanged -= RaiseAnyStateChanged;
		}
	}
}
