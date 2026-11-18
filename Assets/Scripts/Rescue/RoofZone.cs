using FlameAndBlame.Fire;
using FlameAndBlame.VFX;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>Trigger per roof side. Reads the fire flag of its FireSide.</summary>
	public class RoofZone : MonoBehaviour
	{
		[SerializeField] private FireSide fireSide;
		[Tooltip("FillEffectView on the RoofFire_<Side> child (optional).")]
		[SerializeField] private FillEffectView roofFireView;
		[Tooltip("Sector mesh of the same child (optional).")]
		[SerializeField] private RoofSectorMesh roofSector;

		private bool _setupDone;

		public bool OnFire => fireSide != null && fireSide.RoofOnFire;

		private void OnEnable() { if (fireSide != null) fireSide.ReachedTop += OnReachedTop; }
		private void OnDisable() { if (fireSide != null) fireSide.ReachedTop -= OnReachedTop; }

		private void Update() => Refresh();

		/// <summary>Presentation only: lazy one-time setup, then the roof fire fill follows OnFire.</summary>
		public void Refresh()
		{
			if (fireSide == null) return;
			if (!_setupDone && fireSide.Config != null)
			{
				_setupDone = true;
				if (roofFireView != null) roofFireView.SmoothTime = fireSide.Config.roofIgniteTime;
				var b = fireSide.Building;
				if (roofSector != null && b != null) roofSector.Initialize(b.footprintX, b.footprintZ);
			}
			if (roofFireView != null) roofFireView.SetFill(OnFire ? 1f : 0f);
		}

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
