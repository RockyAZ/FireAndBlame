using FlameAndBlame.Fire;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class RoofZone : MonoBehaviour
	{
		[SerializeField] private FireSide fireSide;

		public void ReportEnter(Collider other) { }
		public void ReportExit(Collider other) { }

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerExit(Collider other) => ReportExit(other);
	}
}
