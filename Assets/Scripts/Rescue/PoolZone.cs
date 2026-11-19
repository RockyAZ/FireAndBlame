using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>Trigger on layer Water, tag Pool. The bowl (tag Cup) inside fills the cup; unattached humans entering float here.</summary>
	public class PoolZone : MonoBehaviour
	{
		[Tooltip("The cup (its bowl is reparented at runtime, so it cannot be found from the collider).")]
		[SerializeField] private Cup cup;
		[Tooltip("Child transform: its world Y is the water level.")]
		[SerializeField] private Transform waterSurface;
		[Tooltip("Child transform: its world position and scale are the box humans drift inside.")]
		[SerializeField] private Transform driftArea;

		public float SurfaceY => waterSurface != null ? waterSurface.position.y : transform.position.y;
		public Bounds DriftBounds => driftArea != null
			? new Bounds(driftArea.position, driftArea.lossyScale)
			: new Bounds(transform.position, transform.lossyScale);

		public void ReportEnter(Collider other)
		{
			if (other.CompareTag("Cup")) { if (cup != null) cup.SetInPool(true); }
			else if (other.CompareTag("Human")) Float(other);
		}

		public void ReportStay(Collider other)
		{
			if (other.CompareTag("Cup")) { if (cup != null) cup.FillTick(Time.fixedDeltaTime); return; }
			// A human released inside the pool never gets an Enter, so a falling one is caught here.
			if (other.CompareTag("Human")) { var h = other.GetComponentInParent<Human>(); if (h != null && h.IsFalling) Float(other); }
		}

		public void ReportExit(Collider other)
		{
			if (cup != null && other.CompareTag("Cup")) cup.SetInPool(false);
		}

		private void Float(Collider other)
		{
			var h = other.GetComponentInParent<Human>();
			if (h != null) h.EnterPool(this);
		}

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerStay(Collider other) => ReportStay(other);
		private void OnTriggerExit(Collider other) => ReportExit(other);
	}
}
