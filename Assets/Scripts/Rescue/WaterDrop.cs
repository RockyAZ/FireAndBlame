using FlameAndBlame.Core;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	public class WaterDrop : MonoBehaviour, ITestModeAware
	{
		public float Amount01 { get; private set; }

		public void Launch(float amount01, Vector3 inheritedVelocity) { Amount01 = amount01; }
		public void ReportHit(Collider other) { }

		private void OnTriggerEnter(Collider other) => ReportHit(other);

		public void EnterTestMode() { }
		public void ExitTestMode() { }
	}
}
