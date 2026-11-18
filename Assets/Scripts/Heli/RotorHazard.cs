using UnityEngine;

namespace FlameAndBlame.Heli
{
	public class RotorHazard : MonoBehaviour
	{
		[Tooltip("Optional. Found with GetComponentInParent when empty.")]
		[SerializeField] private HeliController heli;

		public HeliController Heli => heli != null ? heli : (heli = GetComponentInParent<HeliController>());

		private void OnCollisionEnter(Collision c) => ReportHit(c.collider);
		private void OnTriggerEnter(Collider other) => ReportHit(other);

		public void ReportHit(Collider other)
		{
			if (other == null || other.isTrigger) return;
			if (other.CompareTag("Cup") || other.CompareTag("Human") || other.CompareTag("Heli") || other.CompareTag("WaterDrop")) return;
			var h = Heli;
			if (h == null) return;
			// Anything that belongs to the heli hierarchy (body, cup under it) is not an obstacle.
			if (other.GetComponentInParent<HeliController>() == h) return;
			h.Crash();
		}
	}
}
