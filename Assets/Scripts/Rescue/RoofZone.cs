using FlameAndBlame.Fire;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>Trigger per roof side. Reads the fire flag of its FireSide.</summary>
	public class RoofZone : MonoBehaviour
	{
		[SerializeField] private FireSide fireSide;

		public bool OnFire => fireSide != null && fireSide.RoofOnFire;

		private void OnEnable() { if (fireSide != null) fireSide.ReachedTop += OnReachedTop; }
		private void OnDisable() { if (fireSide != null) fireSide.ReachedTop -= OnReachedTop; }

		public void ReportEnter(Collider other)
		{
			// TODO Phase 3: unattached, not Dead human -> SetState(WaitingOnRoof), then if OnFire -> Kill(); track in a set.
		}

		public void ReportExit(Collider other)
		{
			// TODO Phase 3: remove the human from the set.
		}

		private void OnReachedTop(FireSide side)
		{
			// TODO Phase 3: kill every unattached human in the set.
		}

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerExit(Collider other) => ReportExit(other);
	}
}
