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

		public IReadOnlyList<FireSide> Sides => sides;
		public int SidesNotOnFire
		{
			get { int n = 0; foreach (var s in sides) if (s != null && !s.IsBurning) n++; return n; }
		}
		public event Action AllSidesBurning;

		public void Initialize() { }
		public void SetAllLost() { foreach (var s in sides) if (s != null) s.SetLost(); }
		private void RaiseAllSidesBurning() => AllSidesBurning?.Invoke();
	}
}
