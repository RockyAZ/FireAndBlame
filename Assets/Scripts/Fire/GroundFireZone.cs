using FlameAndBlame.Core;
using FlameAndBlame.VFX;
using UnityEngine;

namespace FlameAndBlame.Fire
{
	/// <summary>Trigger on layer FireZone, tag GroundFire. Kills anything killable that touches it.</summary>
	public class GroundFireZone : MonoBehaviour
	{
		[Tooltip("GroundFireEffect scene child instance (optional).")]
		[SerializeField] private GroundFireEffect groundFireEffect;

		private FireConfig _config;
		private Collider _collider;

		public void Initialize(FireConfig config)
		{
			_config = config;
			_collider = GetComponent<Collider>();
			if (groundFireEffect != null) groundFireEffect.Play();
		}

		/// <summary>Only when groundFirePermanent is false: the zone is switched off for maxStopFireTime.</summary>
		public void Extinguish(float amount01)
		{
			if (_config == null || _config.groundFirePermanent || amount01 <= 0f) return;
			if (_collider != null) _collider.enabled = false;
			if (groundFireEffect != null) groundFireEffect.Stop();
			CancelInvoke(nameof(Restore));
			Invoke(nameof(Restore), _config.maxStopFireTime);
		}

		public void ReportEnter(Collider other) => Burn(other);
		public void ReportStay(Collider other) => Burn(other);

		private void Burn(Collider other)
		{
			// TODO Phase 3: only humans are IKillable; a hanging human low over the fire dies too.
			var killable = other.GetComponentInParent<IKillable>();
			if (killable != null) killable.Kill();
		}

		private void Restore()
		{
			if (_collider != null) _collider.enabled = true;
			if (groundFireEffect != null) groundFireEffect.Play();
		}

		private void OnTriggerEnter(Collider other) => ReportEnter(other);
		private void OnTriggerStay(Collider other) => ReportStay(other);
	}
}
