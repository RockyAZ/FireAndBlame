using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>Trigger on layer Water, tag Pool. The bowl (tag Cup) inside fills the cup.</summary>
	public class PoolZone : MonoBehaviour
	{
		[Tooltip("The cup (its bowl is reparented at runtime, so it cannot be found from the collider).")]
		[SerializeField] private Cup cup;

		public void ReportEnter(Collider other)
		{
			if (cup != null && other.CompareTag("Cup")) cup.SetInPool(true);
			// TODO Phase 3: tag Human, unattached and not Dead -> Human.SetState(ChillingInPool).
		}

		public void ReportStay(Collider other)
		{
			if (cup != null && other.CompareTag("Cup")) cup.FillTick(Time.fixedDeltaTime);
		}

		public void ReportExit(Collider other)
		{
			if (cup != null && other.CompareTag("Cup")) cup.SetInPool(false);
		}

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerStay(Collider other) => ReportStay(other);
		private void OnTriggerExit(Collider other) => ReportExit(other);
	}
}
