using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>
	/// Prefab: Rigidbody (gravity), SphereCollider trigger of diameter 1 (Cup scales it), layer Water, tag WaterDrop.
	/// </summary>
	public class WaterDrop : MonoBehaviour, ITestModeAware
	{
		private Rigidbody _body;
		private bool _useGravity = true;

		public float Amount01 { get; private set; }

		private void Awake()
		{
			_body = GetComponent<Rigidbody>();
			if (_body != null) _useGravity = _body.useGravity;
		}

		public void Launch(float amount01, Vector3 inheritedVelocity)
		{
			Amount01 = amount01;
			if (_body != null && !_body.isKinematic) _body.linearVelocity = inheritedVelocity;
		}

		/// <summary>Fire side catch trigger -> Extinguish; ground fire -> Extinguish; solids and pool -> destroyed.</summary>
		public void ReportHit(Collider other)
		{
			if (other == null) return;
			var side = other.GetComponentInParent<FireSide>();
			if (side != null) { side.Extinguish(Amount01); Destroy(gameObject); return; }
			var ground = other.GetComponentInParent<GroundFireZone>();
			if (ground != null) { ground.Extinguish(Amount01); Destroy(gameObject); return; }
			// TODO Phase 5: splash effect.
			if (other.GetComponentInParent<PoolZone>() != null || IsSolid(other.gameObject.layer)) Destroy(gameObject);
		}

		private static bool IsSolid(int layer) => (LayerMask.GetMask("Ground", "Building", "Obstacle") & (1 << layer)) != 0;

		private void OnTriggerEnter(Collider other) => ReportHit(other);

		public void EnterTestMode()
		{
			if (_body == null) return;
			_body.isKinematic = true;
			_body.useGravity = false;
		}

		public void ExitTestMode()
		{
			if (_body == null) return;
			_body.useGravity = _useGravity;
			_body.isKinematic = false;
		}
	}
}
