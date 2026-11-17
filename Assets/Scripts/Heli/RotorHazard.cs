using UnityEngine;

namespace FlameAndBlame.Heli
{
	public class RotorHazard : MonoBehaviour
	{
		private void OnCollisionEnter(Collision c) => ReportHit(c.collider);
		private void OnTriggerEnter(Collider other) => ReportHit(other);

		public void ReportHit(Collider other) { }
	}
}
