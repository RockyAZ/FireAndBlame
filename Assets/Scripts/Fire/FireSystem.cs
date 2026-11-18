using System;
using System.Collections.Generic;
using Injection;
using UnityEngine;

namespace FlameAndBlame.Fire
{
	public class FireSystem : MonoBehaviour
	{
		[Inject] private FireConfig _config;
		[Inject] private BuildingConfig _building;

		[SerializeField] private FireSide[] sides = new FireSide[4];
		[SerializeField] private GroundFireZone[] groundZones;

		[SerializeField] private RoofSlab[] roofSlabs;

		private bool _allBurningRaised;

		public IReadOnlyList<FireSide> Sides => sides;
		public IReadOnlyList<RoofSlab> RoofSlabs => roofSlabs;
		public int SidesNotOnFire
		{
			get { int n = 0; foreach (var s in sides) if (s != null && !s.IsBurning) n++; return n; }
		}
		public event Action AllSidesBurning;

		public void Initialize()
		{
			foreach (var s in sides)
			{
				if (s == null) continue;
				s.Initialize(_config);
				s.ApplyBuilding(_building);
				s.ReachedTop += OnSideReachedTop;
			}
			if (roofSlabs != null)
				foreach (var r in roofSlabs) if (r != null) r.Initialize(_config, _building);
			if (groundZones != null)
				foreach (var z in groundZones) if (z != null) z.Initialize(_config);
		}

		public void SetAllLost() { foreach (var s in sides) if (s != null) s.SetLost(); }

		private void OnSideReachedTop(FireSide _)
		{
			if (_allBurningRaised) return;
			foreach (var s in sides) if (s == null || s.Height01 < 1f) return;
			_allBurningRaised = true;
			AllSidesBurning?.Invoke();
		}

		private void OnDestroy()
		{
			foreach (var s in sides) if (s != null) s.ReachedTop -= OnSideReachedTop;
		}
	}
}
