using UnityEngine;

namespace FlameAndBlame.Fire
{
	public class GroundFireZone : MonoBehaviour
	{
		public void Initialize(FireConfig config) { }
		public void Extinguish(float amount01) { }
		public void ReportEnter(Collider other) { }
		public void ReportStay(Collider other) { }

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerStay(Collider other) => ReportStay(other);
	}
}
