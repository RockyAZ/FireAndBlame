using System;
using FlameAndBlame.Core;
using UnityEngine;

namespace FlameAndBlame.Fire
{
	public class FireSide : MonoBehaviour
	{
		[SerializeField] private Side side;

		public Side Side => side;
		public float Height01 { get; private set; }
		public bool IsBurning { get; private set; }
		public bool RoofOnFire { get; private set; }
		public bool IsLost { get; private set; }
		public float PauseTimeLeft { get; private set; }
		public event Action<FireSide> ReachedTop;

		public void Initialize(FireConfig config) { }
		public void ApplyBuilding(BuildingConfig building) { }
		public void Advance(float dt) { }
		public void Extinguish(float amount01) { }
		public void SetLost() { IsLost = true; }
		public void ReportEnter(Collider other) { }
		public void ReportStay(Collider other) { }

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerStay(Collider other) => ReportStay(other);
		private void RaiseReachedTop() => ReachedTop?.Invoke(this);
	}
}
