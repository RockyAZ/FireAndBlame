using System;
using FlameAndBlame.Core;
using FlameAndBlame.Fire;
using FlameAndBlame.VFX;
using UnityEngine;

namespace FlameAndBlame.Rescue
{
	/// <summary>
	/// Prefab: Rigidbody (gravity), SphereCollider trigger of diameter 1 (Cup scales it), layer Water, tag WaterDrop.
	/// </summary>
	public class WaterDrop : MonoBehaviour, ITestModeAware
	{
		[Tooltip("Trail effect, child of the WaterDrop prefab (optional). Played on Launch, stopped on hit.")]
		[SerializeField] private WaterDropEffect trailEffect;
		[Tooltip("Splash prefab asset instantiated at the hit position on every hit (optional).")]
		[SerializeField] private SplashEffect splashPrefab;

		private const float SplashFallbackLifetime = 5f;   // cleanup if the prefab does not destroy itself

		private Rigidbody _body;
		private bool _useGravity = true;

		public float Amount01 { get; private set; }

		/// <summary>Raised once per hit; the side is null when no fire side was extinguished.</summary>
		public event Action<WaterDrop, FireSide> Hit;

		private void Awake()
		{
			_body = GetComponent<Rigidbody>();
			if (_body != null) _useGravity = _body.useGravity;
		}

		public void Launch(float amount01, Vector3 inheritedVelocity)
		{
			Amount01 = amount01;
			if (_body != null && !_body.isKinematic) _body.linearVelocity = inheritedVelocity;
			if (trailEffect != null) trailEffect.Play();
		}

		/// <summary>Fire side catch trigger -> Extinguish; ground fire -> Extinguish; solids and pool -> destroyed.</summary>
		public void ReportHit(Collider other)
		{
			if (other == null) return;
			var slab = other.GetComponentInParent<RoofSlab>();
			if (slab != null) { Finish(other, slab.FireSide, slab.FireSide); return; }
			var side = other.GetComponentInParent<FireSide>();
			if (side != null) { Finish(other, side, side); return; }
			var ground = other.GetComponentInParent<GroundFireZone>();
			if (ground != null) { ground.Extinguish(Amount01); Finish(other, null, null); return; }
			if (other.GetComponentInParent<PoolZone>() != null || IsSolid(other.gameObject.layer)) Finish(other, null, null);
		}

		// Extinguishes (if a side), raises Hit, plays the trail stop and splash, then destroys the drop.
		private void Finish(Collider other, FireSide extinguish, FireSide reported)
		{
			if (extinguish != null) extinguish.Extinguish(Amount01);
			if (trailEffect != null) trailEffect.Stop();
			if (splashPrefab != null)
			{
				bool noClosest = other is MeshCollider mc && !mc.convex;   // ClosestPoint is unsupported there
				Vector3 at = noClosest ? transform.position : other.ClosestPoint(transform.position);
				var splash = Instantiate(splashPrefab, at, Quaternion.identity);
				splash.Play();
				Destroy(splash.gameObject, SplashFallbackLifetime);
			}
			Hit?.Invoke(this, reported);
			Destroy(gameObject);
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
