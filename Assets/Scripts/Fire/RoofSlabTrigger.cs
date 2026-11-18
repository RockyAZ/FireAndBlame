using UnityEngine;

namespace FlameAndBlame.Fire
{
	/// <summary>On the RoofFire child (trigger collider, layer FireZone). Forwards contacts to the parent RoofSlab.</summary>
	public class RoofSlabTrigger : MonoBehaviour
	{
		private RoofSlab _slab;

		private RoofSlab Slab => _slab != null ? _slab : (_slab = GetComponentInParent<RoofSlab>());

		private void OnTriggerEnter(Collider other) { if (Slab != null) Slab.ReportStay(other); }
		private void OnTriggerStay(Collider other) { if (Slab != null) Slab.ReportStay(other); }
	}
}
