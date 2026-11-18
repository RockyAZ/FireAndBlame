using UnityEngine;

namespace FlameAndBlame.Fire
{
	/// <summary>Put on the burn band child. Child triggers do not reach the parent, so this forwards to FireSide.</summary>
	public class FireSideTrigger : MonoBehaviour
	{
		[SerializeField] private FireSide fireSide;

		private void OnTriggerEnter(Collider other) { if (fireSide != null) fireSide.ReportEnter(other); }
		private void OnTriggerStay(Collider other) { if (fireSide != null) fireSide.ReportStay(other); }
	}
}
