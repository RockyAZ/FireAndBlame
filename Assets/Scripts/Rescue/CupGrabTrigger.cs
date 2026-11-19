using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>On the Cup GrabZone (trigger, tag CupGrabZone) and optionally on the bowl: forwards contacts to Cup.ReportGrabContact.</summary>
	public class CupGrabTrigger : MonoBehaviour
	{
		[SerializeField] private Cup cup;

		private void OnTriggerEnter(Collider other) { if (cup != null) cup.ReportGrabContact(other); }
		private void OnTriggerStay(Collider other) { if (cup != null) cup.ReportGrabContact(other); }
		private void OnCollisionEnter(Collision c) { if (cup != null) cup.ReportGrabContact(c.collider); }
	}
}
