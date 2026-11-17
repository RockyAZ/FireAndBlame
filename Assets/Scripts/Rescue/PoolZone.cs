using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class PoolZone : MonoBehaviour
	{
		public void ReportEnter(Collider other) { }
		public void ReportStay(Collider other) { }
		public void ReportExit(Collider other) { }

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerStay(Collider other) => ReportStay(other);
		private void OnTriggerExit(Collider other) => ReportExit(other);
	}
}
